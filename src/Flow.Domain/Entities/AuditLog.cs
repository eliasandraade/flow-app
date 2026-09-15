using Flow.Domain.Exceptions;

namespace Flow.Domain.Entities;

/// <summary>Append-only governance record. Never updated and never deleted.</summary>
public class AuditLog
{
    public Guid Id { get; private set; }
    public string EntityType { get; private set; } = string.Empty;
    public Guid EntityId { get; private set; }
    public string Action { get; private set; } = string.Empty;
    public Guid ActorId { get; private set; }
    public string ActorName { get; private set; } = string.Empty;
    public string? OldValue { get; private set; }
    public string? NewValue { get; private set; }
    public string? Reason { get; private set; }

    /// <summary>Links the governance record to the distributed trace that produced it.</summary>
    public string? CorrelationId { get; private set; }

    public DateTimeOffset Timestamp { get; private set; }

    private AuditLog() { }

    public static AuditLog Create(
        string entityType,
        Guid entityId,
        string action,
        Guid actorId,
        string actorName,
        string? oldValue = null,
        string? newValue = null,
        string? reason = null,
        string? correlationId = null)
    {
        if (string.IsNullOrWhiteSpace(entityType))
            throw new DomainException("AuditLog requires an entity type.");
        if (entityId == Guid.Empty)
            throw new DomainException("AuditLog requires a valid entity id.");
        if (string.IsNullOrWhiteSpace(action))
            throw new DomainException("AuditLog requires an action.");
        if (actorId == Guid.Empty)
            throw new DomainException("AuditLog requires a valid actor.");

        return new AuditLog
        {
            Id = Guid.NewGuid(),
            EntityType = entityType,
            EntityId = entityId,
            Action = action,
            ActorId = actorId,
            ActorName = actorName,
            OldValue = oldValue,
            NewValue = newValue,
            Reason = reason,
            CorrelationId = correlationId,
            Timestamp = DateTimeOffset.UtcNow
        };
    }
}
