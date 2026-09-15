using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Flow.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Flow.Infrastructure.Notifications;

/// <summary>
/// Sends push notifications through OneSignal.
///
/// Users are addressed by external id, which the mobile client sets to the Flow user id
/// after login. E-mail is never used as an identifier: it is neither stable nor an
/// authorisation claim.
///
/// The REST API key lives only here, on the server. It is never shipped to the app, and it
/// never reaches a log line.
/// </summary>
public sealed class OneSignalPushSender : IPushNotificationSender
{
    public const string HttpClientName = "onesignal";

    private readonly HttpClient _httpClient;
    private readonly OneSignalOptions _options;
    private readonly ILogger<OneSignalPushSender> _logger;

    public OneSignalPushSender(
        HttpClient httpClient,
        IOptions<OneSignalOptions> options,
        ILogger<OneSignalPushSender> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_options.AppId) && !string.IsNullOrWhiteSpace(_options.ApiKey);

    public async Task<PushDeliveryResult> SendAsync(
        PushNotificationRequest request, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured) return PushDeliveryResult.NotConfigured();

        var payload = new OneSignalRequest
        {
            AppId = _options.AppId,
            TargetChannel = "push",
            IncludeAliases = new AliasTargets { ExternalId = [request.UserId.ToString()] },
            Headings = new Dictionary<string, string> { ["en"] = request.Title },
            Contents = new Dictionary<string, string> { ["en"] = request.Body },
            Data = request.DeepLink is null
                ? null
                : new Dictionary<string, string> { ["deepLink"] = request.DeepLink },

            // The provider requires an RFC 9562 UUID here and keeps it for 30 days, so the
            // outbox message id is used verbatim: it is already a Guid, and it is the same
            // value on every retry of the same message. Our own DedupeKey is not a UUID and
            // does not belong in this field.
            IdempotencyKey = request.DeliveryId.ToString()
        };

