using System.Net;
using System.Net.Http.Json;
using Flow.Application.Assistant;
using Flow.Domain.Entities;
using Flow.Domain.Enums;
using Flow.Integration.Tests.Fixtures;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;

namespace Flow.Integration.Tests.Scenarios;

/// <summary>
/// Exercises the intelligent features through the API with a stand-in provider.
///
/// A stand-in is the right tool here: the contract, the governance record, the failure
/// handling and — above all — the rule that the assistant never mutates domain state are
/// properties of our code, not of Gemini. Live validation against the real provider is
/// tracked separately and needs a credential this environment does not have.
/// </summary>
[Collection(MongoCollection.Name)]
public class AssistantTests
{
    private readonly MongoFixture _mongo;

    public AssistantTests(MongoFixture mongo) => _mongo = mongo;

    // ─── Stand-in provider ──────────────────────────────────────────────────

    private sealed class StubAssistant : IInnovationAssistant, IExecutiveInsightService
    {
        public AssistantOutcomeKind Outcome { get; set; } = AssistantOutcomeKind.Success;
        public int CompareCalls { get; private set; }
        public int DraftCalls { get; private set; }
        public IdeaComparisonContext? LastComparisonContext { get; private set; }
        public ExecutiveInsightContext? LastInsightContext { get; private set; }

        private AssistantResult<T> Build<T>(T value) where T : class => Outcome switch
        {
            AssistantOutcomeKind.Success =>
                new AssistantResult<T>(Outcome, value, "gemini-3.8-flash", 1234, 900, 300),
            _ => new AssistantResult<T>(Outcome, null, "gemini-3.8-flash", 20, Error: "stub failure")
        };

        public Task<AssistantResult<IdeaComparisonInsight>> CompareIdeasAsync(
            IdeaComparisonContext context, CancellationToken cancellationToken = default)
        {
            CompareCalls++;
            LastComparisonContext = context;

            var insight = new IdeaComparisonInsight(
                "Resumo da comparação.",
                context.Ideas.Select(i => new IdeaAssessment(
                    i.Id, i.Title, "Alinhada", ["Ponto forte"], ["Risco"], "Priorizar")).ToList(),
                ["Trade-off A vs B"],
                context.Ideas.FirstOrDefault()?.Id,
                "Maior FlowScore.",
                ["flowScore=75"],
                true);

            return Task.FromResult(Build(insight));
        }

        public Task<AssistantResult<ProjectDraft>> DraftProjectAsync(
            ProjectDraftContext context, CancellationToken cancellationToken = default)
        {
            DraftCalls++;

            var draft = new ProjectDraft(
                "Projeto proposto", "Descrição proposta", "High", "Planning",
                90, 50_000m, ["Marco 1"], ["Risco 1"], ["Critério 1"],
                "Justificativa", ["idea.flowScore=75"], true);

            return Task.FromResult(Build(draft));
        }

        public Task<AssistantResult<ExecutiveInsight>> GenerateAsync(
            ExecutiveInsightContext context, CancellationToken cancellationToken = default)
        {
            LastInsightContext = context;

            var insight = new ExecutiveInsight(
                "Resumo executivo.",
                [new InsightItem("Destaque", "Detalhe", ["ideas.total=3"])],
                [new InsightItem("Risco", "Detalhe", ["projects.blocked=1"])],
                [], [],
                ["projects.bottleneckIndex=100"],
                true,
                DateTimeOffset.UtcNow);

            return Task.FromResult(Build(insight));
        }
    }

    private FlowApiFactory CreateFactory(StubAssistant stub) =>
        new(_mongo, services =>
        {
            services.AddSingleton<IInnovationAssistant>(stub);
            services.AddSingleton<IExecutiveInsightService>(stub);
        });

    private sealed record Created(Guid Id);

    private static async Task<Guid> ApprovedIdeaAsync(HttpClient operatorClient, HttpClient manager)
    {
        var created = await operatorClient.PostAsJsonAsync("/api/v1/ideas", new
        {
            title = "Ideia para o copiloto",
            description = "Descrição",
            problem = "Problema operacional",
            linkedGuidelineId = (Guid?)null
        });
        var id = (await created.Content.ReadFromJsonAsync<Created>())!.Id;

        await operatorClient.PostAsync($"/api/v1/ideas/{id}/submit", null);
        await manager.PutAsJsonAsync($"/api/v1/ideas/{id}/flow-score", new
        {
            strategicAlignment = 9, impact = 9, feasibility = 6, urgency = 8, confidence = 8
        });
        await manager.PostAsJsonAsync($"/api/v1/ideas/{id}/approve", new { managerComment = "ok" });

        return id;
    }

