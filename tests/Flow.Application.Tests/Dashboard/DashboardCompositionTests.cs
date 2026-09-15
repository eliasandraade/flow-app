using Flow.Application.Common.Persistence;
using Flow.Application.Dashboard;
using Flow.Application.Dashboard.Queries.GetDashboardSummary;
using Flow.Application.Guidelines;
using Flow.Domain.Entities;
using FluentAssertions;
using Moq;

namespace Flow.Application.Tests.Dashboard;

/// <summary>
/// Covers the arithmetic the dashboard handler performs on top of the aggregation results.
///
/// These are the cases that are awkward to force through a real database — an empty base,
/// a base where every decision went one way, a division that would blow up — and where a
/// silent zero or a NaN would quietly mislead an executive reading the screen.
/// </summary>
public class DashboardCompositionTests
{
    private readonly Mock<IDashboardReadRepository> _dashboard = new();
    private readonly Mock<IGuidelineRepository> _guidelines = new();

    private GetDashboardSummaryQueryHandler CreateHandler() =>
        new(_dashboard.Object, _guidelines.Object);

    private void Setup(
        IdeaAggregates? ideas = null,
        ProjectAggregates? projects = null,
        ResultAggregates? results = null,
        IReadOnlyList<PeriodCounts>? trends = null,
        IReadOnlyList<RankedProjectRow>? topProjects = null)
    {
        _dashboard.Setup(d => d.GetIdeaAggregatesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(ideas ?? EmptyIdeas);
        _dashboard.Setup(d => d.GetProjectAggregatesAsync(
                It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(projects ?? EmptyProjects);
        _dashboard.Setup(d => d.GetResultAggregatesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(results ?? EmptyResults);
        _dashboard.Setup(d => d.GetMonthlyTrendsAsync(
                It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(trends ?? []);
        _dashboard.Setup(d => d.GetTopProjectsByRealisedValueAsync(
                It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(topProjects ?? []);
        _guidelines.Setup(g => g.GetByIdsAsync(
                It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
    }

    private static IdeaAggregates EmptyIdeas => new(0, [], [], [], new Dictionary<Guid, int>());

    private static ProjectAggregates EmptyProjects => new(
        0, [], [], 0, 0, 0, [], [], [], new Dictionary<Guid, ProjectGuidelineCounts>());

    private static ResultAggregates EmptyResults => new(
        0, 0, 0, null, 0, 0, 0, 0, null, 0, null, null, 0, null, 0,
        new Dictionary<Guid, decimal>());

    [Fact]
    public async Task EmptyDatabase_ProducesZeroesAndNoDivisionByZero()
    {
        Setup();

        var summary = await CreateHandler().Handle(new GetDashboardSummaryQuery(), default);

        summary.Ideas.ApprovalRate.Should().Be(0);
        summary.Ideas.ConversionRate.Should().Be(0);
        summary.Projects.BottleneckIndex.Should().Be(0);
        summary.Projects.AverageBlockedDays.Should().Be(0);
        summary.Financial.EstimatedNetValue.Should().Be(0);
        summary.ByStrategy.Should().BeEmpty();
        summary.ByCampaign.Should().BeEmpty();
    }

    [Fact]
    public async Task ApprovalRate_IgnoresIdeasStillAwaitingADecision()
    {
        Setup(ideas: new IdeaAggregates(
            Total: 10,
            ByStatus:
            [
                new StatusCount("Draft", 4),
                new StatusCount("UnderReview", 2),
                new StatusCount("Approved", 3),
                new StatusCount("Rejected", 1)
            ],
            ByPriority: [],
            TopByFlowScore: [],
            CountByGuideline: new Dictionary<Guid, int>()));

        var summary = await CreateHandler().Handle(new GetDashboardSummaryQuery(), default);

        // 3 approved out of 4 decided, not out of 10 submitted.
        summary.Ideas.ApprovalRate.Should().Be(75);
        summary.Ideas.Draft.Should().Be(4);
    }

    [Fact]
    public async Task ConversionRate_IsMeasuredAgainstApprovedIdeasOnly()
    {
        Setup(
            ideas: new IdeaAggregates(
                8, [new StatusCount("Approved", 4)], [], [], new Dictionary<Guid, int>()),
            projects: new ProjectAggregates(
                3, [], [], ConvertedFromIdeas: 3, 0, 0, [], [], [],
                new Dictionary<Guid, ProjectGuidelineCounts>()));

        var summary = await CreateHandler().Handle(new GetDashboardSummaryQuery(), default);

        summary.Ideas.ConversionRate.Should().Be(75);
    }

    [Fact]
    public async Task BottleneckIndex_ExcludesFinishedAndCancelledWork()
    {
        Setup(projects: new ProjectAggregates(
            Total: 10,
            ByStatus:
            [
                new StatusCount("InProgress", 3),
                new StatusCount("Blocked", 1),
                new StatusCount("Completed", 5),
                new StatusCount("Cancelled", 1)
            ],
            ByStage: [],
            ConvertedFromIdeas: 0,
            AverageProgress: 0,
            AverageCompletionDays: 0,
            Blocked: [],
            AtRisk: [],
            Overdue: [],
            ByGuideline: new Dictionary<Guid, ProjectGuidelineCounts>()));

        var summary = await CreateHandler().Handle(new GetDashboardSummaryQuery(), default);

        // 1 blocked out of 4 in flight, not out of 10 total.
        summary.Projects.BottleneckIndex.Should().Be(25);
    }

    [Fact]
    public async Task BottleneckIndex_IsZeroWhenNothingIsInFlight()
    {
        Setup(projects: new ProjectAggregates(
            5, [new StatusCount("Completed", 5)], [], 0, 0, 0, [], [], [],
            new Dictionary<Guid, ProjectGuidelineCounts>()));

        var summary = await CreateHandler().Handle(new GetDashboardSummaryQuery(), default);

        summary.Projects.BottleneckIndex.Should().Be(0);
    }

    [Fact]
    public async Task NetValue_IsRevenuePlusSavingsMinusCost()
    {
        Setup(results: new ResultAggregates(
            EstimatedRevenue: 100_000m, EstimatedSavings: 50_000m, EstimatedCost: 30_000m,
            EstimatedRoiAverage: 400m, ProjectsWithEstimated: 2,
            ActualRevenue: 90_000m, ActualSavings: 40_000m, ActualCost: 35_000m,
            ActualRoiAverage: 271m, ProjectsWithActual: 2,
            AveragePaybackMonths: 8, AverageProductivityGainPercent: 5m,
            TotalTimeSavedHours: 100m, AverageQualityGainPercent: 3m,
            ProjectsReportingImpact: 2,
            ActualNetValueByProject: new Dictionary<Guid, decimal>()));

        var summary = await CreateHandler().Handle(new GetDashboardSummaryQuery(), default);

        summary.Financial.EstimatedNetValue.Should().Be(120_000m);
        summary.Financial.ActualNetValue.Should().Be(95_000m);
    }

    [Fact]
    public async Task BlockedProjects_AreOrderedByHowLongTheyHaveBeenStuck()
    {
        var now = DateTimeOffset.UtcNow;

        Setup(projects: new ProjectAggregates(
            3, [new StatusCount("Blocked", 3)], [], 0, 0, 0,
            Blocked:
            [
                new BlockedProjectRow(Guid.NewGuid(), "Recente", Guid.NewGuid(), "Ana", "r1", now.AddDays(-2)),
                new BlockedProjectRow(Guid.NewGuid(), "Antigo", Guid.NewGuid(), "Ana", "r2", now.AddDays(-30)),
                new BlockedProjectRow(Guid.NewGuid(), "Médio", Guid.NewGuid(), "Ana", "r3", now.AddDays(-10))
            ],
            AtRisk: [], Overdue: [],
            ByGuideline: new Dictionary<Guid, ProjectGuidelineCounts>()));

        var summary = await CreateHandler().Handle(new GetDashboardSummaryQuery(), default);

        summary.BlockedProjects.Select(b => b.Title)
            .Should().ContainInOrder("Antigo", "Médio", "Recente");

        summary.Projects.AverageBlockedDays.Should().BeApproximately(14, 1);
    }

    [Fact]
    public async Task BlockedProject_WithoutAKnownStartDate_ReportsZeroRatherThanACrash()
    {
        Setup(projects: new ProjectAggregates(
            1, [new StatusCount("Blocked", 1)], [], 0, 0, 0,
            Blocked: [new BlockedProjectRow(
                Guid.NewGuid(), "Sem data", Guid.NewGuid(), "Ana", "motivo", BlockedSince: null)],
            AtRisk: [], Overdue: [],
            ByGuideline: new Dictionary<Guid, ProjectGuidelineCounts>()));

        var summary = await CreateHandler().Handle(new GetDashboardSummaryQuery(), default);

        summary.BlockedProjects.Single().DaysBlocked.Should().Be(0);
    }

    [Fact]
    public async Task OverdueProjectsAreListedBeforeMerelyAtRiskOnes()
    {
        var now = DateTimeOffset.UtcNow;

        Setup(projects: new ProjectAggregates(
            2, [], [], 0, 0, 0, Blocked: [],
            AtRisk:
            [
                new RiskProjectRow(Guid.NewGuid(), "Em risco", Guid.NewGuid(), "Ana",
                    "InProgress", "Execution", 30, now.AddDays(10), IsOverdue: false)
            ],
            Overdue:
            [
                new RiskProjectRow(Guid.NewGuid(), "Atrasado", Guid.NewGuid(), "Ana",
                    "InProgress", "Execution", 40, now.AddDays(-5), IsOverdue: true)
            ],
            ByGuideline: new Dictionary<Guid, ProjectGuidelineCounts>()));

        var summary = await CreateHandler().Handle(new GetDashboardSummaryQuery(), default);

        summary.ProjectsAtRisk.Should().HaveCount(2);
        summary.ProjectsAtRisk.First().Title.Should().Be("Atrasado",
            because: "the ones already late need attention before the ones merely heading that way");
        summary.ProjectsAtRisk.First().IsOverdue.Should().BeTrue();
    }

    [Fact]
    public async Task StrategyRollUp_GroupsIntoCampaigns()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();

        var guidelines = new[]
        {
            StrategicGuidelineFor(first, "Diretriz A", "Onda 2026"),
            StrategicGuidelineFor(second, "Diretriz B", "Onda 2026")
        };

        Setup(
            ideas: new IdeaAggregates(5, [], [], [],
                new Dictionary<Guid, int> { [first] = 3, [second] = 2 }),
            projects: new ProjectAggregates(3, [], [], 0, 0, 0, [], [], [],
                new Dictionary<Guid, ProjectGuidelineCounts>
                {
                    [first] = new(2, 1),
                    [second] = new(1, 0)
                }));

        _guidelines.Setup(g => g.GetByIdsAsync(
                It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(guidelines);

        var summary = await CreateHandler().Handle(new GetDashboardSummaryQuery(), default);

        summary.ByStrategy.Should().HaveCount(2);
        summary.ByStrategy.First().ProjectCount.Should().Be(2,
            because: "strategies are ordered by how much work they actually attracted");

        summary.ByCampaign.Should().ContainSingle().Which.Should().Match<CampaignPerformanceDto>(c =>
            c.Campaign == "Onda 2026" && c.IdeaCount == 5 && c.ProjectCount == 3 &&
            c.CompletedProjectCount == 1 && c.GuidelineCount == 2);
    }

    private static StrategicGuideline StrategicGuidelineFor(Guid id, string title, string campaign)
    {
        var guideline = StrategicGuideline.Create(
            title, "Descrição", Domain.Enums.GuidelineCategory.OperationalEfficiency,
            campaign, DateTimeOffset.UtcNow.AddDays(-10), null, Guid.NewGuid());

        // The identifier is assigned by the domain, so the test maps its own key onto it.
        typeof(Domain.Common.BaseEntity)
            .GetProperty(nameof(StrategicGuideline.Id))!
            .SetValue(guideline, id);

        return guideline;
    }
}