        try
        {
            using var message = new HttpRequestMessage(HttpMethod.Post, "notifications")
            {
                Content = JsonContent.Create(payload)
            };

            // OneSignal's current scheme is "Key <api key>", not Basic.
            message.Headers.Authorization = new AuthenticationHeaderValue("Key", _options.ApiKey);

            using var response = await _httpClient.SendAsync(message, cancellationToken);

            if (response.IsSuccessStatusCode)
                return await InterpretSuccessAsync(response, request, cancellationToken);

            var detail = await SafeReadAsync(response, cancellationToken);

            // 401/403 mean the credentials are wrong; retrying will not fix that, and a
            // 4xx body is a request problem rather than a blip.
            return IsTransient(response.StatusCode)
                ? PushDeliveryResult.Transient($"HTTP {(int)response.StatusCode}: {detail}")
                : PushDeliveryResult.Permanent($"HTTP {(int)response.StatusCode}: {detail}");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return PushDeliveryResult.Transient("Timed out waiting for the push provider.");
        }
        catch (HttpRequestException ex)
        {
            return PushDeliveryResult.Transient(ex.Message);
        }
        catch (Exception ex)
        {
            // Logged without the request, which carries the Authorization header.
            _logger.LogError(ex, "Unexpected failure dispatching a push notification.");
            return PushDeliveryResult.Transient("Unexpected provider failure.");
        }
    }

    /// <summary>
    /// A 2xx from this endpoint does not mean a notification exists, and an "errors" field
    /// does not mean it does not.
    ///
    /// The provider answers 200 for any request it accepted, and <c>id</c> is what
    /// discriminates: a UUID means the message was created and dispatched to at least one
    /// subscriber; an empty or absent id means nothing was created. Treating every 2xx as
    /// delivered fills an outbox with messages marked sent that nobody received.
    ///
    /// <c>errors</c> is polymorphic and cannot be read as one shape. It arrives as an array
    /// of strings when nothing was created — "All included players are not subscribed" — and
    /// as an object when the message <b>was</b> created but some recipients were skipped,
    /// carrying keys such as invalid_aliases or invalid_player_ids. Both forms travel under
    /// the same name, so the field is kept as raw JSON and interpreted, rather than being
    /// bound to a type that only fits half the contract. Binding it to a list of strings
    /// made the object form throw, which turned a partial success into an unreadable body
    /// and a retry of a message that had already gone out.
    /// </summary>
    private async Task<PushDeliveryResult> InterpretSuccessAsync(
        HttpResponseMessage response,
        PushNotificationRequest request,
        CancellationToken cancellationToken)
    {
        OneSignalResponse? body;

        try
        {
            body = await response.Content.ReadFromJsonAsync<OneSignalResponse>(cancellationToken);
        }
        catch (Exception ex) when (ex is System.Text.Json.JsonException or InvalidOperationException
            or HttpRequestException)
        {
            // Accepted, but the answer is unreadable, so there is no evidence a message
            // was created. Retrying is safe because the delivery carries a stable
            // idempotency key.
            return PushDeliveryResult.Transient(
                "The push provider answered with a body that could not be read.");
        }

        if (!string.IsNullOrWhiteSpace(body?.Id))
        {
            // Created. Anything in errors describes recipients that were skipped, not the
            // message, so it is a warning and not a failure — reporting it as a failure
            // would schedule a retry of a notification that already went out.
            if (Describe(body!.Errors) is { } skipped)
            {
                _logger.LogWarning(
                    "Push provider created message {ProviderMessageId} for outbox message "
                    + "{DeliveryId} but skipped some recipients: {Skipped}",
                    body.Id, request.DeliveryId, skipped);
            }

            return PushDeliveryResult.Delivered(body!.Id);
        }

        // No id: nothing was created. The provider usually says why, and the reason is a
        // fact about this recipient rather than a blip, so retrying would just repeat it.
        if (Describe(body?.Errors) is { } reason)
        {
            _logger.LogWarning(
                "Push provider accepted the request for outbox message {DeliveryId} but created "
                + "no message: {Reason}",
                request.DeliveryId, reason);

            return PushDeliveryResult.Permanent($"Provider created no message: {reason}");
        }

        // Accepted, no id, no reason. Ambiguous, and one more attempt is safe precisely
        // because the delivery carries a stable idempotency key: if a message was in fact
        // created, the retry returns the original result instead of duplicating it.
        return PushDeliveryResult.Transient(
            "Provider returned success without a message id and without a reason.");
    }

    /// <summary>
    /// Turns whichever shape "errors" arrived in into one short line, or null when there is
    /// nothing to say.
    ///
    /// The object form is summarised by key and count rather than dumped: its values are
    /// lists of recipient identifiers, and a log line is not the place to spill a list of
    /// users. The shape is what a person debugging this actually needs.
    /// </summary>
    private static string? Describe(JsonElement? errors)
    {
        if (errors is not { } element) return null;

        switch (element.ValueKind)
        {
            case JsonValueKind.Array:
            {
                var reasons = element.EnumerateArray()
                    .Where(item => item.ValueKind == JsonValueKind.String)
                    .Select(item => item.GetString())
                    .Where(text => !string.IsNullOrWhiteSpace(text))
                    .Take(5)
                    .ToArray();

                return reasons.Length == 0 ? null : string.Join("; ", reasons);
            }

            case JsonValueKind.Object:
            {
                var parts = element.EnumerateObject()
                    .Take(5)
                    .Select(property => $"{property.Name}({Count(property.Value)})")
                    .ToArray();

                return parts.Length == 0 ? null : string.Join("; ", parts);
            }

            case JsonValueKind.String:
                return string.IsNullOrWhiteSpace(element.GetString()) ? null : element.GetString();

            default:
                return null;
        }
    }

    /// <summary>How many entries a nested error value holds, without revealing them.</summary>
    private static int Count(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Array => value.GetArrayLength(),
        JsonValueKind.Object => value.EnumerateObject().Sum(nested => Count(nested.Value)),
        JsonValueKind.Undefined or JsonValueKind.Null => 0,
        _ => 1
    };

    private static bool IsTransient(HttpStatusCode status) =>
        status is HttpStatusCode.RequestTimeout
            or HttpStatusCode.TooManyRequests
            or HttpStatusCode.InternalServerError
            or HttpStatusCode.BadGateway
            or HttpStatusCode.ServiceUnavailable
            or HttpStatusCode.GatewayTimeout;

    private static async Task<string> SafeReadAsync(
        HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            return content.Length > 500 ? content[..500] : content;
        }
        catch
        {
            return "<unreadable response body>";
        }
    }

    private sealed class OneSignalRequest
    {
        [JsonPropertyName("app_id")] public string AppId { get; init; } = string.Empty;
        [JsonPropertyName("target_channel")] public string TargetChannel { get; init; } = "push";
        [JsonPropertyName("include_aliases")] public AliasTargets IncludeAliases { get; init; } = new();
        [JsonPropertyName("headings")] public Dictionary<string, string> Headings { get; init; } = [];
        [JsonPropertyName("contents")] public Dictionary<string, string> Contents { get; init; } = [];

        [JsonPropertyName("data")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Dictionary<string, string>? Data { get; init; }

        [JsonPropertyName("idempotency_key")] public string IdempotencyKey { get; init; } = string.Empty;
    }

    private sealed class AliasTargets
    {
        [JsonPropertyName("external_id")] public List<string> ExternalId { get; init; } = [];
    }

    private sealed class OneSignalResponse
    {
        [JsonPropertyName("id")] public string? Id { get; init; }

        // Raw on purpose: the provider sends an array of strings in one situation and an
        // object in another, under the same name. See InterpretSuccessAsync.
        [JsonPropertyName("errors")] public JsonElement? Errors { get; init; }

        [JsonPropertyName("warnings")] public JsonElement? Warnings { get; init; }
    }
}
