using System.Text.Json;
using Flow.Application.Common.Exceptions;
using Flow.Application.Common.Interfaces;
using Flow.Application.Common.Persistence;
using Flow.Domain.Entities;
using Flow.Domain.Enums;

namespace Flow.Application.Assistant;

/// <summary>
/// Records every execution of an intelligent feature.
///
/// This is functional governance of the product: who asked, when, which model answered,
/// how long it took, what came back, and — later — whether a human accepted the
/// suggestion. It is what makes it possible to demonstrate that the assistant advises and
/// never decides.
///
/// It records no API key, no token, and no credential of any kind.
/// </summary>
public sealed class AssistantRunRecorder
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly IAssistantRunRepository _runs;
    private readonly ICurrentUserService _currentUser;
    private readonly ICorrelationIdAccessor _correlation;
    private readonly IFlowMetrics _metrics;

    public AssistantRunRecorder(
        IAssistantRunRepository runs,
        ICurrentUserService currentUser,
        ICorrelationIdAccessor correlation,
        IFlowMetrics metrics)
    {
        _runs = runs;
        _currentUser = currentUser;
        _correlation = correlation;
        _metrics = metrics;
    }

    public async Task<AssistantRun> RecordAsync<T>(
        AssistantOperation operation,
        AssistantResult<T> result,
        CancellationToken cancellationToken) where T : class
    {
        var userId = _currentUser.UserId
            ?? throw new InvalidOperationException("Authenticated user identity could not be resolved.");

        var run = AssistantRun.Record(
            userId: userId,
            userRole: _currentUser.Role ?? UserRole.Operator,
            operation: operation,
            model: result.Model,
            outcome: Map(result.Outcome),
            latencyMs: result.LatencyMs,
            structuredResult: result.Value is null
                ? null
                : JsonSerializer.Serialize(result.Value, SerializerOptions),
            promptTokens: result.PromptTokens,
            responseTokens: result.ResponseTokens,
            correlationId: _correlation.CorrelationId,
            errorKind: result.Outcome == AssistantOutcomeKind.Success ? null : result.Outcome.ToString());

        await _runs.AddAsync(run, cancellationToken);

        var label = operation.ToString();
        _metrics.AiRequest(label);
        _metrics.AiLatency(label, result.LatencyMs);

        if (result.Outcome != AssistantOutcomeKind.Success)
            _metrics.AiFailure(label, result.Outcome.ToString());

        return run;
    }

    /// <summary>
    /// Translates an unsuccessful assistant call into the right HTTP answer. The core
    /// product is unaffected by any of these, which is why they are surfaced as ordinary
    /// application failures rather than as a broken request.
    /// </summary>
    public static Exception ToException<T>(AssistantResult<T> result) => result.Outcome switch
    {
        AssistantOutcomeKind.NotConfigured => new AssistantUnavailableException(
            "A funcionalidade inteligente não está configurada neste ambiente."),
        AssistantOutcomeKind.Timeout => new AssistantUnavailableException(
            "O assistente demorou demais para responder. Tente novamente em instantes."),
        AssistantOutcomeKind.Unavailable => new AssistantUnavailableException(
            "O assistente está temporariamente indisponível. O restante do Flow segue funcionando."),
        AssistantOutcomeKind.MalformedResponse => new AssistantUnavailableException(
            "O assistente respondeu em um formato inesperado e a resposta foi descartada."),
        _ => new InvalidOperationException(result.Error ?? "Unknown assistant failure.")
    };

    private static Domain.Enums.AssistantOutcome Map(AssistantOutcomeKind kind) => kind switch
    {
        AssistantOutcomeKind.Success => Domain.Enums.AssistantOutcome.Success,
        AssistantOutcomeKind.Timeout => Domain.Enums.AssistantOutcome.Timeout,
        AssistantOutcomeKind.Unavailable => Domain.Enums.AssistantOutcome.Unavailable,
        AssistantOutcomeKind.NotConfigured => Domain.Enums.AssistantOutcome.Unavailable,
        _ => Domain.Enums.AssistantOutcome.Failed
    };
}

/// <summary>
/// The assistant could not answer. Deliberately distinct from a domain or validation
/// error: nothing the user did was wrong, and nothing in the product is broken.
/// </summary>
public sealed class AssistantUnavailableException : Exception
{
    public AssistantUnavailableException(string message) : base(message) { }
}