    // ─── Contract and governance ────────────────────────────────────────────

    [Fact]
    public async Task CompareIdeas_ReturnsStructuredInsightAndRecordsAGovernanceRun()
    {
        if (!_mongo.IsAvailable) throw new InvalidOperationException(_mongo.UnavailableReason);

        var stub = new StubAssistant();
        using var factory = CreateFactory(stub);

        var operatorClient = await factory.CreateClientAsAsync(UserRole.Operator);
        var (manager, managerId) = await factory.CreateClientWithUserAsync(UserRole.Manager);

        var first = await ApprovedIdeaAsync(operatorClient, manager);
        var second = await ApprovedIdeaAsync(operatorClient, manager);

        var response = await manager.PostAsJsonAsync("/api/v1/assistant/compare-ideas",
            new { ideaIds = new[] { first, second }, question = "Qual priorizar?" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<ComparisonResponse>();
        payload!.Model.Should().Be("gemini-3.8-flash");
        payload.Insight.Assessments.Should().HaveCount(2);
        payload.Insight.EvidenceWasSufficient.Should().BeTrue();

        // The context handed to the provider carries the FlowScore breakdown, so the
        // explanation can reference the same numbers the product shows.
        stub.LastComparisonContext!.Ideas.Should().OnlyContain(i => i.FlowScore == 75);
        stub.LastComparisonContext.Question.Should().Be("Qual priorizar?");

        var run = await factory.Mongo.AssistantRuns
            .Find(Builders<AssistantRun>.Filter.Eq(r => r.Id, payload.AssistantRunId))
            .FirstAsync();

        run.Operation.Should().Be(AssistantOperation.CompareIdeas);
        run.Model.Should().Be("gemini-3.8-flash");
        run.Outcome.Should().Be(Flow.Domain.Enums.AssistantOutcome.Success);
        run.UserId.Should().Be(managerId);
        run.UserRole.Should().Be(UserRole.Manager);
        run.PromptTokens.Should().Be(900);
        run.SuggestionAccepted.Should().BeFalse();
        run.StructuredResult.Should().NotBeNullOrWhiteSpace();
        run.CorrelationId.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task ProjectDraft_CreatesNothingUntilAHumanConfirmsIt()
    {
        if (!_mongo.IsAvailable) throw new InvalidOperationException(_mongo.UnavailableReason);

        var stub = new StubAssistant();
        using var factory = CreateFactory(stub);

        var operatorClient = await factory.CreateClientAsAsync(UserRole.Operator);
        var (manager, managerId) = await factory.CreateClientWithUserAsync(UserRole.Manager);

        var ideaId = await ApprovedIdeaAsync(operatorClient, manager);

        var projectsBefore = await factory.Mongo.Projects
            .CountDocumentsAsync(Builders<Project>.Filter.Empty);

        var response = await manager.PostAsync(
            $"/api/v1/assistant/ideas/{ideaId}/project-draft", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var draft = await response.Content.ReadFromJsonAsync<DraftResponse>();
        draft!.Draft.Title.Should().Be("Projeto proposto");

        // The heart of the rule: a draft is a proposal, not a write.
        var projectsAfter = await factory.Mongo.Projects
            .CountDocumentsAsync(Builders<Project>.Filter.Empty);

        projectsAfter.Should().Be(projectsBefore,
            because: "the assistant proposes; only a human command creates a project");

        var idea = await factory.Mongo.Ideas
            .Find(Builders<Idea>.Filter.Eq(i => i.Id, ideaId)).FirstAsync();

        idea.Status.Should().Be(IdeaStatus.Approved,
            because: "asking for a draft must not move the idea forward on its own");

        // Now the manager accepts it, and only then does anything exist.
        var convert = await manager.PostAsJsonAsync($"/api/v1/ideas/{ideaId}/convert", new
        {
            title = draft.Draft.Title,
            description = draft.Draft.Description,
            priority = draft.Draft.SuggestedPriority,
            ownerId = managerId,
            estimatedCost = draft.Draft.SuggestedEstimatedCost,
            deadline = (DateTimeOffset?)null,
            assistantRunId = draft.AssistantRunId
        });

        convert.StatusCode.Should().Be(HttpStatusCode.Created);
        var projectId = (await convert.Content.ReadFromJsonAsync<Created>())!.Id;

        // The governance loop closes: the run now records that a human acted on it.
        var run = await factory.Mongo.AssistantRuns
            .Find(Builders<AssistantRun>.Filter.Eq(r => r.Id, draft.AssistantRunId))
            .FirstAsync();

        run.SuggestionAccepted.Should().BeTrue();
        run.AcceptedEntityType.Should().Be(nameof(Project));
        run.AcceptedEntityId.Should().Be(projectId);

        // And the creation went through the ordinary audited path.
        var audits = await factory.Mongo.AuditLogs
            .Find(Builders<AuditLog>.Filter.Eq(a => a.EntityId, projectId)).ToListAsync();

        audits.Should().ContainSingle().Which.Action.Should().Be("Created");
    }

    [Fact]
    public async Task ExecutiveInsights_AreBuiltOnlyFromTheDashboardPayload()
    {
        if (!_mongo.IsAvailable) throw new InvalidOperationException(_mongo.UnavailableReason);

        var stub = new StubAssistant();
        using var factory = CreateFactory(stub);

        var leadership = await factory.CreateClientAsAsync(UserRole.Leadership);

        var response = await leadership.PostAsync("/api/v1/dashboard/insights", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<InsightResponse>();
        payload!.Insight.ExecutiveSummary.Should().NotBeNullOrWhiteSpace();

        // What the provider saw is the dashboard, and only the dashboard.
        stub.LastInsightContext!.DashboardJson.Should().Contain("bottleneckIndex");
        stub.LastInsightContext.DashboardJson.Should().Contain("conversionRate");
        stub.LastInsightContext.DashboardJson.Should().NotContain("passwordHash");
        stub.LastInsightContext.DashboardJson.Should().NotContain("tokenHash");
    }

    // ─── Failure modes ──────────────────────────────────────────────────────

    [Theory]
    [InlineData(AssistantOutcomeKind.Timeout, Flow.Domain.Enums.AssistantOutcome.Timeout)]
    [InlineData(AssistantOutcomeKind.Unavailable, Flow.Domain.Enums.AssistantOutcome.Unavailable)]
    [InlineData(AssistantOutcomeKind.MalformedResponse, Flow.Domain.Enums.AssistantOutcome.Failed)]
    [InlineData(AssistantOutcomeKind.NotConfigured, Flow.Domain.Enums.AssistantOutcome.Unavailable)]
    public async Task WhenTheProviderFails_TheApiDegradesAndStillRecordsTheAttempt(
        AssistantOutcomeKind failure, Flow.Domain.Enums.AssistantOutcome expectedRecord)
    {
        if (!_mongo.IsAvailable) throw new InvalidOperationException(_mongo.UnavailableReason);

        var stub = new StubAssistant { Outcome = failure };
        using var factory = CreateFactory(stub);

        var leadership = await factory.CreateClientAsAsync(UserRole.Leadership);

        var response = await leadership.PostAsync("/api/v1/dashboard/insights", null);

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable,
            because: "a degraded dependency is not a client error and not a broken product");

        // The assistant's failures are the one class of message written for the end user,
        // so they are promoted to userMessage and shown verbatim. Everything else the
        // client translates itself, because Title carries developer English.
        var problem = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        problem.GetProperty("userMessage").GetString()
            .Should().NotBeNullOrWhiteSpace().And.NotContain("Exception");
        problem.TryGetProperty("traceId", out _).Should().BeTrue();

        var runs = await factory.Mongo.AssistantRuns
            .Find(Builders<AssistantRun>.Filter.Empty).ToListAsync();

        runs.Should().ContainSingle().Which.Outcome.Should().Be(expectedRecord);
        runs[0].ErrorKind.Should().Be(failure.ToString());
    }

    [Fact]
    public async Task WhenTheAssistantIsDown_TheRestOfTheProductKeepsWorking()
    {
        if (!_mongo.IsAvailable) throw new InvalidOperationException(_mongo.UnavailableReason);

        var stub = new StubAssistant { Outcome = AssistantOutcomeKind.Unavailable };
        using var factory = CreateFactory(stub);

        var operatorClient = await factory.CreateClientAsAsync(UserRole.Operator);
        var (manager, _) = await factory.CreateClientWithUserAsync(UserRole.Manager);
        var leadership = await factory.CreateClientAsAsync(UserRole.Leadership);

        // The whole pipeline still runs, including the deterministic FlowScore.
        var ideaId = await ApprovedIdeaAsync(operatorClient, manager);

        var idea = await factory.Mongo.Ideas
            .Find(Builders<Idea>.Filter.Eq(i => i.Id, ideaId)).FirstAsync();

        idea.FlowScore!.Total.Should().Be(75,
            because: "prioritisation is computed by the domain and never depends on the provider");

        (await leadership.GetAsync("/api/v1/dashboard/summary"))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        (await manager.GetAsync("/api/v1/ideas"))
            .StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ─── Authorization ──────────────────────────────────────────────────────

    [Fact]
    public async Task Copilot_IsNotAvailableToOperators()
    {
        if (!_mongo.IsAvailable) throw new InvalidOperationException(_mongo.UnavailableReason);

        var stub = new StubAssistant();
        using var factory = CreateFactory(stub);

        var operatorClient = await factory.CreateClientAsAsync(UserRole.Operator);

        var response = await operatorClient.PostAsJsonAsync("/api/v1/assistant/compare-ideas",
            new { ideaIds = new[] { Guid.NewGuid(), Guid.NewGuid() }, question = (string?)null });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        stub.CompareCalls.Should().Be(0, because: "authorization runs before we spend a request");
    }

    [Fact]
    public async Task ExecutiveInsights_AreLeadershipOnly()
    {
        if (!_mongo.IsAvailable) throw new InvalidOperationException(_mongo.UnavailableReason);

        var stub = new StubAssistant();
        using var factory = CreateFactory(stub);

        var manager = await factory.CreateClientAsAsync(UserRole.Manager);

        (await manager.PostAsync("/api/v1/dashboard/insights", null))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ProjectDraft_ForAnUnapprovedIdea_IsRefusedBeforeCallingTheProvider()
    {
        if (!_mongo.IsAvailable) throw new InvalidOperationException(_mongo.UnavailableReason);

        var stub = new StubAssistant();
        using var factory = CreateFactory(stub);

        var operatorClient = await factory.CreateClientAsAsync(UserRole.Operator);
        var manager = await factory.CreateClientAsAsync(UserRole.Manager);

        var created = await operatorClient.PostAsJsonAsync("/api/v1/ideas", new
        {
            title = "Ainda em rascunho",
            description = "Descrição",
            problem = "Problema",
            linkedGuidelineId = (Guid?)null
        });
        var ideaId = (await created.Content.ReadFromJsonAsync<Created>())!.Id;

        var response = await manager.PostAsync(
            $"/api/v1/assistant/ideas/{ideaId}/project-draft", null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        stub.DraftCalls.Should().Be(0, because: "domain rules are checked before spending a call");
    }

    [Fact]
    public async Task CompareIdeas_WithASingleIdea_IsRefusedBeforeCallingTheProvider()
    {
        if (!_mongo.IsAvailable) throw new InvalidOperationException(_mongo.UnavailableReason);

        var stub = new StubAssistant();
        using var factory = CreateFactory(stub);

        var manager = await factory.CreateClientAsAsync(UserRole.Manager);

        var response = await manager.PostAsJsonAsync("/api/v1/assistant/compare-ideas",
            new { ideaIds = new[] { Guid.NewGuid() }, question = (string?)null });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        stub.CompareCalls.Should().Be(0);
    }

    private sealed record ComparisonResponse(
        Guid AssistantRunId, string Model, long LatencyMs, IdeaComparisonInsight Insight);

    private sealed record DraftResponse(
        Guid AssistantRunId, Guid IdeaId, string Model, long LatencyMs, ProjectDraft Draft);

    private sealed record InsightResponse(
        Guid AssistantRunId, string Model, long LatencyMs, ExecutiveInsight Insight);
}
