using System.Net;
using System.Net.Http.Json;
using Flow.Domain.Enums;
using Flow.Integration.Tests.Fixtures;
using FluentAssertions;

namespace Flow.Integration.Tests.Scenarios;

public class DashboardTests : IntegrationTestBase
{
    public DashboardTests(MongoFixture mongo) : base(mongo) { }

    private sealed record Created(Guid Id);
    private sealed record Slice(string Label, int Count, double Percentage);
    private sealed record Funnel(
        int Total, int Draft, int UnderReview, int Approved, int Rejected,
        int ConvertedToProjects, double ApprovalRate, double ConversionRate);
    private sealed record Health(
        int Total, int Planned, int InProgress, int Blocked, int Completed, int Cancelled,
        int Overdue, int AtRisk, double AverageProgress, double AverageCompletionDays,
        double AverageBlockedDays, double BottleneckIndex,
        List<Slice> ByStatus, List<Slice> ByStage);
    private sealed record Financial(
        decimal EstimatedNetValue, decimal ActualNetValue,
        int ProjectsWithEstimated, int ProjectsWithActual);
    private sealed record Impact(
        decimal? AverageProductivityGainPercent, decimal TotalTimeSavedHours,
        decimal? AverageQualityGainPercent, int ProjectsReporting);
    private sealed record Blocked(Guid ProjectId, string Title, string Reason, int DaysBlocked);
    private sealed record Trend(string Period, int Ideas, int Projects, int Completed);
    private sealed record StrategyRow(
        Guid GuidelineId, string Title, string Category, string? Campaign,
        bool IsCurrent, int IdeaCount, int ProjectCount);
    private sealed record CampaignRow(string Campaign, int IdeaCount, int ProjectCount);
    private sealed record Summary(
        Funnel Ideas, Health Projects, Financial Financial, Impact Impact,
        List<Blocked> BlockedProjects, List<object> ProjectsAtRisk,
        List<object> TopIdeas, List<object> TopProjects,
        List<StrategyRow> ByStrategy, List<CampaignRow> ByCampaign,
        List<Trend> Trends, DateTimeOffset GeneratedAt);

    [Fact]
    public async Task Summary_OnAnEmptyDatabase_ReturnsZeroesRatherThanFailing()
    {
        RequireDatabase();
        var leadership = await Factory.CreateClientAsAsync(UserRole.Leadership);

        var summary = await leadership.GetFromJsonAsync<Summary>("/api/v1/dashboard/summary");

        summary.Should().NotBeNull();
        summary!.Ideas.Total.Should().Be(0);
        summary.Ideas.ApprovalRate.Should().Be(0, because: "no divide-by-zero on an empty base");
        summary.Ideas.ConversionRate.Should().Be(0);
        summary.Projects.Total.Should().Be(0);
        summary.Projects.BottleneckIndex.Should().Be(0);
        summary.Financial.ActualNetValue.Should().Be(0);
        summary.BlockedProjects.Should().BeEmpty();
        summary.ByStrategy.Should().BeEmpty();
        summary.Trends.Should().NotBeNull();
    }

