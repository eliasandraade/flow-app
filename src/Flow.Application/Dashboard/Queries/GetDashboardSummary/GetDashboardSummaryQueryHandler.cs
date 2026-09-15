using Flow.Application.Common.Persistence;
using Flow.Domain.Entities;
using MediatR;

namespace Flow.Application.Dashboard.Queries.GetDashboardSummary;

public class GetDashboardSummaryQueryHandler
    : IRequestHandler<GetDashboardSummaryQuery, DashboardSummaryDto>
{
    private const int TrendMonths = 6;
    private const int TopN = 5;

    private readonly IDashboardReadRepository _dashboard;
    private readonly IGuidelineRepository _guidelines;

    public GetDashboardSummaryQueryHandler(
        IDashboardReadRepository dashboard, IGuidelineRepository guidelines)
    {
        _dashboard = dashboard;
        _guidelines = guidelines;
    }

    public async Task<DashboardSummaryDto> Handle(
        GetDashboardSummaryQuery request, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;

        // Four aggregation round trips for the whole dashboard, one per collection.
        var ideaAgg = await _dashboard.GetIdeaAggregatesAsync(cancellationToken);
        var projectAgg = await _dashboard.GetProjectAggregatesAsync(now, cancellationToken);
        var resultAgg = await _dashboard.GetResultAggregatesAsync(cancellationToken);
        var trends = await _dashboard.GetMonthlyTrendsAsync(
            now.AddMonths(-TrendMonths), cancellationToken);
        var topProjects = await _dashboard.GetTopProjectsByRealisedValueAsync(TopN, cancellationToken);

        var ideas = BuildFunnel(ideaAgg, projectAgg);
        var projects = BuildHealth(projectAgg, now);
        var financial = BuildFinancial(resultAgg);
        var impact = new ImpactOutcomeDto(
            resultAgg.AverageProductivityGainPercent,
            resultAgg.TotalTimeSavedHours,
            resultAgg.AverageQualityGainPercent,
            resultAgg.ProjectsReportingImpact);

        var (byStrategy, byCampaign) = await BuildStrategyViewsAsync(
            ideaAgg, projectAgg, resultAgg, now, cancellationToken);

        return new DashboardSummaryDto(
            Ideas: ideas,
            Projects: projects,
            Financial: financial,
            Impact: impact,
            BlockedProjects: projectAgg.Blocked
                .Select(b => new BlockedProjectDto(
                    b.Id, b.Title, b.OwnerId, b.OwnerName, b.Reason,
                    b.BlockedSince is null ? 0 : (int)(now - b.BlockedSince.Value).TotalDays))
                .OrderByDescending(b => b.DaysBlocked)
                .ToList(),
            ProjectsAtRisk: projectAgg.Overdue.Concat(projectAgg.AtRisk)
                .Select(r => ToRiskDto(r, now))
                .OrderBy(r => r.DaysToDeadline)
                .ToList(),
            TopIdeas: ideaAgg.TopByFlowScore
                .Take(TopN)
                .Select(i => new RankedIdeaDto(i.Id, i.Title, i.Status, i.Score, i.FlowScore, i.SubmittedByName))
                .ToList(),
            TopProjects: topProjects
                .Select(p => new RankedProjectDto(
                    p.Id, p.Title, p.Status, p.ProgressPercentage, p.ActualRoi, p.NetValue))
                .ToList(),
            ByStrategy: byStrategy,
            ByCampaign: byCampaign,
            Trends: trends
                .Select(t => new TrendPointDto(t.Period, t.Ideas, t.Projects, t.Completed))
                .ToList(),
            GeneratedAt: now);
    }

    private static IdeaFunnelDto BuildFunnel(IdeaAggregates ideas, ProjectAggregates projects)
    {
        var draft = CountOf(ideas.ByStatus, "Draft");
        var underReview = CountOf(ideas.ByStatus, "UnderReview");
        var approved = CountOf(ideas.ByStatus, "Approved");
        var rejected = CountOf(ideas.ByStatus, "Rejected");
        var decided = approved + rejected;

        return new IdeaFunnelDto(
            Total: ideas.Total,
            Draft: draft,
            UnderReview: underReview,
            Approved: approved,
            Rejected: rejected,
            ConvertedToProjects: projects.ConvertedFromIdeas,
            ApprovalRate: Percentage(approved, decided),
            ConversionRate: Percentage(projects.ConvertedFromIdeas, approved));
    }

    private static ProjectHealthDto BuildHealth(ProjectAggregates p, DateTimeOffset now)
    {
        var planned = CountOf(p.ByStatus, "Planned");
        var inProgress = CountOf(p.ByStatus, "InProgress");
        var blocked = CountOf(p.ByStatus, "Blocked");
        var completed = CountOf(p.ByStatus, "Completed");
        var cancelled = CountOf(p.ByStatus, "Cancelled");

        var averageBlockedDays = p.Blocked.Count == 0
            ? 0.0
            : Math.Round(
                p.Blocked
                    .Where(b => b.BlockedSince is not null)
                    .Select(b => (now - b.BlockedSince!.Value).TotalDays)
                    .DefaultIfEmpty(0)
                    .Average(), 1);

        // Share of work in flight that is stuck. This is the product's headline
        // bottleneck signal, so it deliberately excludes finished and cancelled work.
        var active = inProgress + blocked;

        return new ProjectHealthDto(
            Total: p.Total,
            Planned: planned,
            InProgress: inProgress,
            Blocked: blocked,
            Completed: completed,
            Cancelled: cancelled,
            Overdue: p.Overdue.Count,
            AtRisk: p.AtRisk.Count,
            AverageProgress: Math.Round(p.AverageProgress, 1),
            AverageCompletionDays: Math.Round(p.AverageCompletionDays, 1),
            AverageBlockedDays: averageBlockedDays,
            BottleneckIndex: Percentage(blocked, active),
            ByStatus: ToDistribution(p.ByStatus, p.Total),
            ByStage: ToDistribution(p.ByStage, p.Total));
    }

    private static FinancialOutcomeDto BuildFinancial(ResultAggregates r) => new(
        EstimatedRevenue: r.EstimatedRevenue,
        EstimatedSavings: r.EstimatedSavings,
        EstimatedCost: r.EstimatedCost,
        EstimatedNetValue: r.EstimatedRevenue + r.EstimatedSavings - r.EstimatedCost,
        EstimatedRoiAverage: r.EstimatedRoiAverage,
        ActualRevenue: r.ActualRevenue,
        ActualSavings: r.ActualSavings,
        ActualCost: r.ActualCost,
        ActualNetValue: r.ActualRevenue + r.ActualSavings - r.ActualCost,
        ActualRoiAverage: r.ActualRoiAverage,
        AveragePaybackMonths: r.AveragePaybackMonths,
        ProjectsWithEstimated: r.ProjectsWithEstimated,
        ProjectsWithActual: r.ProjectsWithActual);

    private async Task<(IReadOnlyList<StrategyPerformanceDto>, IReadOnlyList<CampaignPerformanceDto>)>
        BuildStrategyViewsAsync(
            IdeaAggregates ideas,
            ProjectAggregates projects,
            ResultAggregates results,
            DateTimeOffset now,
            CancellationToken cancellationToken)
    {
        var guidelineIds = ideas.CountByGuideline.Keys
            .Union(projects.ByGuideline.Keys)
            .ToList();

        if (guidelineIds.Count == 0) return ([], []);

        // One batched lookup rather than a join per row.
        var guidelines = await _guidelines.GetByIdsAsync(guidelineIds, cancellationToken);

        var perStrategy = guidelines
            .Select(g =>
            {
                ideas.CountByGuideline.TryGetValue(g.Id, out var ideaCount);
                projects.ByGuideline.TryGetValue(g.Id, out var projectCounts);

                return new StrategyPerformanceDto(
                    g.Id, g.Title, g.Category.ToString(), g.Campaign, g.IsCurrentAt(now),
                    ideaCount,
                    projectCounts?.Total ?? 0,
                    projectCounts?.Completed ?? 0,
                    ActualNetValue: null);
            })
            .OrderByDescending(s => s.ProjectCount)
            .ThenByDescending(s => s.IdeaCount)
            .ToList();

        var perCampaign = perStrategy
            .Where(s => !string.IsNullOrWhiteSpace(s.Campaign))
            .GroupBy(s => s.Campaign!)
            .Select(g => new CampaignPerformanceDto(
                g.Key,
                GuidelineCount: g.Count(),
                IdeaCount: g.Sum(x => x.IdeaCount),
                ProjectCount: g.Sum(x => x.ProjectCount),
                CompletedProjectCount: g.Sum(x => x.CompletedProjectCount),
                ActualNetValue: null))
            .OrderByDescending(c => c.ProjectCount)
            .ToList();

        return (perStrategy, perCampaign);
    }

    private static RiskProjectDto ToRiskDto(RiskProjectRow r, DateTimeOffset now) => new(
        r.Id, r.Title, r.OwnerId, r.OwnerName, r.Status, r.Stage, r.ProgressPercentage,
        r.Deadline,
        r.Deadline is null ? int.MaxValue : (int)Math.Floor((r.Deadline.Value - now).TotalDays),
        r.IsOverdue);

    private static int CountOf(IReadOnlyList<StatusCount> counts, string status) =>
        counts.FirstOrDefault(c => c.Status == status)?.Count ?? 0;

    private static double Percentage(int part, int whole) =>
        whole <= 0 ? 0.0 : Math.Round((double)part / whole * 100, 1);

    private static IReadOnlyList<DistributionSliceDto> ToDistribution(
        IReadOnlyList<StatusCount> counts, int total) =>
        counts
            .Select(c => new DistributionSliceDto(c.Status, c.Count, Percentage(c.Count, total)))
            .OrderByDescending(s => s.Count)
            .ToList();
}
