using Flow.Domain.Enums;

namespace Flow.Domain.Entities;

/// <summary>
/// Immutable, append-only capture of a project at the moment of a transition.
/// Never updated and never deleted.
/// </summary>
public class ProjectSnapshot
{
    /// <summary>
    /// Version 2 adds Stage, ProgressPercentage and LinkedGuidelineId. Version 1 snapshots
    /// remain interpretable because the version travels with the document.
    /// </summary>
    public const int CurrentSchemaVersion = 2;

    public Guid Id { get; private set; }
    public Guid ProjectId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public ProjectStatus Status { get; private set; }
    public ProjectStage Stage { get; private set; }
    public int ProgressPercentage { get; private set; }
    public ProjectPriority Priority { get; private set; }
    public Guid OwnerId { get; private set; }
    public string OwnerName { get; private set; } = string.Empty;
    public Guid? SourceIdeaId { get; private set; }
    public Guid? LinkedGuidelineId { get; private set; }
    public decimal? EstimatedCost { get; private set; }
    public decimal? ActualCost { get; private set; }
    public DateTimeOffset? StartDate { get; private set; }
    public DateTimeOffset? Deadline { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public string? BlockedReason { get; private set; }
    public string? CancelledReason { get; private set; }
    public DateTimeOffset TakenAt { get; private set; }
    public string TriggerAction { get; private set; } = string.Empty;
    public Guid TriggeredByActorId { get; private set; }
    public int SchemaVersion { get; private set; }

    private ProjectSnapshot() { }

    public static ProjectSnapshot Create(
        Project project,
        string triggerAction,
        Guid triggeredByActorId)
    {
        ArgumentNullException.ThrowIfNull(project);
        if (string.IsNullOrWhiteSpace(triggerAction))
            throw new ArgumentException("Trigger action is required.", nameof(triggerAction));
        if (triggeredByActorId == Guid.Empty)
            throw new ArgumentException("A snapshot must record its actor.", nameof(triggeredByActorId));

        return new ProjectSnapshot
        {
            Id = Guid.NewGuid(),
            ProjectId = project.Id,
            Title = project.Title,
            Description = project.Description,
            Status = project.Status,
            Stage = project.Stage,
            ProgressPercentage = project.ProgressPercentage,
            Priority = project.Priority,
            OwnerId = project.OwnerId,
            OwnerName = project.OwnerName,
            SourceIdeaId = project.SourceIdeaId,
            LinkedGuidelineId = project.LinkedGuidelineId,
            EstimatedCost = project.EstimatedCost,
            ActualCost = project.ActualCost,
            StartDate = project.StartDate,
            Deadline = project.Deadline,
            CompletedAt = project.CompletedAt,
            BlockedReason = project.BlockedReason,
            CancelledReason = project.CancelledReason,
            TakenAt = DateTimeOffset.UtcNow,
            TriggerAction = triggerAction,
            TriggeredByActorId = triggeredByActorId,
            SchemaVersion = CurrentSchemaVersion
        };
    }
}
