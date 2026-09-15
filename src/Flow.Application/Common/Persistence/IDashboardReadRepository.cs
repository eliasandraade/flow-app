namespace Flow.Application.Common.Persistence;

/// <summary>
/// Read-side aggregation for the dashboard.
///
/// Deliberately coarse-grained: each method is one round trip that returns a whole facet
/// of the picture. The previous implementation issued roughly eight sequential queries for
/// a much smaller dashboard, and that pattern gets worse with every metric added.
/// </summary>
public interface IDashboardReadRepository
{
    Task<IdeaAggregates> GetIdeaAggregatesAsync(CancellationToken cancellationToken = default);

    Task<ProjectAggregates> GetProjectAggregatesAsync(
        DateTimeOffset at, CancellationToken cancellationToken = default);

    Task<ResultAggregates> GetResultAggregatesAsync(CancellationToken cancellationToken = default);

    /// <summary>Monthly counts for the trend chart, oldest first.</summary>
    Task<IReadOnlyList<PeriodCounts>> GetMonthlyTrendsAsync(
        DateTimeOffset from, CancellationToken cancellationToken = default);

    /// <summary>
    /// Projects ranked by realised net value. Resolved against the projects collection so
    /// that completed work — which is most of what ranks here — carries its real title.
    /// </summary>
    Task<IReadOnlyList<RankedProjectRow>> GetTopProjectsByRealisedValueAsync(
        int take, CancellationToken cancellationToken = default);

    Task<ProjectDetailAggregates> GetProjectDetailAggregatesAsync(
        Guid projectId, CancellationToken cancellationToken = default);

    Task<StrategyDetailAggregates> GetStrategyDetailAggregatesAsync(
        Guid guidelineId, CancellationToken cancellationToken = default);
}

public sealed record StatusCount(string Status, int Count);

public sealed record IdeaAggregates(
    int Total,
    IReadOnlyList<StatusCount> ByStatus,
    IReadOnlyList<StatusCount> ByPriority,
    IReadOnlyList<RankedIdeaRow> TopByFlowScore,
    IReadOnlyDictionary<Guid, int> CountByGuideline);

public sealed record RankedIdeaRow(
    Guid Id, string Title, string Status, int? Score, int? FlowScore, string SubmittedByName);

public sealed record BlockedProjectRow(
    Guid Id, string Title, Guid OwnerId, string OwnerName, string Reason, DateTimeOffset? BlockedSince);

public sealed record RiskProjectRow(
    Guid Id, string Title, Guid OwnerId, string OwnerName, string Status, string Stage,
    int ProgressPercentage, DateTimeOffset? Deadline, bool IsOverdue);

public sealed record ProjectAggregates(
    int Total,
    IReadOnlyList<StatusCount> ByStatus,
    IReadOnlyList<StatusCount> ByStage,
    int ConvertedFromIdeas,
    double AverageProgress,
    double AverageCompletionDays,
    IReadOnlyList<BlockedProjectRow> Blocked,
    IReadOnlyList<RiskProjectRow> AtRisk,
    IReadOnlyList<RiskProjectRow> Overdue,
    IReadOnlyDictionary<Guid, ProjectGuidelineCounts> ByGuideline);

public sealed record ProjectGuidelineCounts(int Total, int Completed);

public sealed record ResultAggregates(
    decimal EstimatedRevenue,
    decimal EstimatedSavings,
    decimal EstimatedCost,
    decimal? EstimatedRoiAverage,
    int ProjectsWithEstimated,
    decimal ActualRevenue,
    decimal ActualSavings,
    decimal ActualCost,
    decimal? ActualRoiAverage,
    int ProjectsWithActual,
    double? AveragePaybackMonths,
    decimal? AverageProductivityGainPercent,
    decimal TotalTimeSavedHours,
    decimal? AverageQualityGainPercent,
    int ProjectsReportingImpact,
    IReadOnlyDictionary<Guid, decimal> ActualNetValueByProject);

public sealed record PeriodCounts(string Period, int Ideas, int Projects, int Completed);

public sealed record RankedProjectRow(
    Guid Id, string Title, string Status, int ProgressPercentage, decimal? ActualRoi, decimal NetValue);

public sealed record ProjectDetailAggregates(
    int SnapshotCount,
    int AuditEntryCount,
    int TimesBlocked,
    double TotalDaysBlocked);

public sealed record StrategyDetailAggregates(
    int IdeaCount,
    IReadOnlyList<StatusCount> IdeasByStatus,
    int ProjectCount,
    IReadOnlyList<StatusCount> ProjectsByStatus,
    decimal ActualNetValue,
    decimal EstimatedNetValue);
