namespace Flow.Domain.Common;

public abstract class BaseEntity : IAuditableEntity
{
    public Guid Id { get; protected set; } = Guid.NewGuid();
    public DateTimeOffset CreatedAt { get; protected set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; protected set; } = DateTimeOffset.UtcNow;

    protected void SetUpdated() => UpdatedAt = DateTimeOffset.UtcNow;
}
