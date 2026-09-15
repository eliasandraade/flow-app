using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Flow.Application.Assistant;
using Google.GenAI;
using Google.GenAI.Types;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Flow.Infrastructure.Assistant;

/// <summary>
/// One place where Gemini is actually called.
///
/// Everything the product needs from the model is a typed object, so every call asks for
/// structured JSON against an explicit schema and parses it. A free-text answer is treated
/// as a failure, not as something to show the user.
///
/// Retries are deliberately absent: content generation is a non-idempotent, metered POST.
/// Repeating it after a timeout would pay twice for an answer that may already be on its
/// way back. Timeouts and a circuit breaker cover the failure modes that matter.
/// </summary>
public sealed class GeminiStructuredClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly GeminiOptions _options;
    private readonly CircuitBreaker _circuitBreaker;
    private readonly ILogger<GeminiStructuredClient> _logger;
    private readonly Lazy<Client?> _client;

    public GeminiStructuredClient(
        IOptions<GeminiOptions> options,
        CircuitBreaker circuitBreaker,
        ILogger<GeminiStructuredClient> logger)
    {
        _options = options.Value;
        _circuitBreaker = circuitBreaker;
        _logger = logger;

        _client = new Lazy<Client?>(() => IsConfigured
            ? new Client(
                apiKey: _options.ApiKey,
                httpOptions: new HttpOptions { Timeout = (int)_options.Timeout.TotalMilliseconds })
            : null);
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_options.ApiKey);

    public string Model => _options.Model;

    public async Task<AssistantResult<T>> GenerateAsync<T>(
        string systemInstruction,
        string prompt,
        JsonNode responseSchema,
        CancellationToken cancellationToken) where T : class
    {
        if (!IsConfigured)
            return new AssistantResult<T>(
                AssistantOutcomeKind.NotConfigured, null, _options.Model, 0,
                Error: "Gemini:ApiKey is not configured.");

        if (!_circuitBreaker.AllowRequest())
            return new AssistantResult<T>(
                AssistantOutcomeKind.Unavailable, null, _options.Model, 0,
                Error: "The assistant is temporarily unavailable after repeated failures.");

        var stopwatch = Stopwatch.StartNew();

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_options.Timeout);

        try
        {
            var config = new GenerateContentConfig
            {
                SystemInstruction = new Content
                {
                    Role = "system",
                    Parts = [new Part { Text = systemInstruction }]
                },
                ResponseMimeType = "application/json",
                ResponseJsonSchema = responseSchema,
                Temperature = _options.Temperature,
                MaxOutputTokens = _options.MaxOutputTokens
            };

            var response = await _client.Value!.Models.GenerateContentAsync(
                model: _options.Model,
                contents: prompt,
                config: config,
                cancellationToken: timeout.Token);

            stopwatch.Stop();

            var promptTokens = response.UsageMetadata?.PromptTokenCount;
            var responseTokens = response.UsageMetadata?.CandidatesTokenCount;

            var text = response.Text;

            if (string.IsNullOrWhiteSpace(text))
            {
                // A blocked or truncated answer is far more useful named than as an empty box.
                var finish = response.Candidates?.FirstOrDefault()?.FinishReason?.ToString() ?? "unknown";

                _circuitBreaker.RecordFailure();
                return Failure<T>(AssistantOutcomeKind.MalformedResponse, stopwatch,
                    $"The model returned no usable content (finish reason: {finish}).",
                    promptTokens, responseTokens);
            }

            T? parsed;
            try
            {
                parsed = JsonSerializer.Deserialize<T>(text, JsonOptions);
            }
            catch (JsonException ex)
            {
                _circuitBreaker.RecordFailure();

                // The payload itself is not logged: it may echo business content.
                _logger.LogWarning("Assistant response failed to parse: {Reason}", ex.Message);

                return Failure<T>(AssistantOutcomeKind.MalformedResponse, stopwatch,
                    "The model response did not match the expected structure.",
                    promptTokens, responseTokens);
            }

            if (parsed is null)
            {
                _circuitBreaker.RecordFailure();
                return Failure<T>(AssistantOutcomeKind.MalformedResponse, stopwatch,
                    "The model response deserialised to null.", promptTokens, responseTokens);
            }

            _circuitBreaker.RecordSuccess();

            return new AssistantResult<T>(
                AssistantOutcomeKind.Success, parsed, _options.Model,
                stopwatch.ElapsedMilliseconds, promptTokens, responseTokens);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The caller went away; that is not a provider failure.
            stopwatch.Stop();
            throw;
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            _circuitBreaker.RecordFailure();

            return Failure<T>(AssistantOutcomeKind.Timeout, stopwatch,
                $"The assistant did not answer within {_options.Timeout.TotalSeconds:0}s.");
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _circuitBreaker.RecordFailure();

            // Never log the exception object wholesale: provider errors can echo the
            // request, and the request carries the API key header.
            _logger.LogError("Assistant call failed: {ExceptionType}", ex.GetType().Name);

            return Failure<T>(AssistantOutcomeKind.Unavailable, stopwatch,
                "The assistant provider is unavailable.");
        }
    }

    private AssistantResult<T> Failure<T>(
        AssistantOutcomeKind kind,
        Stopwatch stopwatch,
        string error,
        int? promptTokens = null,
        int? responseTokens = null) where T : class =>
        new(kind, null, _options.Model, stopwatch.ElapsedMilliseconds, promptTokens, responseTokens, error);
}
