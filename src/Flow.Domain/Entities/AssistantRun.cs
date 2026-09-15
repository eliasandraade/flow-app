using Flow.Domain.Enums;
using Flow.Domain.Exceptions;

namespace Flow.Domain.Entities;

/// <summary>
/// Governance record of one execution of an intelligent feature: who asked, when, which
/// model answered, what came back, and whether the suggestion was later accepted.
///
/// This is functional governance of the product, not authorship of code. It never stores
/// an API key, a JWT, or credentials of any kind.
/// </summary>
public class AssistantRun
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public UserRole UserRole { get; private set; }
    public AssistantOperation Operation { get; private set; }
    public string Model { get; private set; } = string.Empty;
    public DateTimeOffset RequestedAt { get; private set; }
    public long LatencyMs { get; private set; }
    public AssistantOutcome Outcome { get; private set; }
    public int? PromptTokens { get; private set; }
    public int? ResponseTokens { get; private set; }
    public string? CorrelationId { get; private set; }

    /// <summary>Structured payload returned to the user, serialised for later inspection.</summary>
    public string? StructuredResult { get; private set; }

    public string? ErrorKind { get; private set; }

    /// <summary>
    /// A suggestion is only ever "accepted" by a human running a normal application
    /// command. The model itself never writes to the database.
    /// </summary>
    public bool SuggestionAccepted { get; private set; }

    public string? AcceptedEntityType { get; private set; }
    public Guid? AcceptedEntityId { get; private set; }

    private AssistantRun() { }

    public static AssistantRun Record(
        Guid userId,
        UserRole userRole,
        AssistantOperation operation,
        string model,
        AssistantOutcome outcome,
        long latencyMs,
        string? structuredResult = null,
        int? promptTokens = null,
        int? responseTokens = null,
        string? correlationId = null,
        string? errorKind = null)
    {
        if (userId == Guid.Empty)
            throw new DomainException("An assistant run must record its requester.");
        if (string.IsNullOrWhiteSpace(model))
            throw new DomainException("An assistant run must record the model used.");

        return new AssistantRun
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            UserRole = userRole,
            Operation = operation,
            Model = model,
            Outcome = outcome,
            LatencyMs = latencyMs,
            StructuredResult = structuredResult,
            PromptTokens = promptTokens,
            ResponseTokens = responseTokens,
            CorrelationId = correlationId,
            ErrorKind = errorKind,
            RequestedAt = DateTimeOffset.UtcNow,
            SuggestionAccepted = false
        };
    }

    public void MarkSuggestionAccepted(string entityType, Guid entityId)
    {
        if (string.IsNullOrWhiteSpace(entityType))
            throw new DomainException("Accepted suggestion requires an entity type.");
        if (entityId == Guid.Empty)
            throw new DomainException("Accepted suggestion requires a valid entity id.");

        SuggestionAccepted = true;
        AcceptedEntityType = entityType;
        AcceptedEntityId = entityId;
    }
}
