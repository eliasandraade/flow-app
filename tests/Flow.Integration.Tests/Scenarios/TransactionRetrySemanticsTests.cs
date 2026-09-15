using System.Net;
using System.Net.Http.Json;
using Flow.Application.Common.Persistence;
using Flow.Infrastructure.Persistence.Mongo;
using Flow.Infrastructure.Persistence.Mongo.Repositories;
using Flow.Integration.Tests.Fixtures;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;
using DomainRefreshToken = Flow.Domain.Entities.RefreshToken;

namespace Flow.Integration.Tests.Scenarios;

/// <summary>
/// Telling a lost race apart from a sick database.
///
/// The compare-and-set that consumes a refresh token can come back empty for two reasons
/// that look identical from a distance and could not be more different up close. Either
/// another request consumed the token — a security signal, and the policy revokes the whole
/// family — or the cluster had an event: a primary step-down, an election, a write conflict.
/// The second says nothing whatsoever about the token.
///
/// An earlier version collapsed the two, so an election would have logged a user out of
/// every session. These tests keep them apart, and they check the lever that matters:
/// whether the application decided to revoke.
///
/// The cluster event is injected rather than provoked. Forcing a real step-down would make
/// the suite slow and unreliable, and it would be testing MongoDB rather than Flow. Every
/// other path here runs against the real database.
/// </summary>
[Collection(MongoCollection.Name)]
public class TransactionRetrySemanticsTests
{
    private readonly MongoFixture _mongo;

    public TransactionRetrySemanticsTests(MongoFixture mongo) => _mongo = mongo;

    private sealed record Session(string AccessToken, string RefreshToken, Guid UserId);

    private (FlowApiFactory Factory, RefreshTokenFaultPlan Plan) CreateFactory()
    {
        var plan = new RefreshTokenFaultPlan();

        var factory = new FlowApiFactory(_mongo, services =>
        {
            services.AddSingleton(plan);

            // The real repository is still underneath: only the failure that cannot be
            // provoked on command is simulated.
            services.AddScoped<IRefreshTokenRepository>(sp =>
                new FaultInjectingRefreshTokenRepository(
                    new MongoRefreshTokenRepository(
                        sp.GetRequiredService<FlowMongoContext>(),
                        sp.GetRequiredService<MongoSessionAccessor>()),
                    sp.GetRequiredService<RefreshTokenFaultPlan>()));
        });

        return (factory, plan);
    }

