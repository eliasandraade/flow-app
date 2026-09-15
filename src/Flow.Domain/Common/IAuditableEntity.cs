namespace Flow.Domain.Common;

public interface IAuditableEntity
{
    DateTimeOffset CreatedAt { get; }
    DateTimeOffset UpdatedAt { get; }
}
