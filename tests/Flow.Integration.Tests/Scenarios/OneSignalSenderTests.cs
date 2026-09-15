using System.Net;
using System.Text.Json;
using Flow.Application.Common.Interfaces;
using Flow.Infrastructure.Notifications;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Flow.Integration.Tests.Scenarios;

/// <summary>
/// The contract with the push provider, exercised against a fake transport.
///
/// Two things are being pinned down here. The first is which key travels in the request:
/// the provider requires an RFC 9562 UUID for its idempotency key, and our own dedupe key
/// — "IdeaApproved:{ideaId}:{userId}" — is not one. The second is what counts as proof of
/// delivery: this endpoint answers 200 for any request it accepted, and only returns a
/// message id when a message was actually created.
///
/// Nothing here talks to the real API. That would be neither reproducible nor free.
/// </summary>
public class OneSignalSenderTests
{
    /// <summary>Captures every request and answers with whatever the test scripted.</summary>
    private sealed class ScriptedHandler : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses = new();

        public List<JsonDocument> Payloads { get; } = [];
        public List<string?> AuthorizationHeaders { get; } = [];

        public ScriptedHandler Respond(HttpStatusCode status, string body = "{}")
        {
            _responses.Enqueue(new HttpResponseMessage(status)
            {
                Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json")
            });

            return this;
        }

        public ScriptedHandler Throw() => Respond((HttpStatusCode)599);

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var raw = await request.Content!.ReadAsStringAsync(cancellationToken);
            Payloads.Add(JsonDocument.Parse(raw));
            AuthorizationHeaders.Add(request.Headers.Authorization?.ToString());

            if (_responses.Count == 0) throw new InvalidOperationException("No scripted response left.");

            var response = _responses.Dequeue();

            if ((int)response.StatusCode == 599)
                throw new HttpRequestException("Simulated transport failure.");