    private static async Task<Session> RegisterAsync(FlowApiFactory factory)
    {
        var client = factory.CreateClient();
        var email = $"retry-{Guid.NewGuid():N}@flow.test";

        var registered = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            name = "Retentativa",
            email,
            password = "IntegrationTest1!"
        });

        registered.EnsureSuccessStatusCode();
        var auth = (await registered.Content.ReadFromJsonAsync<FlowApiFactory.AuthResponse>())!;
        return new Session(auth.AccessToken, auth.RefreshToken, auth.UserId);
    }

    private static Task<HttpResponseMessage> RefreshAsync(FlowApiFactory factory, Session session) =>
        factory.CreateClient().PostAsJsonAsync("/api/v1/auth/refresh", new
        {
            accessToken = session.AccessToken,
            refreshToken = session.RefreshToken
        });

    private static Task<List<DomainRefreshToken>> TokensOfAsync(FlowApiFactory factory, Guid userId) =>
        factory.Mongo.RefreshTokens
            .Find(Builders<DomainRefreshToken>.Filter.Eq(x => x.UserId, userId))
            .ToListAsync();

    // ─── A cluster event is not a stolen token ──────────────────────────────

    [Fact]
    public async Task ATransientClusterErrorIsRetriedAndNeverTreatedAsReuse()
    {
        if (!_mongo.IsAvailable) throw new InvalidOperationException(_mongo.UnavailableReason);

        var (factory, plan) = CreateFactory();
        using var _ = factory;

        var session = await RegisterAsync(factory);
        plan.TransientFailuresToInject = 1;

        using var response = await RefreshAsync(factory, session);

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            because: "the driver retries a transaction that failed transiently, so the user never sees it");

        plan.ConsumeAttempts.Should().Be(2,
            because: "the whole transaction is re-executed, which is exactly the transient rule");

        plan.RevokeAllCalls.Should().Be(0,
            because: "an election says nothing about the token; logging the user out of every "
                   + "session because a replica changed role is the failure this test exists to catch");
    }

    [Fact]
    public async Task ARetriedTransactionCommitsItsWorkExactlyOnce()
    {
        if (!_mongo.IsAvailable) throw new InvalidOperationException(_mongo.UnavailableReason);

        var (factory, plan) = CreateFactory();
        using var _ = factory;

        var session = await RegisterAsync(factory);
        plan.TransientFailuresToInject = 1;

        using var response = await RefreshAsync(factory, session);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var stored = await TokensOfAsync(factory, session.UserId);

        // The discarded attempt also issued a replacement. If a retry could leave that
        // behind, the count would be three and the user would own a token nothing points at.
        stored.Should().HaveCount(2,
            because: "the original plus one replacement; the rolled-back attempt must leave nothing");

        stored.Count(t => t.IsActive).Should().Be(1);

        var original = stored.Single(t => t.TokenHash == DomainRefreshToken.Hash(session.RefreshToken));
        original.IsRevoked.Should().BeTrue();

        var renewed = (await response.Content.ReadFromJsonAsync<FlowApiFactory.AuthResponse>())!;
        original.ReplacedByTokenHash.Should().Be(DomainRefreshToken.Hash(renewed.RefreshToken),
            because: "the chain must point at the replacement the caller actually received");
    }

    [Fact]
    public async Task SeveralTransientErrorsInARowAreStillAbsorbed()
    {
        if (!_mongo.IsAvailable) throw new InvalidOperationException(_mongo.UnavailableReason);

        var (factory, plan) = CreateFactory();
        using var _ = factory;

        var session = await RegisterAsync(factory);
        plan.TransientFailuresToInject = 3;

        using var response = await RefreshAsync(factory, session);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        plan.ConsumeAttempts.Should().Be(4);
        plan.RevokeAllCalls.Should().Be(0);
        (await TokensOfAsync(factory, session.UserId)).Should().HaveCount(2);
    }

    [Fact]
    public async Task AnInfrastructureFailureThatIsNotTransientDoesNotRevokeAnySession()
    {
        if (!_mongo.IsAvailable) throw new InvalidOperationException(_mongo.UnavailableReason);

        var (factory, plan) = CreateFactory();
        using var _ = factory;

        var session = await RegisterAsync(factory);

        // No retry label: the driver gives up immediately and the failure surfaces.
        plan.PermanentFault = new MongoException("Simulated unrecoverable database failure.");

        using var response = await RefreshAsync(factory, session);

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError,
            because: "a database that is down is a server fault, not a rejected credential");

        plan.RevokeAllCalls.Should().Be(0);

        // And the session survives: once the database recovers, the same token still works.
        plan.PermanentFault = null;

        using var afterRecovery = await RefreshAsync(factory, session);

        afterRecovery.StatusCode.Should().Be(HttpStatusCode.OK,
            because: "nothing about the token changed while the database was unwell");
    }

    // ─── A genuinely lost race still is a security signal ───────────────────

    [Fact]
    public async Task AGenuinelyLostCompareAndSetIsStillTreatedAsReuse()
    {
        if (!_mongo.IsAvailable) throw new InvalidOperationException(_mongo.UnavailableReason);

        var (factory, plan) = CreateFactory();
        using var _ = factory;

        var session = await RegisterAsync(factory);

        // First rotation succeeds and consumes the token for real.
        using (var first = await RefreshAsync(factory, session))
            first.StatusCode.Should().Be(HttpStatusCode.OK);

        plan.RevokeAllCalls.Should().Be(0, "nothing suspicious has happened yet");

        // Presenting it again finds no consumable document — the CAS legitimately lost.
        using var replay = await RefreshAsync(factory, session);

        replay.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        plan.RevokeAllCalls.Should().Be(1,
            because: "this is the case the revocation policy is for, and it must still fire");
    }

    [Fact]
    public async Task TheContrastIsTheWholePoint()
    {
        if (!_mongo.IsAvailable) throw new InvalidOperationException(_mongo.UnavailableReason);

        // Same endpoint, same token shape, two failures — and opposite security decisions.
        var (transientFactory, transientPlan) = CreateFactory();
        using (transientFactory)
        {
            var session = await RegisterAsync(transientFactory);
            transientPlan.TransientFailuresToInject = 1;
            using var response = await RefreshAsync(transientFactory, session);

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            transientPlan.RevokeAllCalls.Should().Be(0);
        }

        var (reuseFactory, reusePlan) = CreateFactory();
        using (reuseFactory)
        {
            var session = await RegisterAsync(reuseFactory);
            (await RefreshAsync(reuseFactory, session)).Dispose();
            using var replay = await RefreshAsync(reuseFactory, session);

            replay.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            reusePlan.RevokeAllCalls.Should().Be(1);
        }
    }

    // ─── The transaction boundary itself ────────────────────────────────────

    [Fact]
    public async Task ATransientErrorInsideAnyUnitOfWorkReExecutesItAndCommitsOnce()
    {
        if (!_mongo.IsAvailable) throw new InvalidOperationException(_mongo.UnavailableReason);

        using var factory = new FlowApiFactory(_mongo);
        using var scope = factory.Services.CreateScope();

        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var guidelines = scope.ServiceProvider.GetRequiredService<IGuidelineRepository>();

        var attempts = 0;
        var createdIds = new List<Guid>();

        var result = await unitOfWork.ExecuteAsync(async ct =>
        {
            attempts++;

            var guideline = Flow.Domain.Entities.StrategicGuideline.Create(
                title: $"Diretriz da tentativa {attempts}",
                description: "Descrição para o teste de retentativa transacional.",
                category: Flow.Domain.Enums.GuidelineCategory.OperationalEfficiency,
                campaign: null,
                validFrom: DateTimeOffset.UtcNow.AddDays(-1),
                validUntil: null,
                createdBy: Guid.NewGuid());

            createdIds.Add(guideline.Id);
            await guidelines.AddAsync(guideline, ct);

            // Fails the first attempt the way a cluster event would, after the write.
            if (attempts == 1)
                throw RefreshTokenFaultPlan.TransientTransactionError("Simulated election mid-transaction.");

            return guideline.Id;
        }, CancellationToken.None);

        attempts.Should().Be(2, because: "the transient rule re-executes the whole callback");
        createdIds.Should().HaveCount(2, because: "both attempts did the work");

        var persisted = await factory.Mongo.Guidelines
            .Find(Builders<Flow.Domain.Entities.StrategicGuideline>.Filter
                .In(x => x.Id, createdIds))
            .ToListAsync();

        // This is the property that matters: the work of the discarded attempt is gone.
        persisted.Should().ContainSingle(
            because: "only the committed attempt may survive, or a retry would duplicate business data");
        persisted[0].Id.Should().Be(result);
        result.Should().Be(createdIds[1]);
    }
}
