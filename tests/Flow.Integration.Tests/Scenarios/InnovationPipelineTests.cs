using System.Net;
using System.Text.Json;
using System.Net.Http.Json;
using Flow.Domain.Entities;
using Flow.Domain.Enums;
using Flow.Integration.Tests.Fixtures;
using FluentAssertions;
using MongoDB.Driver;

namespace Flow.Integration.Tests.Scenarios;

/// <summary>
/// Walks the whole product pipeline against a real database:
/// strategy → idea → review → approval → project → execution → result.
/// </summary>
public class InnovationPipelineTests : IntegrationTestBase
{
    public InnovationPipelineTests(MongoFixture mongo) : base(mongo) { }

    private sealed record Created(Guid Id);
    private sealed record IdeaDetail(
        Guid Id, string Status, string Priority, int? Score, bool CanEdit, bool CanDelete);

    private async Task<Guid> CreateGuidelineAsync(HttpClient leadership, bool current = true)
    {
        var response = await leadership.PostAsJsonAsync("/api/v1/guidelines", new
        {
            title = $"Diretriz {Guid.NewGuid():N}",
            description = "Descrição da diretriz",
            category = nameof(GuidelineCategory.OperationalEfficiency),
            campaign = "Campanha de Teste",
            validFrom = DateTimeOffset.UtcNow.AddDays(current ? -30 : -400),
            validUntil = current ? (DateTimeOffset?)null : DateTimeOffset.UtcNow.AddDays(-200)
        });

        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<Created>())!.Id;
    }

    private static async Task<Guid> CreateIdeaAsync(HttpClient operatorClient, Guid? guidelineId = null)
    {
        var response = await operatorClient.PostAsJsonAsync("/api/v1/ideas", new
        {
            title = "Reduzir espera na doca",
            description = "Agendar janela de carga por transportadora.",
            problem = "Caminhões esperam duas horas e bloqueiam o pátio.",
            linkedGuidelineId = guidelineId
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<Created>())!.Id;
    }

    // ─── Idea lifecycle ─────────────────────────────────────────────────────

    [Fact]
    public async Task Idea_FollowsDraftToApprovedAndAwardsPoints()
    {
        RequireDatabase();
        var (operatorClient, operatorId) = await Factory.CreateClientWithUserAsync(UserRole.Operator);
        var manager = await Factory.CreateClientAsAsync(UserRole.Manager);

        var ideaId = await CreateIdeaAsync(operatorClient);

        (await operatorClient.PostAsync($"/api/v1/ideas/{ideaId}/submit", null))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var approve = await manager.PostAsJsonAsync($"/api/v1/ideas/{ideaId}/approve",
            new { managerComment = "Aprovada com evidência." });
        approve.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var detail = await manager.GetFromJsonAsync<IdeaDetail>($"/api/v1/ideas/{ideaId}");
        detail!.Status.Should().Be(nameof(IdeaStatus.Approved));

        var points = await operatorClient.GetFromJsonAsync<PointsSummary>("/api/v1/users/me/points");
        points!.Points.Should().Be(50, because: "approval awards the author 50 points");

        var ledger = await Factory.Mongo.PointLedger
            .Find(Builders<PointLedgerEntry>.Filter.Eq(e => e.UserId, operatorId))
            .ToListAsync();

        ledger.Should().ContainSingle()
            .Which.ReferenceId.Should().Be(ideaId,
                because: "every award must be traceable to what earned it");
    }

    [Fact]
    public async Task Idea_CanBeEditedAndDeletedOnlyWhileDraft()
    {
        RequireDatabase();
        var operatorClient = await Factory.CreateClientAsAsync(UserRole.Operator);

        var ideaId = await CreateIdeaAsync(operatorClient);

        var edit = await operatorClient.PutAsJsonAsync($"/api/v1/ideas/{ideaId}", new
        {
            title = "Título revisado",
            description = "Descrição revisada",
            problem = "Problema revisado",
            linkedGuidelineId = (Guid?)null
        });
        edit.StatusCode.Should().Be(HttpStatusCode.NoContent);

        await operatorClient.PostAsync($"/api/v1/ideas/{ideaId}/submit", null);

        var editAfterSubmit = await operatorClient.PutAsJsonAsync($"/api/v1/ideas/{ideaId}", new
        {
            title = "Tarde demais",
            description = "Descrição",
            problem = "Problema",
            linkedGuidelineId = (Guid?)null
        });
        editAfterSubmit.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var deleteAfterSubmit = await operatorClient.DeleteAsync($"/api/v1/ideas/{ideaId}");
        deleteAfterSubmit.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Idea_DraftIsDeletable()
    {
        RequireDatabase();
        var operatorClient = await Factory.CreateClientAsAsync(UserRole.Operator);

        var ideaId = await CreateIdeaAsync(operatorClient);

        var delete = await operatorClient.DeleteAsync($"/api/v1/ideas/{ideaId}");
        delete.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var afterDelete = await operatorClient.GetAsync($"/api/v1/ideas/{ideaId}");
        afterDelete.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // The idea is gone, but the record that it existed and was deleted is not.
        var audit = await Factory.Mongo.AuditLogs
            .Find(Builders<AuditLog>.Filter.Eq(a => a.EntityId, ideaId))
            .ToListAsync();

        audit.Should().Contain(a => a.Action == "Deleted");
    }

    [Fact]
    public async Task Idea_OfAnotherOperator_IsNotReadable()
    {
        RequireDatabase();
        var author = await Factory.CreateClientAsAsync(UserRole.Operator);
        var intruder = await Factory.CreateClientAsAsync(UserRole.Operator);

        var ideaId = await CreateIdeaAsync(author);

        var response = await intruder.GetAsync($"/api/v1/ideas/{ideaId}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            because: "role alone is not enough; ownership is checked too");
    }

    [Fact]
    public async Task Idea_ListForAnOperatorNeverLeaksOtherPeoplesIdeas()
    {
        RequireDatabase();
        var author = await Factory.CreateClientAsAsync(UserRole.Operator);
        var (other, otherId) = await Factory.CreateClientWithUserAsync(UserRole.Operator);

        await CreateIdeaAsync(author);
        await CreateIdeaAsync(other);

        // Even asking explicitly for someone else's ideas returns only your own.
        var listed = await author.GetFromJsonAsync<List<IdeaSummary>>(
            $"/api/v1/ideas?submittedById={otherId}");

        listed!.Should().OnlyContain(i => i.SubmittedBy != otherId);
    }

    [Fact]
    public async Task Idea_LinkedToAMissingGuideline_IsRejected()
    {
        RequireDatabase();
        var operatorClient = await Factory.CreateClientAsAsync(UserRole.Operator);

        var response = await operatorClient.PostAsJsonAsync("/api/v1/ideas", new
        {
            title = "Ideia órfã",
            description = "Descrição",
            problem = "Problema",
            linkedGuidelineId = Guid.NewGuid()
        });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound,
            because: "a dangling strategy reference would break the traceability chain");
    }

    [Fact]
    public async Task Idea_RejectionRequiresAReason()
    {
        RequireDatabase();
        var operatorClient = await Factory.CreateClientAsAsync(UserRole.Operator);
        var manager = await Factory.CreateClientAsAsync(UserRole.Manager);

        var ideaId = await CreateIdeaAsync(operatorClient);
        await operatorClient.PostAsync($"/api/v1/ideas/{ideaId}/submit", null);

        var withoutReason = await manager.PostAsJsonAsync(
            $"/api/v1/ideas/{ideaId}/reject", new { managerComment = "" });

        withoutReason.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task ValidationMessages_ComeBackInPortuguese()
    {
        RequireDatabase();
        var operatorClient = await Factory.CreateClientAsAsync(UserRole.Operator);

        // An empty title trips FluentValidation's built-in NotEmpty, not a custom message:
        // this is what proves the whole rule set is localised, not just the few strings
        // written by hand.
        var response = await operatorClient.PostAsJsonAsync("/api/v1/ideas", new
        {
            title = "",
            description = "Descrição suficiente para o teste.",
            problem = "Problema suficiente para o teste."
        });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        var messages = problem.GetProperty("errors").GetProperty("Title")
            .EnumerateArray().Select(item => item.GetString()!).ToList();

        messages.Should().NotBeEmpty();
        messages.Should().OnlyContain(message => !message.Contains("must not be empty"),
            because: "the interface is pt-BR and the user reads these verbatim");

        // Both halves have to be translated: the rule's wording and the field's name. A
        // message like "'Title' deve ser informado" is a half-translated interface.
        messages.Should().OnlyContain(message => !message.Contains("Title"));
        messages.Should().Contain(message => message.Contains("título"));
    }

    // ─── FlowScore ──────────────────────────────────────────────────────────

    [Fact]
    public async Task FlowScore_IsComputedByTheDomainAndPersistedWithItsBreakdown()
    {
        RequireDatabase();
        var operatorClient = await Factory.CreateClientAsAsync(UserRole.Operator);
        var manager = await Factory.CreateClientAsAsync(UserRole.Manager);

        var ideaId = await CreateIdeaAsync(operatorClient);
        await operatorClient.PostAsync($"/api/v1/ideas/{ideaId}/submit", null);

        var response = await manager.PutAsJsonAsync($"/api/v1/ideas/{ideaId}/flow-score", new
        {
            strategicAlignment = 6, impact = 8, feasibility = 7, urgency = 9, confidence = 7
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var score = await response.Content.ReadFromJsonAsync<FlowScorePayload>();
        score!.Total.Should().Be(63, because: "0.35*6 + 0.25*8 + 0.25*7 + 0.15*9 = 7.2, times 0.88, times 10");

        var stored = await Factory.Mongo.Ideas
            .Find(Builders<Idea>.Filter.Eq(i => i.Id, ideaId))
            .FirstAsync();

        stored.FlowScore!.Total.Should().Be(63);
        stored.FlowScore.Components.Impact.Should().Be(8);
    }

    [Fact]
    public async Task FlowScore_OutOfRangeComponents_Return422()
    {
        RequireDatabase();
        var operatorClient = await Factory.CreateClientAsAsync(UserRole.Operator);
        var manager = await Factory.CreateClientAsAsync(UserRole.Manager);

        var ideaId = await CreateIdeaAsync(operatorClient);
        await operatorClient.PostAsync($"/api/v1/ideas/{ideaId}/submit", null);

        var response = await manager.PutAsJsonAsync($"/api/v1/ideas/{ideaId}/flow-score", new
        {
            strategicAlignment = 11, impact = 8, feasibility = 7, urgency = 9, confidence = 7
        });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task ManualScore_AndFlowScore_CoexistIndependently()
    {
        RequireDatabase();
        var operatorClient = await Factory.CreateClientAsAsync(UserRole.Operator);
        var manager = await Factory.CreateClientAsAsync(UserRole.Manager);

        var ideaId = await CreateIdeaAsync(operatorClient);
        await operatorClient.PostAsync($"/api/v1/ideas/{ideaId}/submit", null);

        await manager.PutAsJsonAsync($"/api/v1/ideas/{ideaId}/flow-score", new
        {
            strategicAlignment = 5, impact = 5, feasibility = 5, urgency = 5, confidence = 5
        });
        await manager.PatchAsJsonAsync($"/api/v1/ideas/{ideaId}/score", new { score = 95 });
        await manager.PatchAsJsonAsync($"/api/v1/ideas/{ideaId}/priority",
            new { priority = nameof(IdeaPriority.High) });

        var stored = await Factory.Mongo.Ideas
            .Find(Builders<Idea>.Filter.Eq(i => i.Id, ideaId)).FirstAsync();

        stored.Score.Should().Be(95);
        stored.FlowScore!.Total.Should().Be(40);
        stored.Priority.Should().Be(IdeaPriority.High);
    }

    [Fact]
    public async Task CompareIdeas_RanksByFlowScore()
    {
        RequireDatabase();
        var operatorClient = await Factory.CreateClientAsAsync(UserRole.Operator);
        var manager = await Factory.CreateClientAsAsync(UserRole.Manager);

        var weak = await CreateIdeaAsync(operatorClient);
        var strong = await CreateIdeaAsync(operatorClient);

        foreach (var id in new[] { weak, strong })
            await operatorClient.PostAsync($"/api/v1/ideas/{id}/submit", null);

        await manager.PutAsJsonAsync($"/api/v1/ideas/{weak}/flow-score", new
        {
            strategicAlignment = 2, impact = 2, feasibility = 2, urgency = 2, confidence = 2
        });
        await manager.PutAsJsonAsync($"/api/v1/ideas/{strong}/flow-score", new
        {
            strategicAlignment = 9, impact = 9, feasibility = 9, urgency = 9, confidence = 9
        });

        var response = await manager.PostAsJsonAsync("/api/v1/ideas/compare",
            new { ideaIds = new[] { weak, strong } });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var comparison = await response.Content.ReadFromJsonAsync<ComparisonPayload>();
        comparison!.HighestFlowScoreId.Should().Be(strong);
        comparison.Ideas.First().Id.Should().Be(strong, because: "the comparison opens on the recommendation");
    }

    [Fact]
    public async Task CompareIdeas_NeedsAtLeastTwo()
    {
        RequireDatabase();
        var operatorClient = await Factory.CreateClientAsAsync(UserRole.Operator);
        var manager = await Factory.CreateClientAsAsync(UserRole.Manager);

        var ideaId = await CreateIdeaAsync(operatorClient);

        var response = await manager.PostAsJsonAsync("/api/v1/ideas/compare",
            new { ideaIds = new[] { ideaId } });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    // ─── Strategy → idea → project → result ─────────────────────────────────

    [Fact]
    public async Task ApprovedIdea_ConvertsIntoAProjectCarryingTheStrategy()
    {
        RequireDatabase();
        var leadership = await Factory.CreateClientAsAsync(UserRole.Leadership);
        var operatorClient = await Factory.CreateClientAsAsync(UserRole.Operator);
        var (manager, managerId) = await Factory.CreateClientWithUserAsync(UserRole.Manager);

        var guidelineId = await CreateGuidelineAsync(leadership);
        var ideaId = await CreateIdeaAsync(operatorClient, guidelineId);

        await operatorClient.PostAsync($"/api/v1/ideas/{ideaId}/submit", null);
        await manager.PostAsJsonAsync($"/api/v1/ideas/{ideaId}/approve", new { managerComment = "ok" });

        var convert = await manager.PostAsJsonAsync($"/api/v1/ideas/{ideaId}/convert", new
        {
            title = "Janela agendada de carga",
            description = "Agendamento por transportadora",
            priority = nameof(ProjectPriority.High),
            ownerId = managerId,
            estimatedCost = 45_000m,
            deadline = DateTimeOffset.UtcNow.AddDays(60),
            assistantRunId = (Guid?)null
        });

        convert.StatusCode.Should().Be(HttpStatusCode.Created);
        var projectId = (await convert.Content.ReadFromJsonAsync<Created>())!.Id;

        var project = await Factory.Mongo.Projects
            .Find(Builders<Project>.Filter.Eq(p => p.Id, projectId)).FirstAsync();

        project.SourceIdeaId.Should().Be(ideaId);
        project.LinkedGuidelineId.Should().Be(guidelineId,
            because: "the strategy has to survive the hop from idea to project");
    }

    [Fact]
    public async Task ConvertingAnIdeaTwice_IsRejected()
    {
        RequireDatabase();
        var operatorClient = await Factory.CreateClientAsAsync(UserRole.Operator);
        var (manager, managerId) = await Factory.CreateClientWithUserAsync(UserRole.Manager);

        var ideaId = await CreateIdeaAsync(operatorClient);
        await operatorClient.PostAsync($"/api/v1/ideas/{ideaId}/submit", null);
        await manager.PostAsJsonAsync($"/api/v1/ideas/{ideaId}/approve", new { managerComment = "ok" });

        object payload = new
        {
            title = "Projeto",
            description = "Descrição",
            priority = nameof(ProjectPriority.Medium),
            ownerId = managerId,
            estimatedCost = (decimal?)null,
            deadline = (DateTimeOffset?)null,
            assistantRunId = (Guid?)null
        };

        (await manager.PostAsJsonAsync($"/api/v1/ideas/{ideaId}/convert", payload))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        (await manager.PostAsJsonAsync($"/api/v1/ideas/{ideaId}/convert", payload))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task UnapprovedIdea_CannotBecomeAProject()
    {
        RequireDatabase();
        var operatorClient = await Factory.CreateClientAsAsync(UserRole.Operator);
        var (manager, managerId) = await Factory.CreateClientWithUserAsync(UserRole.Manager);

        var ideaId = await CreateIdeaAsync(operatorClient);

        var convert = await manager.PostAsJsonAsync($"/api/v1/ideas/{ideaId}/convert", new
        {
            title = "Projeto",
            description = "Descrição",
            priority = nameof(ProjectPriority.Medium),
            ownerId = managerId,
            estimatedCost = (decimal?)null,
            deadline = (DateTimeOffset?)null,
            assistantRunId = (Guid?)null
        });

        convert.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    private sealed record PointsSummary(Guid UserId, string UserName, int Points);
    private sealed record IdeaSummary(Guid Id, string Title, Guid SubmittedBy);
    private sealed record FlowScorePayload(int Total, int StrategicAlignment, int Impact);
    private sealed record ComparisonRow(Guid Id, string Title, int? FlowScore);
    private sealed record ComparisonPayload(
        List<ComparisonRow> Ideas, Guid? HighestFlowScoreId, Guid? HighestScoreId);
}
