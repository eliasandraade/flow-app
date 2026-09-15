using Flow.Domain.Entities;

namespace Flow.Application.Projects;

public record ProjectSnapshotDto(
    Guid Id,
    Guid ProjectId,
    string Title,
    string Status,
    string Stage,
    int ProgressPercentage,
    string Priority,
    Guid OwnerId,
    string OwnerName,
    Guid? LinkedGuidelineId,
    decimal? EstimatedCost,
    decimal? ActualCost,
    DateTimeOffset? StartDate,
    DateTimeOffset? Deadline,
    DateTimeOffset? CompletedAt,
    string? BlockedReason,
    string? CancelledReason,
    string TriggerAction,
    Guid TriggeredByActorId,
    int SchemaVersion,
    DateTimeOffset TakenAt)
{
    public static ProjectSnapshotDto From(ProjectSnapshot s) => new(
        s.Id, s.ProjectId, s.Title, s.Status.ToString(), s.Stage.ToString(), s.ProgressPercentage,
        s.Priority.ToString(), s.OwnerId, s.OwnerName, s.LinkedGuidelineId,
        s.EstimatedCost, s.ActualCost, s.StartDate, s.Deadline, s.CompletedAt,
        s.BlockedReason, s.CancelledReason, s.TriggerAction, s.TriggeredByActorId,
        s.SchemaVersion, s.TakenAt);
}