    [Fact]
    public async Task Summary_OnARealisticDataset_ReportsTheWholePicture()
    {
        RequireDatabase();

        var leadership = await Factory.CreateClientAsAsync(UserRole.Leadership);
        var operatorClient = await Factory.CreateClientAsAsync(UserRole.Operator);
        var (manager, managerId) = await Factory.CreateClientWithUserAsync(UserRole.Manager);

        // A strategy under a campaign, so the strategy and campaign views have content.
        var guideline = await leadership.PostAsJsonAsync("/api/v1/guidelines", new
        {
            title = "Reduzir retrabalho",
            description = "Diretriz de eficiência operacional",
            category = nameof(GuidelineCategory.OperationalEfficiency),
            campaign = "Onda Operacional",
            validFrom = DateTimeOffset.UtcNow.AddDays(-30),
            validUntil = (DateTimeOffset?)null
        });
        var guidelineId = (await guideline.Content.ReadFromJsonAsync<Created>())!.Id;

        // Idea 1: approved and converted, then completed with a measured result.
        var approvedId = await SubmitIdeaAsync(operatorClient, guidelineId, "Ideia aprovada");
        await manager.PostAsJsonAsync($"/api/v1/ideas/{approvedId}/approve", new { managerComment = "ok" });

        var convert = await manager.PostAsJsonAsync($"/api/v1/ideas/{approvedId}/convert", new
        {
            title = "Projeto entregue",
            description = "Descrição",
            priority = nameof(ProjectPriority.High),
            ownerId = managerId,
            estimatedCost = 100_000m,
            deadline = DateTimeOffset.UtcNow.AddDays(30),
            assistantRunId = (Guid?)null
        });
        var completedProject = (await convert.Content.ReadFromJsonAsync<Created>())!.Id;

        await manager.PostAsync($"/api/v1/projects/{completedProject}/start", null);
        await manager.PostAsync($"/api/v1/projects/{completedProject}/complete", null);

        await manager.PutAsJsonAsync($"/api/v1/projects/{completedProject}/result", new
        {
            estimatedRevenue = 0m, estimatedSavings = 150_000m, estimatedCost = 100_000m,
            actualRevenue = 0m, actualSavings = 143_000m, actualCost = 79_400m,
            paybackPeriodMonths = 7,
            productivityGainPercent = 4.5m, timeSavedHours = 180m, qualityGainPercent = 22m,
            notes = "Entregue"
        });

        // Idea 2: rejected, so the funnel shows a real decision both ways.
        var rejectedId = await SubmitIdeaAsync(operatorClient, guidelineId, "Ideia rejeitada");
        await manager.PostAsJsonAsync($"/api/v1/ideas/{rejectedId}/reject",
            new { managerComment = "Fora de escopo" });

        // Idea 3: still waiting in the queue.
        await SubmitIdeaAsync(operatorClient, guidelineId, "Ideia na fila");

        // A blocked project, which is the bottleneck signal.
        var blocked = await manager.PostAsJsonAsync("/api/v1/projects", new
        {
            title = "Projeto bloqueado",
            description = "Descrição",
            priority = nameof(ProjectPriority.Medium),
            ownerId = managerId,
            linkedGuidelineId = guidelineId,
            estimatedCost = 20_000m,
            deadline = DateTimeOffset.UtcNow.AddDays(5)
        });
        var blockedProject = (await blocked.Content.ReadFromJsonAsync<Created>())!.Id;

        await manager.PostAsync($"/api/v1/projects/{blockedProject}/start", null);
        await manager.PostAsJsonAsync($"/api/v1/projects/{blockedProject}/block",
            new { reason = "Fornecedor atrasou" });

        var summary = await leadership.GetFromJsonAsync<Summary>("/api/v1/dashboard/summary");

        // ── Funnel ──
        summary!.Ideas.Total.Should().Be(3);
        summary.Ideas.Approved.Should().Be(1);
        summary.Ideas.Rejected.Should().Be(1);
        summary.Ideas.UnderReview.Should().Be(1);
        summary.Ideas.ConvertedToProjects.Should().Be(1);
        summary.Ideas.ApprovalRate.Should().Be(50, because: "one of two decided ideas was approved");
        summary.Ideas.ConversionRate.Should().Be(100);

        // ── Project health ──
        summary.Projects.Total.Should().Be(2);
        summary.Projects.Completed.Should().Be(1);
        summary.Projects.Blocked.Should().Be(1);
        summary.Projects.BottleneckIndex.Should().Be(100,
            because: "the only project still in flight is blocked");
        summary.Projects.ByStatus.Should().HaveCount(2);
        summary.Projects.ByStage.Should().NotBeEmpty();

        // ── Money and impact ──
        summary.Financial.EstimatedNetValue.Should().Be(50_000m);
        summary.Financial.ActualNetValue.Should().Be(63_600m);
        summary.Financial.ProjectsWithActual.Should().Be(1);
        summary.Impact.TotalTimeSavedHours.Should().Be(180m);
        summary.Impact.AverageProductivityGainPercent.Should().Be(4.5m);

        // ── Blockers ──
        summary.BlockedProjects.Should().ContainSingle()
            .Which.Reason.Should().Be("Fornecedor atrasou");

        // ── Risk ──
        summary.Projects.AtRisk.Should().Be(1,
            because: "the blocked project has a deadline five days away");

        // ── Strategy and campaign roll-ups ──
        summary.ByStrategy.Should().ContainSingle().Which.Should().Match<StrategyRow>(s =>
            s.GuidelineId == guidelineId && s.IdeaCount == 3 && s.ProjectCount == 2 && s.IsCurrent);

        summary.ByCampaign.Should().ContainSingle()
            .Which.Campaign.Should().Be("Onda Operacional");

        // ── Trends ──
        summary.Trends.Should().NotBeEmpty();
        summary.Trends.Sum(t => t.Ideas).Should().Be(3);
    }

