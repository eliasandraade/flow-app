using Flow.Domain.Entities;

namespace Flow.Application.Projects;

public record ProjectDetailDto(
    Guid Id,
    string Title,
    string Description,
    string Status,
    string Stage,
    int ProgressPercentage,
    string Priority,
    Guid OwnerId,
    string OwnerName,
    Guid? SourceIdeaId,
    string? SourceIdeaTitle,
    Guid? LinkedGuidelineId,
    string? LinkedGuidelineTitle,
    decimal? EstimatedCost,
    decimal? ActualCost,
    DateTimeOffset? StartDate,
    DateTimeOffset? Deadline,
    DateTimeOffset? CompletedAt,
    string? BlockedReason,
    DateTimeOffset? BlockedSince,
    int? DaysBlocked,
    string? CancelledReason,
    bool IsOverdue,
    bool IsAtRisk,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    public static ProjectDetailDto From(
        Project p, string? sourceIdeaTitle, string? guidelineTitle, DateTimeOffset at) => new(
        p.Id, p.Title, p.Description, p.Status.ToString(), p.Stage.ToString(), p.ProgressPercentage,
        p.Priority.ToString(), p.OwnerId, p.OwnerName,
        p.SourceIdeaId, sourceIdeaTitle, p.LinkedGuidelineId, guidelineTitle,
        p.EstimatedCost, p.ActualCost, p.StartDate, p.Deadline, p.CompletedAt,
        p.BlockedReason, p.BlockedSince,
        p.BlockedSince is null ? null : (int)(at - p.BlockedSince.Value).TotalDays,
        p.CancelledReason, p.IsOverdueAt(at), p.IsAtRiskAt(at), p.CreatedAt, p.UpdatedAt);
}
