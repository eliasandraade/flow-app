using System.Net;
using System.Net.Http.Json;
using Flow.Domain.Enums;
using Flow.Integration.Tests.Fixtures;
using FluentAssertions;

namespace Flow.Integration.Tests.Scenarios;

/// <summary>
/// Object-level authorization, the IDOR/BOLA boundary.
///
/// Role attributes answer "may this role call this endpoint". They cannot answer "may this
/// user read this document", and every test here attacks the gap between the two: an
/// authenticated user who simply knows a GUID.
///
/// The rule under test, from the MVP role matrix: Manager and Leadership read the whole
/// programme; an Operator reads only their own trail — their ideas, and the projects and
/// results those ideas produced.
/// </summary>
public class ResourceAuthorizationTests : IntegrationTestBase
{
    public ResourceAuthorizationTests(MongoFixture mongo) : base(mongo) { }

    private sealed record Created(Guid Id);
    private sealed record ProjectSummary(Guid Id, string Title);

    private static async Task<Guid> CreateIdeaAsync(HttpClient operatorClient, string title)
    {
        var response = await operatorClient.PostAsJsonAsync("/api/v1/ideas", new
        {
            title,
            description = "Descrição suficiente para o teste de autorização.",
            problem = "Problema suficiente para o teste de autorização."
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<Created>())!.Id;
    }

    /// <summary>
    /// Walks the real pipeline — idea, submission, approval, conversion — so the project
    /// under test is genuinely linked to the operator who proposed it.
    /// </summary>
    private async Task<(Guid IdeaId, Guid ProjectId)> IdeaToProjectAsync(
        HttpClient operatorClient, HttpClient manager, Guid managerId, string title)
    {
        var ideaId = await CreateIdeaAsync(operatorClient, title);

        (await operatorClient.PostAsync($"/api/v1/ideas/{ideaId}/submit", null))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await manager.PostAsJsonAsync($"/api/v1/ideas/{ideaId}/approve",
            new { managerComment = "Aprovada para o teste." }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var converted = await manager.PostAsJsonAsync($"/api/v1/ideas/{ideaId}/convert", new
        {
            title = $"Projeto de {title}",
            description = "Projeto criado a partir da ideia.",
            priority = nameof(ProjectPriority.High),
            ownerId = managerId,
            estimatedCost = 10_000m,
            deadline = (DateTimeOffset?)null,
            assistantRunId = (Guid?)null
        });

        converted.StatusCode.Should().Be(HttpStatusCode.Created);
        return (ideaId, (await converted.Content.ReadFromJsonAsync<Created>())!.Id);
    }

    private async Task RecordResultAsync(HttpClient manager, Guid projectId)
    {
        var response = await manager.PutAsJsonAsync($"/api/v1/projects/{projectId}/result", new
        {
            estimatedRevenue = 250_000m,
            estimatedSavings = 40_000m,
            estimatedCost = 60_000m,
            actualRevenue = 310_000m,
            actualSavings = 55_000m,
            actualCost = 58_000m,
            paybackPeriodMonths = 7,
            productivityGainPercent = 12m,
            timeSavedHours = 900m,
            qualityGainPercent = 8m,
            notes = "Resultado do teste de autorização."
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ─── Ideas ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Operator_ReadsTheirOwnIdea()
    {
        RequireDatabase();
        var author = await Factory.CreateClientAsAsync(UserRole.Operator);

        var ideaId = await CreateIdeaAsync(author, "Ideia do próprio autor");

        (await author.GetAsync($"/api/v1/ideas/{ideaId}"))
            .StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Operator_CannotReadTheIdeaOfAnotherOperator()
    {
        RequireDatabase();
        var author = await Factory.CreateClientAsAsync(UserRole.Operator);
        var intruder = await Factory.CreateClientAsAsync(UserRole.Operator);

        var ideaId = await CreateIdeaAsync(author, "Ideia alheia");

        (await intruder.GetAsync($"/api/v1/ideas/{ideaId}"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden,
                because: "knowing the GUID must never be enough to read someone else's idea");
    }

    [Fact]
    public async Task Operator_ReadsTheCommentsOnTheirOwnIdea()
    {
        RequireDatabase();
        var author = await Factory.CreateClientAsAsync(UserRole.Operator);
        var manager = await Factory.CreateClientAsAsync(UserRole.Manager);

        var ideaId = await CreateIdeaAsync(author, "Ideia comentada");
        (await author.PostAsync($"/api/v1/ideas/{ideaId}/submit", null))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await manager.PostAsJsonAsync($"/api/v1/ideas/{ideaId}/comments",
            new { body = "Precisa de estimativa de custo." }))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        var response = await author.GetAsync($"/api/v1/ideas/{ideaId}/comments");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<List<object>>())!.Should().HaveCount(1);
    }

    [Fact]
    public async Task Operator_CannotReadTheCommentsOnAnotherIdea()
    {
        RequireDatabase();
        var author = await Factory.CreateClientAsAsync(UserRole.Operator);
        var intruder = await Factory.CreateClientAsAsync(UserRole.Operator);
        var manager = await Factory.CreateClientAsAsync(UserRole.Manager);

        var ideaId = await CreateIdeaAsync(author, "Ideia com parecer");
        await author.PostAsync($"/api/v1/ideas/{ideaId}/submit", null);
        await manager.PostAsJsonAsync($"/api/v1/ideas/{ideaId}/comments",
            new { body = "Parecer confidencial do gestor." });

        (await intruder.GetAsync($"/api/v1/ideas/{ideaId}/comments"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden,
                because: "the manager's assessment is as sensitive as the idea itself");
    }

    // ─── Projects ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Operator_CannotReadAProjectThatIsNotTheirs()
    {
        RequireDatabase();
        var (manager, managerId) = await Factory.CreateClientWithUserAsync(UserRole.Manager);
        var author = await Factory.CreateClientAsAsync(UserRole.Operator);
        var intruder = await Factory.CreateClientAsAsync(UserRole.Operator);

        var (_, projectId) = await IdeaToProjectAsync(author, manager, managerId, "Projeto alheio");

        (await intruder.GetAsync($"/api/v1/projects/{projectId}"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Operator_CannotReadTheTimelineOfAProjectThatIsNotTheirs()
    {
        RequireDatabase();
        var (manager, managerId) = await Factory.CreateClientWithUserAsync(UserRole.Manager);
        var author = await Factory.CreateClientAsAsync(UserRole.Operator);
        var intruder = await Factory.CreateClientAsAsync(UserRole.Operator);

        var (_, projectId) = await IdeaToProjectAsync(author, manager, managerId, "Projeto com histórico");

        (await intruder.GetAsync($"/api/v1/projects/{projectId}/timeline"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden,
                because: "the timeline names who did what and why work was blocked");
    }

    [Fact]
    public async Task Operator_CannotReadTheFinancialResultOfAProjectThatIsNotTheirs()
    {
        RequireDatabase();
        var (manager, managerId) = await Factory.CreateClientWithUserAsync(UserRole.Manager);
        var author = await Factory.CreateClientAsAsync(UserRole.Operator);
        var intruder = await Factory.CreateClientAsAsync(UserRole.Operator);

        var (_, projectId) = await IdeaToProjectAsync(author, manager, managerId, "Projeto com resultado");
        await RecordResultAsync(manager, projectId);

        (await intruder.GetAsync($"/api/v1/projects/{projectId}/result"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden,
                because: "revenue, savings and cost are the most sensitive figures in the product");
    }

    [Fact]
    public async Task Operator_CannotReadTheSnapshotsOfAnyProject()
    {
        RequireDatabase();
        var (manager, managerId) = await Factory.CreateClientWithUserAsync(UserRole.Manager);
        var author = await Factory.CreateClientAsAsync(UserRole.Operator);

        var (_, projectId) = await IdeaToProjectAsync(author, manager, managerId, "Projeto com snapshots");

        // Snapshots are a governance artefact for the roles that steer the programme, and
        // the role attribute alone is the right control here.
        (await author.GetAsync($"/api/v1/projects/{projectId}/snapshots"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Operator_ListsOnlyTheProjectsThatCameFromTheirOwnIdeas()
    {
        RequireDatabase();
        var (manager, managerId) = await Factory.CreateClientWithUserAsync(UserRole.Manager);
        var author = await Factory.CreateClientAsAsync(UserRole.Operator);
        var stranger = await Factory.CreateClientAsAsync(UserRole.Operator);

        var (_, mine) = await IdeaToProjectAsync(author, manager, managerId, "Meu projeto");
        var (_, theirs) = await IdeaToProjectAsync(stranger, manager, managerId, "Projeto de outro");

        var listed = (await author.GetFromJsonAsync<List<ProjectSummary>>("/api/v1/projects"))!;

        listed.Select(p => p.Id).Should().Contain(mine,
            because: "the specification grants an operator visibility of their own trail");
        listed.Select(p => p.Id).Should().NotContain(theirs,
            because: "a list endpoint must not enumerate what the caller may not open");
    }

    [Fact]
    public async Task Operator_ReadsTheProjectAndResultThatCameFromTheirOwnIdea()
    {
        RequireDatabase();
        var (manager, managerId) = await Factory.CreateClientWithUserAsync(UserRole.Manager);
        var author = await Factory.CreateClientAsAsync(UserRole.Operator);

        var (_, projectId) = await IdeaToProjectAsync(author, manager, managerId, "Projeto do autor");
        await RecordResultAsync(manager, projectId);

        // "Operator (own)" in the role matrix is not an empty promise: closing the loop on
        // their own idea is the whole point of the operator journey.
        (await author.GetAsync($"/api/v1/projects/{projectId}")).StatusCode
            .Should().Be(HttpStatusCode.OK);
        (await author.GetAsync($"/api/v1/projects/{projectId}/timeline")).StatusCode
            .Should().Be(HttpStatusCode.OK);
        (await author.GetAsync($"/api/v1/projects/{projectId}/result")).StatusCode
            .Should().Be(HttpStatusCode.OK);
    }

    // ─── The roles that are meant to see everything still do ────────────────

    [Theory]
    [InlineData(UserRole.Manager)]
    [InlineData(UserRole.Leadership)]
    public async Task ManagerAndLeadership_KeepProgrammeWideRead(UserRole role)
    {
        RequireDatabase();
        var (manager, managerId) = await Factory.CreateClientWithUserAsync(UserRole.Manager);
        var author = await Factory.CreateClientAsAsync(UserRole.Operator);
        var reader = await Factory.CreateClientAsAsync(role);

        var (ideaId, projectId) = await IdeaToProjectAsync(
            author, manager, managerId, "Projeto do programa");
        await RecordResultAsync(manager, projectId);

        (await reader.GetAsync($"/api/v1/ideas/{ideaId}")).StatusCode
            .Should().Be(HttpStatusCode.OK);
        (await reader.GetAsync($"/api/v1/ideas/{ideaId}/comments")).StatusCode
            .Should().Be(HttpStatusCode.OK);
        (await reader.GetAsync($"/api/v1/projects/{projectId}")).StatusCode
            .Should().Be(HttpStatusCode.OK);
        (await reader.GetAsync($"/api/v1/projects/{projectId}/timeline")).StatusCode
            .Should().Be(HttpStatusCode.OK);
        (await reader.GetAsync($"/api/v1/projects/{projectId}/result")).StatusCode
            .Should().Be(HttpStatusCode.OK);

        var listed = (await reader.GetFromJsonAsync<List<ProjectSummary>>("/api/v1/projects"))!;
        listed.Select(p => p.Id).Should().Contain(projectId);
    }

    // ─── Unauthenticated stays 401, never 403 ───────────────────────────────

    [Theory]
    [InlineData("/api/v1/projects")]
    [InlineData("/api/v1/ideas")]
    public async Task AnonymousCallsAreRefusedWith401NotWith403(string path)
    {
        RequireDatabase();
        var anonymous = Factory.CreateClient();

        // 401 means "I do not know who you are". Answering 403 here would claim the caller
        // was identified and merely lacks permission, which is a different problem.
        (await anonymous.GetAsync(path)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AnonymousCallToAKnownProjectIdIsRefusedWith401()
    {
        RequireDatabase();
        var (manager, managerId) = await Factory.CreateClientWithUserAsync(UserRole.Manager);
        var author = await Factory.CreateClientAsAsync(UserRole.Operator);

        var (_, projectId) = await IdeaToProjectAsync(author, manager, managerId, "Projeto conhecido");

        var anonymous = Factory.CreateClient();

        (await anonymous.GetAsync($"/api/v1/projects/{projectId}")).StatusCode
            .Should().Be(HttpStatusCode.Unauthorized);
        (await anonymous.GetAsync($"/api/v1/projects/{projectId}/result")).StatusCode
            .Should().Be(HttpStatusCode.Unauthorized);
    }
}
