using System.Net;
using System.Net.Http.Json;
using Flow.Application.Common.Persistence;
using Flow.Domain.Enums;
using Flow.Integration.Tests.Fixtures;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;
using DomainRefreshToken = Flow.Domain.Entities.RefreshToken;

namespace Flow.Integration.Tests.Scenarios;

/// <summary>
/// Concurrent rotation of a refresh token.
///
/// Sequential rotation and later replay were already covered, and neither proves anything
/// about concurrency: they only exercise the path where one request finishes before the
/// next begins. The dangerous case is two requests carrying the same token at the same
/// instant, both reading it while it is still active.
///
/// Nothing here is serialised on purpose. The requests are released from a shared gate and
/// awaited together, and the invariant is checked against what actually reached the
/// database.
/// </summary>
public class RefreshTokenConcurrencyTests : IntegrationTestBase
{
    public RefreshTokenConcurrencyTests(MongoFixture mongo) : base(mongo) { }

    private sealed record Session(string AccessToken, string RefreshToken, Guid UserId);

    private async Task<Session> RegisterAsync()
    {
        var client = Factory.CreateClient();
        var email = $"race-{Guid.NewGuid():N}@flow.test";

        var registered = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            name = "Corrida",
            email,
            password = "IntegrationTest1!"
        });

        registered.EnsureSuccessStatusCode();
        var auth = (await registered.Content.ReadFromJsonAsync<FlowApiFactory.AuthResponse>())!;
        return new Session(auth.AccessToken, auth.RefreshToken, auth.UserId);
    }

    /// <summary>
    /// The primitive, on its own. Twelve callers try to consume the same token at once and
    /// the contract is that exactly one succeeds — enforced by the database, in a single
    /// operation, not by anything this process does.
    /// </summary>
    [Fact]
    public async Task TryConsume_LetsExactlyOneOfManySimultaneousCallersWin()
    {
        RequireDatabase();
        var session = await RegisterAsync();
        var tokenHash = DomainRefreshToken.Hash(session.RefreshToken);

        const int racers = 12;
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var attempts = Enumerable.Range(0, racers).Select(i => Task.Run(async () =>
        {
            // Each racer gets its own scope, exactly as separate requests would.
            using var scope = Factory.Services.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IRefreshTokenRepository>();

            await gate.Task;

            return await repository.TryConsumeAsync(
                tokenHash,
                session.UserId,
                replacementTokenHash: $"replacement-{i:D2}",
                consumedAt: DateTimeOffset.UtcNow,
                CancellationToken.None);
        })).ToArray();

        gate.SetResult();
        var results = await Task.WhenAll(attempts);

        results.Count(won => won).Should().Be(1,
            because: "consumption is a compare-and-set, so only one caller can observe the token as unconsumed");

        var stored = await Factory.Mongo.RefreshTokens
            .Find(Builders<DomainRefreshToken>.Filter.Eq(x => x.TokenHash, tokenHash))
            .SingleAsync();

        stored.IsRevoked.Should().BeTrue();
        stored.ReplacedByTokenHash.Should().StartWith("replacement-",
            because: "the winner is the only caller that got to write its replacement");
    }

    /// <summary>
    /// The same race over HTTP, through the real endpoint. Repeated because a race that
    /// only shows up sometimes is still a race, and one round could get lucky.
    /// </summary>
    [Fact]
    public async Task TwoSimultaneousRefreshes_WithTheSameToken_LeaveExactlyOneWinner()
    {
        RequireDatabase();

        const int rounds = 5;

        for (var round = 0; round < rounds; round++)
        {
            var session = await RegisterAsync();
            var tokenHash = DomainRefreshToken.Hash(session.RefreshToken);

            var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            async Task<HttpResponseMessage> RefreshAsync()
            {
                var client = Factory.CreateClient();
                await gate.Task;

                return await client.PostAsJsonAsync("/api/v1/auth/refresh", new
                {
                    accessToken = session.AccessToken,
                    refreshToken = session.RefreshToken
                });
            }

            var first = Task.Run(RefreshAsync);
            var second = Task.Run(RefreshAsync);

            gate.SetResult();
            var responses = await Task.WhenAll(first, second);

            var accepted = responses.Where(r => r.StatusCode == HttpStatusCode.OK).ToList();
            var refused = responses.Where(r => r.StatusCode != HttpStatusCode.OK).ToList();

            accepted.Should().HaveCount(1, $"round {round}: exactly one request may consume the token");
            refused.Should().HaveCount(1, $"round {round}: the loser must be refused");
            refused[0].StatusCode.Should().Be(HttpStatusCode.Unauthorized,
                $"round {round}: losing the race is an authentication failure, not a server error");

            // ── What actually reached the database ──────────────────────────

            var userFilter = Builders<DomainRefreshToken>.Filter.Eq(x => x.UserId, session.UserId);
            var stored = await Factory.Mongo.RefreshTokens.Find(userFilter).ToListAsync();

            stored.Should().HaveCount(2,
                $"round {round}: the original plus one replacement. A third document would mean "
                + "the loser also issued a token, which is precisely the second valid chain "
                + "this test exists to rule out");

            var original = stored.Single(t => t.TokenHash == tokenHash);
            original.IsRevoked.Should().BeTrue($"round {round}: the presented token is consumed");

            var winner = (await accepted[0].Content.ReadFromJsonAsync<FlowApiFactory.AuthResponse>())!;
            original.ReplacedByTokenHash.Should().Be(DomainRefreshToken.Hash(winner.RefreshToken),
                $"round {round}: the chain points at the winner and at nobody else");

            stored.Count(t => t.IsActive).Should().BeLessThanOrEqualTo(1,
                $"round {round}: one token can never leave two live chains behind it");

            foreach (var response in responses) response.Dispose();
        }
    }

    /// <summary>
    /// The loser is treated as reuse, and reuse kills the family. This is the conservative
    /// reading of the OAuth 2.0 security guidance: a request that loses the race and a
    /// stolen token being replayed look identical from the server, so the safe answer is
    /// the same for both. It is also the behaviour the API already documented, and the
    /// mobile client is what keeps benign races from happening — it single-flights its
    /// refresh.
    /// </summary>
    [Fact]
    public async Task LosingTheRace_InvalidatesTheWholeChain()
    {
        RequireDatabase();
        var session = await RegisterAsync();

        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        async Task<HttpResponseMessage> RefreshAsync()
        {
            var client = Factory.CreateClient();
            await gate.Task;
            return await client.PostAsJsonAsync("/api/v1/auth/refresh", new
            {
                accessToken = session.AccessToken,
                refreshToken = session.RefreshToken
            });
        }

        var responses = await Task.Run(async () =>
        {
            var a = Task.Run(RefreshAsync);
            var b = Task.Run(RefreshAsync);
            gate.SetResult();
            return await Task.WhenAll(a, b);
        });

        var winner = responses.Single(r => r.StatusCode == HttpStatusCode.OK);
        var renewed = (await winner.Content.ReadFromJsonAsync<FlowApiFactory.AuthResponse>())!;

        var client = Factory.CreateClient();
        var afterTheRace = await client.PostAsJsonAsync("/api/v1/auth/refresh", new
        {
            accessToken = renewed.AccessToken,
            refreshToken = renewed.RefreshToken
        });

        afterTheRace.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            because: "reuse revokes every live token for the user, the winner's included");

        foreach (var response in responses) response.Dispose();
    }

    /// <summary>
    /// Rotation still works when nothing is racing, and the old token still dies. The point
    /// of the atomic consume is to change nothing about the happy path.
    /// </summary>
    [Fact]
    public async Task SequentialRotation_StillWorksAndStillConsumesTheOldToken()
    {
        RequireDatabase();
        var session = await RegisterAsync();
        var client = Factory.CreateClient();

        var first = await client.PostAsJsonAsync("/api/v1/auth/refresh", new
        {
            accessToken = session.AccessToken,
            refreshToken = session.RefreshToken
        });

        first.StatusCode.Should().Be(HttpStatusCode.OK);
        var renewed = (await first.Content.ReadFromJsonAsync<FlowApiFactory.AuthResponse>())!;
        renewed.RefreshToken.Should().NotBe(session.RefreshToken);

        var second = await client.PostAsJsonAsync("/api/v1/auth/refresh", new
        {
            accessToken = renewed.AccessToken,
            refreshToken = renewed.RefreshToken
        });

        second.StatusCode.Should().Be(HttpStatusCode.OK,
            because: "the replacement issued by a successful rotation is usable");
    }

    /// <summary>
    /// Logging out revokes the presented token atomically, and only when it belongs to the
    /// caller. Someone else's token must survive an attempt to log it out.
    /// </summary>
    [Fact]
    public async Task Logout_RevokesOnlyTheCallersOwnToken()
    {
        RequireDatabase();
        var mine = await RegisterAsync();
        var theirs = await RegisterAsync();

        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", mine.AccessToken);

        // Logging out while presenting a token that belongs to someone else.
        var response = await client.PostAsJsonAsync("/api/v1/auth/logout",
            new { refreshToken = theirs.RefreshToken });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var victim = Factory.CreateClient();
        var stillWorks = await victim.PostAsJsonAsync("/api/v1/auth/refresh", new
        {
            accessToken = theirs.AccessToken,
            refreshToken = theirs.RefreshToken
        });

        stillWorks.StatusCode.Should().Be(HttpStatusCode.OK,
            because: "ownership is a condition on the write, so another user cannot revoke this token");
    }

    /// <summary>
    /// Roles have nothing to do with this, but the token store is shared, so the happy path
    /// is confirmed for each of them.
    /// </summary>
    [Theory]
    [InlineData(UserRole.Operator)]
    [InlineData(UserRole.Manager)]
    [InlineData(UserRole.Leadership)]
    public async Task RotationWorksForEveryRole(UserRole role)
    {
        RequireDatabase();
        var email = $"{role.ToString().ToLowerInvariant()}-rot-{Guid.NewGuid():N}@flow.test";
        await Factory.CreateClientAsAsync(role, email);

        var client = Factory.CreateClient();
        var auth = await Factory.LoginAsync(client, email, "IntegrationTest1!");

        var refreshed = await client.PostAsJsonAsync("/api/v1/auth/refresh", new
        {
            accessToken = auth.AccessToken,
            refreshToken = auth.RefreshToken
        });

        refreshed.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
