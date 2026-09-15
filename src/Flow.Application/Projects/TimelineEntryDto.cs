using Flow.Domain.Entities;

namespace Flow.Application.Projects;

public record TimelineEntryDto(
    string Action,
    Guid ActorId,
    string ActorName,
    string? OldValue,
    string? NewValue,
    string? Reason,
    DateTimeOffset Timestamp)
{
    public static TimelineEntryDto From(AuditLog a) =>
        new(a.Action, a.ActorId, a.ActorName, a.OldValue, a.NewValue, a.Reason, a.Timestamp);
}
