using System.Net;
using System.Net.Http.Json;
using Flow.Domain.Enums;
using Flow.Integration.Tests.Fixtures;
using FluentAssertions;
using MongoDB.Driver;

namespace Flow.Integration.Tests.Scenarios;

public class AuthTests : IntegrationTestBase
{
    public AuthTests(MongoFixture mongo) : base(mongo) { }

    [Fact]
    public async Task Register_CreatesAnOperatorAndReturnsASession()
    {
        RequireDatabase();
        var client = Factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            name = "Novo Operador",
            email = $"novo-{Guid.NewGuid():N}@flow.test",
            password = "Operador1!"
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var auth = await response.Content.ReadFromJsonAsync<FlowApiFactory.AuthResponse>();
        auth!.Role.Should().Be(nameof(UserRole.Operator),
            because: "public registration must never mint an elevated role");
        auth.AccessToken.Should().NotBeNullOrWhiteSpace();
        auth.RefreshToken.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Register_WithADuplicateEmail_IsRejected()
    {
        RequireDatabase();
        var client = Factory.CreateClient();
        var email = $"dup-{Guid.NewGuid():N}@flow.test";

        var first = await client.PostAsJsonAsync("/api/v1/auth/register",
            new { name = "Primeiro", email, password = "Operador1!" });
        first.EnsureSuccessStatusCode();

        var second = await client.PostAsJsonAsync("/api/v1/auth/register",
            new { name = "Segundo", email, password = "Operador1!" });

        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Register_WithAWeakPassword_Returns422()
    {
        RequireDatabase();
        var client = Factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            name = "Fraco",
            email = $"fraco-{Guid.NewGuid():N}@flow.test",
            password = "abc"
        });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Login_WithWrongPassword_Is401()
    {
        RequireDatabase();
        var client = Factory.CreateClient();
        var email = $"login-{Guid.NewGuid():N}@flow.test";

        await client.PostAsJsonAsync("/api/v1/auth/register",
            new { name = "Alguém", email, password = "Operador1!" });

        var response = await client.PostAsJsonAsync("/api/v1/auth/login",
            new { email, password = "SenhaErrada1!" });

        // 401, not 403: the caller failed to prove who they are. 403 would mean the
        // identity was accepted and the action refused, which is a different problem and
        // sends the client down a different path.
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithoutAToken_Is401()
    {
        RequireDatabase();
        var client = Factory.CreateClient();

        var response = await client.GetAsync("/api/v1/ideas");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData(UserRole.Operator, HttpStatusCode.Forbidden)]
    [InlineData(UserRole.Manager, HttpStatusCode.OK)]
    [InlineData(UserRole.Leadership, HttpStatusCode.OK)]
    public async Task Dashboard_EnforcesTheRoleMatrix(UserRole role, HttpStatusCode expected)
    {
        RequireDatabase();
        var client = await Factory.CreateClientAsAsync(role);

        var response = await client.GetAsync("/api/v1/dashboard/summary");

        response.StatusCode.Should().Be(expected);
    }

    [Theory]
    [InlineData(UserRole.Operator, HttpStatusCode.Forbidden)]
    [InlineData(UserRole.Manager, HttpStatusCode.Forbidden)]
    [InlineData(UserRole.Leadership, HttpStatusCode.Created)]
    public async Task GuidelineCreation_IsLeadershipOnly(UserRole role, HttpStatusCode expected)
    {
        RequireDatabase();
        var client = await Factory.CreateClientAsAsync(role);

        var response = await client.PostAsJsonAsync("/api/v1/guidelines", new
        {
            title = "Diretriz de teste",
            description = "Descrição da diretriz de teste",
            category = nameof(GuidelineCategory.Quality),
            campaign = (string?)null,
            validFrom = DateTimeOffset.UtcNow.AddDays(-1),
            validUntil = (DateTimeOffset?)null
        });

        response.StatusCode.Should().Be(expected);
    }

    // ─── Refresh token rotation ─────────────────────────────────────────────

    [Fact]
    public async Task Refresh_IssuesANewPairAndRotatesTheOldToken()
    {
        RequireDatabase();
        var client = Factory.CreateClient();
        var email = $"refresh-{Guid.NewGuid():N}@flow.test";
        const string password = "Operador1!";

        var registered = await client.PostAsJsonAsync("/api/v1/auth/register",
            new { name = "Refresh", email, password });
        var original = (await registered.Content.ReadFromJsonAsync<FlowApiFactory.AuthResponse>())!;

        var refreshed = await client.PostAsJsonAsync("/api/v1/auth/refresh", new
        {
            accessToken = original.AccessToken,
            refreshToken = original.RefreshToken
        });

        refreshed.StatusCode.Should().Be(HttpStatusCode.OK);

        var renewed = (await refreshed.Content.ReadFromJsonAsync<FlowApiFactory.AuthResponse>())!;
        renewed.RefreshToken.Should().NotBe(original.RefreshToken,
            because: "every refresh rotates the token");
    }

    [Fact]
    public async Task Refresh_ReusingARotatedToken_IsRejectedAndKillsTheChain()
    {
        RequireDatabase();
        var client = Factory.CreateClient();
        var email = $"replay-{Guid.NewGuid():N}@flow.test";
        const string password = "Operador1!";

        var registered = await client.PostAsJsonAsync("/api/v1/auth/register",
            new { name = "Replay", email, password });
        var original = (await registered.Content.ReadFromJsonAsync<FlowApiFactory.AuthResponse>())!;

        var firstRefresh = await client.PostAsJsonAsync("/api/v1/auth/refresh",
            new { accessToken = original.AccessToken, refreshToken = original.RefreshToken });
        var renewed = (await firstRefresh.Content.ReadFromJsonAsync<FlowApiFactory.AuthResponse>())!;

        // Replaying the token that was already consumed is the classic theft signature.
        var replay = await client.PostAsJsonAsync("/api/v1/auth/refresh",
            new { accessToken = original.AccessToken, refreshToken = original.RefreshToken });

        replay.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // And the whole chain is invalidated, so the stolen-but-newer token dies too.
        var afterRevocation = await client.PostAsJsonAsync("/api/v1/auth/refresh",
            new { accessToken = renewed.AccessToken, refreshToken = renewed.RefreshToken });

        afterRevocation.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Logout_RevokesTheRefreshToken()
    {
        RequireDatabase();
        var client = Factory.CreateClient();
        var email = $"logout-{Guid.NewGuid():N}@flow.test";
        const string password = "Operador1!";

        var registered = await client.PostAsJsonAsync("/api/v1/auth/register",
            new { name = "Logout", email, password });
        var session = (await registered.Content.ReadFromJsonAsync<FlowApiFactory.AuthResponse>())!;

        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", session.AccessToken);

        var logout = await client.PostAsJsonAsync("/api/v1/auth/logout",
            new { refreshToken = session.RefreshToken });
        logout.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var refreshAfterLogout = await client.PostAsJsonAsync("/api/v1/auth/refresh",
            new { accessToken = session.AccessToken, refreshToken = session.RefreshToken });

        refreshAfterLogout.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task RefreshTokens_AreStoredOnlyAsHashes()
    {
        RequireDatabase();
        var client = Factory.CreateClient();
        var email = $"hash-{Guid.NewGuid():N}@flow.test";

        var registered = await client.PostAsJsonAsync("/api/v1/auth/register",
            new { name = "Hash", email, password = "Operador1!" });
        var session = (await registered.Content.ReadFromJsonAsync<FlowApiFactory.AuthResponse>())!;

        var stored = await Factory.Mongo.RefreshTokens
            .Find(Builders<Flow.Domain.Entities.RefreshToken>.Filter.Empty)
            .ToListAsync();

        stored.Should().NotBeEmpty();
        stored.Should().NotContain(t => t.TokenHash == session.RefreshToken,
            because: "a database dump must not be replayable against the API");
        stored.Should().Contain(t =>
            t.TokenHash == Flow.Domain.Entities.RefreshToken.Hash(session.RefreshToken));
    }
}
