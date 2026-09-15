using System.Net;
using System.Net.Http.Json;
using Flow.Domain.Enums;
using Flow.Integration.Tests.Fixtures;
using FluentAssertions;

namespace Flow.Integration.Tests.Scenarios;

public class ProjectAndResultTests : IntegrationTestBase
{
    public ProjectAndResultTests(MongoFixture mongo) : base(mongo) { }

    private sealed record Created(Guid Id);

    private sealed record ProjectDetail(
        Guid Id, string Status, string Stage, int ProgressPercentage,
        string? BlockedReason, int? DaysBlocked, bool IsOverdue, bool IsAtRisk);

    private sealed record Measurement(
        decimal? Revenue, decimal? Savings, decimal? Cost, decimal? Roi, decimal? NetValue);

    private sealed record ResultPayload(
        Guid ProjectId, Measurement? Estimated, Measurement? Actual,
        int? PaybackPeriodMonths, decimal? ProductivityGainPercent,
        decimal? TimeSavedHours, decimal? QualityGainPercent);

    private async Task<(HttpClient Manager, Guid ProjectId)> CreateProjectAsync(
        DateTimeOffset? deadline = null)
    {
        var (manager, managerId) = await Factory.CreateClientWithUserAsync(UserRole.Manager);

        var response = await manager.PostAsJsonAsync("/api/v1/projects", new
        {
            title = "Projeto de teste",
            description = "Descrição do projeto de teste",
            priority = nameof(ProjectPriority.High),
            ownerId = managerId,
            linkedGuidelineId = (Guid?)null,
            estimatedCost = 50_000m,
            deadline
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (manager, (await response.Content.ReadFromJsonAsync<Created>())!.Id);
    }

    // ─── State machine over HTTP ────────────────────────────────────────────

    [Fact]
    public async Task Project_WalksTheFullLifecycle()
    {
        RequireDatabase();
        var (manager, projectId) = await CreateProjectAsync();

        (await manager.PostAsync($"/api/v1/projects/{projectId}/start", null))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await manager.PatchAsJsonAsync($"/api/v1/projects/{projectId}/stage",
            new { stage = nameof(ProjectStage.Execution) }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await manager.PatchAsJsonAsync($"/api/v1/projects/{projectId}/progress",
            new { progressPercentage = 60 }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await manager.PostAsync($"/api/v1/projects/{projectId}/complete", null))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var detail = await manager.GetFromJsonAsync<ProjectDetail>($"/api/v1/projects/{projectId}");

        detail!.Status.Should().Be(nameof(ProjectStatus.Completed));
        detail.ProgressPercentage.Should().Be(100);
        detail.Stage.Should().Be(nameof(ProjectStage.Rollout));
    }

    [Fact]
    public async Task Project_InvalidTransition_Returns409()
    {
        RequireDatabase();
        var (manager, projectId) = await CreateProjectAsync();

        // Completing a project that never started is a state-machine violation.
        var response = await manager.PostAsync($"/api/v1/projects/{projectId}/complete", null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Project_BlockAndUnblock_TracksHowLongItWasStuck()
    {
        RequireDatabase();
        var (manager, projectId) = await CreateProjectAsync();
        await manager.PostAsync($"/api/v1/projects/{projectId}/start", null);

        await manager.PostAsJsonAsync($"/api/v1/projects/{projectId}/block",
            new { reason = "Fornecedor atrasou a entrega" });

        var blocked = await manager.GetFromJsonAsync<ProjectDetail>($"/api/v1/projects/{projectId}");
        blocked!.Status.Should().Be(nameof(ProjectStatus.Blocked));
        blocked.BlockedReason.Should().Be("Fornecedor atrasou a entrega");
        blocked.DaysBlocked.Should().NotBeNull();

        await manager.PostAsync($"/api/v1/projects/{projectId}/unblock", null);

        var unblocked = await manager.GetFromJsonAsync<ProjectDetail>($"/api/v1/projects/{projectId}");
        unblocked!.Status.Should().Be(nameof(ProjectStatus.InProgress));
        unblocked.BlockedReason.Should().BeNull();
        unblocked.DaysBlocked.Should().BeNull();
    }

    [Fact]
    public async Task Project_BlockingWithoutAReason_Returns422()
    {
        RequireDatabase();
        var (manager, projectId) = await CreateProjectAsync();
        await manager.PostAsync($"/api/v1/projects/{projectId}/start", null);

        var response = await manager.PostAsJsonAsync(
            $"/api/v1/projects/{projectId}/block", new { reason = "" });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Theory]
    [InlineData(-5)]
    [InlineData(101)]
    public async Task Project_ProgressOutOfRange_Returns422(int progress)
    {
        RequireDatabase();
        var (manager, projectId) = await CreateProjectAsync();
        await manager.PostAsync($"/api/v1/projects/{projectId}/start", null);

        var response = await manager.PatchAsJsonAsync(
            $"/api/v1/projects/{projectId}/progress", new { progressPercentage = progress });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Project_ProgressOf100_IsRefusedBecauseOnlyCompletionReachesIt()
    {
        RequireDatabase();
        var (manager, projectId) = await CreateProjectAsync();
        await manager.PostAsync($"/api/v1/projects/{projectId}/start", null);

        var response = await manager.PatchAsJsonAsync(
            $"/api/v1/projects/{projectId}/progress", new { progressPercentage = 100 });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Project_AtRisk_IsFlaggedWhenTheDeadlineIsCloseAndWorkIsBehind()
    {
        RequireDatabase();
        var (manager, projectId) = await CreateProjectAsync(
            deadline: DateTimeOffset.UtcNow.AddDays(4));

        await manager.PostAsync($"/api/v1/projects/{projectId}/start", null);
        await manager.PatchAsJsonAsync($"/api/v1/projects/{projectId}/progress",
            new { progressPercentage = 20 });

        var detail = await manager.GetFromJsonAsync<ProjectDetail>($"/api/v1/projects/{projectId}");

        detail!.IsAtRisk.Should().BeTrue();
        detail.IsOverdue.Should().BeFalse();
    }

    [Fact]
    public async Task Project_Overdue_IsFlaggedOnceThePastDeadlinePasses()
    {
        RequireDatabase();
        var (manager, projectId) = await CreateProjectAsync(
            deadline: DateTimeOffset.UtcNow.AddDays(-3));

        await manager.PostAsync($"/api/v1/projects/{projectId}/start", null);

        var detail = await manager.GetFromJsonAsync<ProjectDetail>($"/api/v1/projects/{projectId}");

        detail!.IsOverdue.Should().BeTrue();
    }

    [Fact]
    public async Task Project_WriteOperations_AreManagerOnly()
    {
        RequireDatabase();
        var (_, projectId) = await CreateProjectAsync();
        var operatorClient = await Factory.CreateClientAsAsync(UserRole.Operator);

        (await operatorClient.PostAsync($"/api/v1/projects/{projectId}/start", null))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Project_Snapshots_AreVisibleToManagementOnly()
    {
        RequireDatabase();
        var (manager, projectId) = await CreateProjectAsync();
        var operatorClient = await Factory.CreateClientAsAsync(UserRole.Operator);

        (await manager.GetAsync($"/api/v1/projects/{projectId}/snapshots"))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        (await operatorClient.GetAsync($"/api/v1/projects/{projectId}/snapshots"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ─── Results ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Result_KeepsEstimatedAndActualIndependent()
    {
        RequireDatabase();
        var (manager, projectId) = await CreateProjectAsync();

        var estimated = await manager.PutAsJsonAsync($"/api/v1/projects/{projectId}/result", new
        {
            estimatedRevenue = 0m,
            estimatedSavings = 210_000m,
            estimatedCost = 140_000m,
            actualRevenue = (decimal?)null,
            actualSavings = (decimal?)null,
            actualCost = (decimal?)null,
            paybackPeriodMonths = 10,
            productivityGainPercent = (decimal?)null,
            timeSavedHours = (decimal?)null,
            qualityGainPercent = (decimal?)null,
            notes = "Estimativa inicial"
        });
        estimated.StatusCode.Should().Be(HttpStatusCode.OK);

        // Writing the realised numbers must leave the estimate untouched.
        var actual = await manager.PutAsJsonAsync($"/api/v1/projects/{projectId}/result", new
        {
            estimatedRevenue = (decimal?)null,
            estimatedSavings = (decimal?)null,
            estimatedCost = (decimal?)null,
            actualRevenue = 0m,
            actualSavings = 196_500m,
            actualCost = 151_200m,
            paybackPeriodMonths = (int?)null,
            productivityGainPercent = 11.5m,
            timeSavedHours = 640m,
            qualityGainPercent = 18.5m,
            notes = (string?)null
        });
        actual.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await actual.Content.ReadFromJsonAsync<ResultPayload>();

        payload!.Estimated!.Savings.Should().Be(210_000m,
            because: "recording realised values must not overwrite the plan");
        payload.Estimated.Roi.Should().Be(50m);   // (0 + 210000 - 140000) / 140000 * 100
        payload.Actual!.Savings.Should().Be(196_500m);
        payload.Actual.NetValue.Should().Be(45_300m);
        payload.ProductivityGainPercent.Should().Be(11.5m);
        payload.TimeSavedHours.Should().Be(640m);
        payload.QualityGainPercent.Should().Be(18.5m);
        payload.PaybackPeriodMonths.Should().Be(10);
    }

    [Fact]
    public async Task Result_NegativeMoney_Returns422()
    {
        RequireDatabase();
        var (manager, projectId) = await CreateProjectAsync();

        var response = await manager.PutAsJsonAsync($"/api/v1/projects/{projectId}/result", new
        {
            estimatedRevenue = -1m,
            estimatedSavings = (decimal?)null,
            estimatedCost = (decimal?)null,
            actualRevenue = (decimal?)null,
            actualSavings = (decimal?)null,
            actualCost = (decimal?)null,
            paybackPeriodMonths = (int?)null,
            productivityGainPercent = (decimal?)null,
            timeSavedHours = (decimal?)null,
            qualityGainPercent = (decimal?)null,
            notes = (string?)null
        });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Result_ForAnUnknownProject_Is404()
    {
        RequireDatabase();
        var manager = await Factory.CreateClientAsAsync(UserRole.Manager);

        var response = await manager.GetAsync($"/api/v1/projects/{Guid.NewGuid()}/result");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Result_IsWritableByManagersOnly()
    {
        RequireDatabase();
        var (_, projectId) = await CreateProjectAsync();
        var operatorClient = await Factory.CreateClientAsAsync(UserRole.Operator);

        var response = await operatorClient.PutAsJsonAsync($"/api/v1/projects/{projectId}/result", new
        {
            estimatedRevenue = 1m,
            estimatedSavings = (decimal?)null,
            estimatedCost = (decimal?)null,
            actualRevenue = (decimal?)null,
            actualSavings = (decimal?)null,
            actualCost = (decimal?)null,
            paybackPeriodMonths = (int?)null,
            productivityGainPercent = (decimal?)null,
            timeSavedHours = (decimal?)null,
            qualityGainPercent = (decimal?)null,
            notes = (string?)null
        });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DecimalValues_SurviveTheRoundTripWithoutPrecisionLoss()
    {
        RequireDatabase();
        var (manager, projectId) = await CreateProjectAsync();

        // Values chosen to be unrepresentable in binary floating point.
        await manager.PutAsJsonAsync($"/api/v1/projects/{projectId}/result", new
        {
            estimatedRevenue = 1_234_567.89m,
            estimatedSavings = 0.10m,
            estimatedCost = 0.20m,
            actualRevenue = (decimal?)null,
            actualSavings = (decimal?)null,
            actualCost = (decimal?)null,
            paybackPeriodMonths = (int?)null,
            productivityGainPercent = (decimal?)null,
            timeSavedHours = (decimal?)null,
            qualityGainPercent = (decimal?)null,
            notes = (string?)null
        });

        var payload = await manager.GetFromJsonAsync<ResultPayload>(
            $"/api/v1/projects/{projectId}/result");

        payload!.Estimated!.Revenue.Should().Be(1_234_567.89m,
            because: "financial values are stored as Decimal128, not double");
        payload.Estimated.NetValue.Should().Be(1_234_567.79m);
    }
}