    [Fact]
    public async Task ProjectDashboard_ExposesGovernanceCounts()
    {
        RequireDatabase();
        var (manager, managerId) = await Factory.CreateClientWithUserAsync(UserRole.Manager);

        var created = await manager.PostAsJsonAsync("/api/v1/projects", new
        {
            title = "Projeto detalhado",
            description = "Descrição",
            priority = nameof(ProjectPriority.Low),
            ownerId = managerId,
            linkedGuidelineId = (Guid?)null,
            estimatedCost = (decimal?)null,
            deadline = (DateTimeOffset?)null
        });
        var projectId = (await created.Content.ReadFromJsonAsync<Created>())!.Id;

        await manager.PostAsync($"/api/v1/projects/{projectId}/start", null);
        await manager.PostAsJsonAsync($"/api/v1/projects/{projectId}/block",
            new { reason = "Aguardando peça" });
        await manager.PostAsync($"/api/v1/projects/{projectId}/unblock", null);

        var detail = await manager.GetFromJsonAsync<ProjectDashboard>(
            $"/api/v1/dashboard/projects/{projectId}");

        detail!.TimesBlocked.Should().Be(1);
        detail.SnapshotCount.Should().Be(4, because: "created, started, blocked and unblocked");
        detail.AuditEntryCount.Should().Be(4);
    }

    [Fact]
    public async Task StrategyDashboard_RollsUpItsIdeasAndProjects()
    {
        RequireDatabase();
        var leadership = await Factory.CreateClientAsAsync(UserRole.Leadership);
        var operatorClient = await Factory.CreateClientAsAsync(UserRole.Operator);

        var guideline = await leadership.PostAsJsonAsync("/api/v1/guidelines", new
        {
            title = "Diretriz com métricas",
            description = "Descrição",
            category = nameof(GuidelineCategory.Safety),
            campaign = (string?)null,
            validFrom = DateTimeOffset.UtcNow.AddDays(-10),
            validUntil = (DateTimeOffset?)null
        });
        var guidelineId = (await guideline.Content.ReadFromJsonAsync<Created>())!.Id;

        await SubmitIdeaAsync(operatorClient, guidelineId, "Ideia vinculada");

        var detail = await leadership.GetFromJsonAsync<StrategyDashboard>(
            $"/api/v1/dashboard/strategies/{guidelineId}");

        detail!.IdeaCount.Should().Be(1);
        detail.IsCurrent.Should().BeTrue();
        detail.IdeasByStatus.Should().ContainSingle()
            .Which.Label.Should().Be(nameof(IdeaStatus.UnderReview));
    }

    [Fact]
    public async Task ProjectDashboard_ForAnUnknownProject_Is404()
    {
        RequireDatabase();
        var leadership = await Factory.CreateClientAsAsync(UserRole.Leadership);

        var response = await leadership.GetAsync($"/api/v1/dashboard/projects/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private static async Task<Guid> SubmitIdeaAsync(HttpClient client, Guid guidelineId, string title)
    {
        var created = await client.PostAsJsonAsync("/api/v1/ideas", new
        {
            title,
            description = "Descrição da ideia",
            problem = "Descrição do problema",
            linkedGuidelineId = guidelineId
        });
        created.EnsureSuccessStatusCode();

        var id = (await created.Content.ReadFromJsonAsync<Created>())!.Id;
        (await client.PostAsync($"/api/v1/ideas/{id}/submit", null)).EnsureSuccessStatusCode();

        return id;
    }

    private sealed record ProjectDashboard(
        Guid ProjectId, string Title, int TimesBlocked, double TotalDaysBlocked,
        int SnapshotCount, int AuditEntryCount);

    private sealed record StrategyDashboard(
        Guid GuidelineId, string Title, bool IsCurrent,
        int IdeaCount, List<Slice> IdeasByStatus, int ProjectCount, List<Slice> ProjectsByStatus);
}
