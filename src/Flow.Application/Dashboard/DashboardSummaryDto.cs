namespace Flow.Application.Dashboard;

/// <summary>A named value in a distribution, ready to be drawn without further work.</summary>
public record DistributionSliceDto(string Label, int Count, double Percentage);

/// <summary>One point on a time series.</summary>
public record TrendPointDto(string Period, int Ideas, int Projects, int Completed);

public record BlockedProjectDto(
    Guid ProjectId,
    string Title,
    Guid OwnerId,
    string OwnerName,
    string Reason,
    int DaysBlocked);

public record RiskProjectDto(
    Guid ProjectId,
    string Title,
    Guid OwnerId,
    string OwnerName,
    string Status,
    string Stage,
    int ProgressPercentage,
    DateTimeOffset? Deadline,
    int DaysToDeadline,
    bool IsOverdue);

public record RankedIdeaDto(
    Guid IdeaId,
    string Title,
    string Status,
    int? Score,
    int? FlowScore,
    string SubmittedByName);

public record RankedProjectDto(
    Guid ProjectId,
    string Title,
    string Status,
    int ProgressPercentage,
    decimal? ActualRoi,
    decimal? NetValue);

public record StrategyPerformanceDto(
    Guid GuidelineId,
    string Title,
    string Category,
    string? Campaign,
    bool IsCurrent,
    int IdeaCount,
    int ProjectCount,
    int CompletedProjectCount,
    decimal? ActualNetValue);

public record CampaignPerformanceDto(
    string Campaign,
    int GuidelineCount,
    int IdeaCount,
    int ProjectCount,
    int CompletedProjectCount,
    decimal? ActualNetValue);

public record IdeaFunnelDto(
    int Total,
    int Draft,
    int UnderReview,
    int Approved,
    int Rejected,
    int ConvertedToProjects,
    double ApprovalRate,
    double ConversionRate);

public record ProjectHealthDto(
    int Total,
    int Planned,
    int InProgress,
    int Blocked,
    int Completed,
    int Cancelled,
    int Overdue,
    int AtRisk,
    double AverageProgress,
    double AverageCompletionDays,
    double AverageBlockedDays,
    double BottleneckIndex,
    IReadOnlyList<DistributionSliceDto> ByStatus,
    IReadOnlyList<DistributionSliceDto> ByStage);

public record FinancialOutcomeDto(
    decimal EstimatedRevenue,
    decimal EstimatedSavings,
    decimal EstimatedCost,
    decimal EstimatedNetValue,
    decimal? EstimatedRoiAverage,
    decimal ActualRevenue,
    decimal ActualSavings,
    decimal ActualCost,
    decimal ActualNetValue,
    decimal? ActualRoiAverage,
    double? AveragePaybackMonths,
    int ProjectsWithEstimated,
    int ProjectsWithActual);

public record ImpactOutcomeDto(
    decimal? AverageProductivityGainPercent,
    decimal TotalTimeSavedHours,
    decimal? AverageQualityGainPercent,
    int ProjectsReporting);

/// <summary>
/// Everything the executive dashboard needs, already aggregated and already shaped for
/// visualisation. The mobile client renders this; it does not recompute it.
/// </summary>
public record DashboardSummaryDto(
    IdeaFunnelDto Ideas,
    ProjectHealthDto Projects,
    FinancialOutcomeDto Financial,
    ImpactOutcomeDto Impact,
    IReadOnlyList<BlockedProjectDto> BlockedProjects,
    IReadOnlyList<RiskProjectDto> ProjectsAtRisk,
    IReadOnlyList<RankedIdeaDto> TopIdeas,
    IReadOnlyList<RankedProjectDto> TopProjects,
    IReadOnlyList<StrategyPerformanceDto> ByStrategy,
    IReadOnlyList<CampaignPerformanceDto> ByCampaign,
    IReadOnlyList<TrendPointDto> Trends,
    DateTimeOffset GeneratedAt);
