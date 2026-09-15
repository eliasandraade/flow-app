using Flow.Domain.Entities;

namespace Flow.Application.Projects;

public record ProjectSummaryDto(
    Guid Id,
    string Title,
    string Status,
    string Stage,
    int ProgressPercentage,
    string Priority,
    Guid OwnerId,
    string OwnerName,
    Guid? SourceIdeaId,
    Guid? LinkedGuidelineId,
    DateTimeOffset? Deadline,
    string? BlockedReason,
    bool IsOverdue,
    bool IsAtRisk,
    DateTimeOffset CreatedAt)
{
    public static ProjectSummaryDto From(Project p, DateTimeOffset at) => new(
        p.Id, p.Title, p.Status.ToString(), p.Stage.ToString(), p.ProgressPercentage,
        p.Priority.ToString(), p.OwnerId, p.OwnerName, p.SourceIdeaId, p.LinkedGuidelineId,
        p.Deadline, p.BlockedReason, p.IsOverdueAt(at), p.IsAtRiskAt(at), p.CreatedAt);
}
