using MediatR;

namespace Flow.Application.Dashboard.Queries.GetProjectDashboard;

public record GetProjectDashboardQuery(Guid ProjectId) : IRequest<ProjectDashboardDto>;

public record ProjectDashboardDto(
    Guid ProjectId,
    string Title,
    string Status,
    string Stage,
    int ProgressPercentage,
    Guid OwnerId,
    string OwnerName,
    Guid? SourceIdeaId,
    string? SourceIdeaTitle,
    Guid? LinkedGuidelineId,
    string? LinkedGuidelineTitle,
    DateTimeOffset? Deadline,
    int? DaysToDeadline,
    bool IsOverdue,
    bool IsAtRisk,
    int TimesBlocked,
    double TotalDaysBlocked,
    int SnapshotCount,
    int AuditEntryCount,
    ResultSummaryDto? Result,
    DateTimeOffset GeneratedAt);

public record ResultSummaryDto(
    decimal? EstimatedNetValue,
    decimal? EstimatedRoi,
    decimal? ActualNetValue,
    decimal? ActualRoi,
    int? PaybackPeriodMonths,
    decimal? ProductivityGainPercent,
    decimal? TimeSavedHours,
    decimal? QualityGainPercent);