            return response;
        }
    }

    private const string ApiKey = "fake-key-used-only-by-this-test";

    private static OneSignalPushSender Create(ScriptedHandler handler) =>
        new(
            new HttpClient(handler) { BaseAddress = new Uri("https://api.onesignal.com/") },
            Options.Create(new OneSignalOptions { AppId = "app-id", ApiKey = ApiKey }),
            NullLogger<OneSignalPushSender>.Instance);

    private static PushNotificationRequest Request(Guid? deliveryId = null, string? dedupeKey = null) =>
        new(
            UserId: Guid.NewGuid(),
            Title: "Ideia aprovada",
            Body: "Sua ideia foi aprovada.",
            DeepLink: "flow://ideas/1",
            DedupeKey: dedupeKey ?? $"IdeaApproved:{Guid.NewGuid()}:{Guid.NewGuid()}",
            DeliveryId: deliveryId ?? Guid.NewGuid());

    private static string IdempotencyKeyOf(JsonDocument payload) =>
        payload.RootElement.GetProperty("idempotency_key").GetString()!;

    // ─── The key that travels ───────────────────────────────────────────────

    [Fact]
    public async Task ThePayloadCarriesTheDeliveryIdAsAValidUuidIdempotencyKey()
    {
        var handler = new ScriptedHandler().Respond(HttpStatusCode.OK, """{"id":"msg-1"}""");
        var deliveryId = Guid.NewGuid();

        var result = await Create(handler).SendAsync(Request(deliveryId));

        result.Outcome.Should().Be(PushDeliveryOutcome.Delivered);

        var key = IdempotencyKeyOf(handler.Payloads.Single());
        key.Should().Be(deliveryId.ToString());
        Guid.TryParse(key, out _).Should().BeTrue(
            because: "the provider requires an RFC 9562 UUID and rejects anything else");
    }

    [Fact]
    public async Task TheInternalDedupeKeyNeverReachesTheProvider()
    {
        var handler = new ScriptedHandler().Respond(HttpStatusCode.OK, """{"id":"msg-1"}""");
        const string dedupeKey = "IdeaApproved:11111111-1111-1111-1111-111111111111:2222";

        await Create(handler).SendAsync(Request(dedupeKey: dedupeKey));

        var raw = handler.Payloads.Single().RootElement.GetRawText();

        raw.Should().NotContain(dedupeKey,
            because: "our dedupe key is shaped for our storage, not for the provider's API");
        raw.Should().NotContain("\"external_id\":\"IdeaApproved",
            because: "the legacy top-level external_id was being fed a value that is not a UUID");
    }

    [Fact]
    public async Task RetriesOfTheSameMessageReuseTheSameIdempotencyKey()
    {
        var handler = new ScriptedHandler()
            .Respond(HttpStatusCode.ServiceUnavailable, """{"errors":["temporarily down"]}""")
            .Respond(HttpStatusCode.OK, """{"id":"msg-1"}""");

        var sender = Create(handler);
        var deliveryId = Guid.NewGuid();

        // The same outbox message, attempted twice — which is exactly what the dispatcher
        // does after a transient failure.
        var first = await sender.SendAsync(Request(deliveryId));
        var second = await sender.SendAsync(Request(deliveryId));

        first.Outcome.Should().Be(PushDeliveryOutcome.TransientFailure);
        second.Outcome.Should().Be(PushDeliveryOutcome.Delivered);

        handler.Payloads.Should().HaveCount(2);
        IdempotencyKeyOf(handler.Payloads[0]).Should().Be(IdempotencyKeyOf(handler.Payloads[1]),
            because: "a key that changes per attempt deduplicates nothing");
    }

    [Fact]
    public async Task DifferentMessagesUseDifferentIdempotencyKeys()
    {
        var handler = new ScriptedHandler()
            .Respond(HttpStatusCode.OK, """{"id":"msg-1"}""")
            .Respond(HttpStatusCode.OK, """{"id":"msg-2"}""");

        var sender = Create(handler);

        await sender.SendAsync(Request());
        await sender.SendAsync(Request());

        IdempotencyKeyOf(handler.Payloads[0]).Should().NotBe(IdempotencyKeyOf(handler.Payloads[1]),
            because: "reusing a key across distinct sends would silently drop the second message");
    }

    // ─── What counts as delivered ───────────────────────────────────────────

    [Fact]
    public async Task SuccessWithAMessageIdIsDelivered()
    {
        var handler = new ScriptedHandler()
            .Respond(HttpStatusCode.OK, """{"id":"5eb5a37e-b458-11e3-ac11-000c2940e62c","recipients":1}""");

        var result = await Create(handler).SendAsync(Request());

        result.Outcome.Should().Be(PushDeliveryOutcome.Delivered);
        result.ProviderMessageId.Should().Be("5eb5a37e-b458-11e3-ac11-000c2940e62c");
    }

    [Fact]
    public async Task SuccessWithAMessageIdAndPartialErrorsIsStillDelivered()
    {
        // The shape the provider returns when the notification WAS created but some
        // recipients were skipped. Here "errors" is an object, not an array of strings, and
        // it travels alongside a perfectly valid id.
        var handler = new ScriptedHandler().Respond(
            HttpStatusCode.OK,
            """
            {
              "id": "5eb5a37e-b458-11e3-ac11-000c2940e62c",
              "external_id": "9c1f0b6e-2f4a-4a5b-9c0d-1e2f3a4b5c6d",
              "errors": {
                "invalid_aliases": {
                  "external_id": ["user_a", "user_b", "user_c"]
                }
              },
              "warnings": ["some warning"]
            }
            """);

        var result = await Create(handler).SendAsync(Request());

        result.Outcome.Should().Be(PushDeliveryOutcome.Delivered,
            because: "the id says the message was created; errors here describes skipped "
                   + "recipients, and retrying would resend something that already went out");
        result.ProviderMessageId.Should().Be("5eb5a37e-b458-11e3-ac11-000c2940e62c");
    }

    [Fact]
    public async Task AnErrorsObjectDoesNotBreakParsingWhenThereIsNoId()
    {
        // Same object shape, but nothing was created. It must still parse, and it must
        // still not count as delivered.
        var handler = new ScriptedHandler().Respond(
            HttpStatusCode.OK,
            """{"id":"","errors":{"invalid_player_ids":["a","b"]},"warnings":{}}""");

        var result = await Create(handler).SendAsync(Request());

        result.Outcome.Should().NotBe(PushDeliveryOutcome.Delivered);
        result.Outcome.Should().Be(PushDeliveryOutcome.PermanentFailure);
        result.Error.Should().Contain("invalid_player_ids");
    }

    [Fact]
    public async Task AResponseWithNoIdFieldAtAllAndAnErrorsObjectIsNotDelivered()
    {
        var handler = new ScriptedHandler().Respond(
            HttpStatusCode.OK,
            """{"errors":{"invalid_aliases":{"external_id":["only-one"]}}}""");

        var result = await Create(handler).SendAsync(Request());

        result.Outcome.Should().NotBe(PushDeliveryOutcome.Delivered,
            because: "an absent id is as much of a no as an empty one");
    }

    [Fact]
    public async Task TheErrorSummaryReportsShapeRatherThanRecipientIdentifiers()
    {
        var handler = new ScriptedHandler().Respond(
            HttpStatusCode.OK,
            """{"id":"","errors":{"invalid_aliases":{"external_id":["u-1","u-2","u-3"]}}}""");

        var result = await Create(handler).SendAsync(Request());

        result.Error.Should().Contain("invalid_aliases");
        result.Error.Should().Contain("3", because: "the count is the useful part");
        result.Error.Should().NotContain("u-1",
            because: "the values are recipient identifiers and do not belong in an error string");
    }

    [Fact]
    public async Task SuccessWithoutAMessageIdButWithAReasonIsNotDelivered()
    {
        // The shape the provider actually returns when nobody in the audience is
        // subscribed: HTTP 200, no id, and an explanation.
        var handler = new ScriptedHandler().Respond(
            HttpStatusCode.OK,
            """{"id":"","recipients":0,"errors":["All included players are not subscribed"]}""");

        var result = await Create(handler).SendAsync(Request());

        result.Outcome.Should().NotBe(PushDeliveryOutcome.Delivered,
            because: "no message id means the provider created no message, whatever the status code said");
        result.Outcome.Should().Be(PushDeliveryOutcome.PermanentFailure,
            because: "an unsubscribed recipient is a fact about them, not a blip worth retrying");
        result.Error.Should().Contain("not subscribed");
    }

    [Fact]
    public async Task SuccessWithNeitherIdNorReasonIsRetriedRatherThanTrusted()
    {
        var handler = new ScriptedHandler().Respond(HttpStatusCode.OK, """{"recipients":0}""");

        var result = await Create(handler).SendAsync(Request());

        // Ambiguous. Retrying is safe here only because the delivery carries a stable
        // idempotency key, so a message that was in fact created is not duplicated.
        result.Outcome.Should().Be(PushDeliveryOutcome.TransientFailure);
    }

    [Fact]
    public async Task SuccessWithAnUnreadableBodyIsNotDelivered()
    {
        var handler = new ScriptedHandler().Respond(HttpStatusCode.OK, "not json at all");

        var result = await Create(handler).SendAsync(Request());

        result.Outcome.Should().NotBe(PushDeliveryOutcome.Delivered);
    }

    // ─── Failure classification ─────────────────────────────────────────────

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.BadRequest)]
    public async Task RejectedCredentialsAndMalformedRequestsArePermanent(HttpStatusCode status)
    {
        var handler = new ScriptedHandler().Respond(status, """{"errors":["nope"]}""");

        var result = await Create(handler).SendAsync(Request());

        result.Outcome.Should().Be(PushDeliveryOutcome.PermanentFailure,
            because: "no number of retries fixes a wrong key or a malformed request");
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.GatewayTimeout)]
    [InlineData(HttpStatusCode.RequestTimeout)]
    public async Task RateLimitsAndServerFaultsAreTransient(HttpStatusCode status)
    {
        var handler = new ScriptedHandler().Respond(status, """{"errors":["later"]}""");

        var result = await Create(handler).SendAsync(Request());

        result.Outcome.Should().Be(PushDeliveryOutcome.TransientFailure);
    }

    [Fact]
    public async Task ATransportFailureIsTransient()
    {
        var handler = new ScriptedHandler().Throw();

        var result = await Create(handler).SendAsync(Request());

        result.Outcome.Should().Be(PushDeliveryOutcome.TransientFailure);
    }

    // ─── Credentials ────────────────────────────────────────────────────────

    [Fact]
    public async Task WithoutCredentialsNothingIsSentAndNothingIsClaimed()
    {
        var handler = new ScriptedHandler();

        var sender = new OneSignalPushSender(
            new HttpClient(handler) { BaseAddress = new Uri("https://api.onesignal.com/") },
            Options.Create(new OneSignalOptions { AppId = "", ApiKey = "" }),
            NullLogger<OneSignalPushSender>.Instance);

        var result = await sender.SendAsync(Request());

        result.Outcome.Should().Be(PushDeliveryOutcome.NotConfigured);
        handler.Payloads.Should().BeEmpty(because: "there is nothing to send a request with");
    }

    [Fact]
    public async Task TheApiKeyTravelsOnlyInTheAuthorizationHeaderAndNeverInTheBody()
    {
        var handler = new ScriptedHandler().Respond(HttpStatusCode.OK, """{"id":"msg-1"}""");

        await Create(handler).SendAsync(Request());

        handler.AuthorizationHeaders.Single().Should().Be($"Key {ApiKey}",
            because: "the provider's current scheme is Key, not Basic");
        handler.Payloads.Single().RootElement.GetRawText().Should().NotContain(ApiKey);
    }
}
