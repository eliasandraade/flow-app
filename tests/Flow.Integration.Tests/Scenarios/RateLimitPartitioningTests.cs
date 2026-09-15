using System.Net;
using System.Net.Http.Json;
using Flow.Domain.Enums;
using Flow.Integration.Tests.Fixtures;
using FluentAssertions;

namespace Flow.Integration.Tests.Scenarios;

/// <summary>
/// Which bucket a request lands in.
///
/// The AI policy is meant to be partitioned by the authenticated user, because those calls
/// cost real money and one person should not be able to spend everyone else's quota. That
/// only works if the limiter can see who is calling, which depends on two things that are
/// easy to get wrong and invisible when they are: the limiter has to run after
/// authentication, and the user id has to be read the way the token actually arrives.
///
/// Reading the order in Program.cs proves nothing, so this asserts the behaviour instead.
/// Every request here comes from the same address — the test host reports no remote address
/// at all, which is exactly the shape of many users behind one NAT — so if the partitioning
/// fell back to the address, the second user would already be throttled.
/// </summary>
[Collection(MongoCollection.Name)]
public class RateLimitPartitioningTests
{
    private readonly MongoFixture _mongo;

    public RateLimitPartitioningTests(MongoFixture mongo) => _mongo = mongo;

    private const string AiEndpoint = "/api/v1/dashboard/insights";

    private FlowApiFactory CreateFactory(int aiPermits = 3, int authPermits = 3) =>
        new(_mongo, overrideConfiguration: new()
        {
            ["RateLimiting:Enabled"] = "true",
            ["RateLimiting:AiPermitPerFiveMinutes"] = aiPermits.ToString(),
            ["RateLimiting:AuthPermitPerMinute"] = authPermits.ToString(),
            ["RateLimiting:GlobalPermitPerMinute"] = "1000"
        });

    private static async Task<List<HttpStatusCode>> CallAiAsync(HttpClient client, int times)
    {
        var statuses = new List<HttpStatusCode>();

        for (var i = 0; i < times; i++)
        {
            using var response = await client.PostAsync(AiEndpoint, null);
            statuses.Add(response.StatusCode);
        }

        return statuses;
    }

    // ─── The point of the whole policy ──────────────────────────────────────

    [Fact]
    public async Task TwoAuthenticatedUsersDoNotShareTheAiQuota()
    {
        if (!_mongo.IsAvailable) throw new InvalidOperationException(_mongo.UnavailableReason);

        using var factory = CreateFactory(aiPermits: 3);

        var first = await factory.CreateClientAsAsync(UserRole.Leadership);
        var second = await factory.CreateClientAsAsync(UserRole.Leadership);

        // The first user spends their whole allowance and then hits the wall.
        var spent = await CallAiAsync(first, 3);
        spent.Should().NotContain(HttpStatusCode.TooManyRequests,
            because: "three calls is exactly the allowance");

        using var overTheLimit = await first.PostAsync(AiEndpoint, null);
        overTheLimit.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);

        // The second user has not spent anything, and shares an address with the first.
        using var untouched = await second.PostAsync(AiEndpoint, null);
        untouched.StatusCode.Should().NotBe(HttpStatusCode.TooManyRequests,
            because: "the quota belongs to a user, not to whatever address they came from");
    }

    [Fact]
    public async Task TheSameUserIsStillLimited()
    {
        if (!_mongo.IsAvailable) throw new InvalidOperationException(_mongo.UnavailableReason);

        using var factory = CreateFactory(aiPermits: 2);
        var leadership = await factory.CreateClientAsAsync(UserRole.Leadership);

        var statuses = await CallAiAsync(leadership, 5);

        statuses.Take(2).Should().NotContain(HttpStatusCode.TooManyRequests);
        statuses.Skip(2).Should().OnlyContain(status => status == HttpStatusCode.TooManyRequests,
            because: "partitioning per user is not the same as giving each user no limit");
    }

    [Fact]
    public async Task TheSameUserAcrossTwoClientsSharesTheirOwnQuota()
    {
        if (!_mongo.IsAvailable) throw new InvalidOperationException(_mongo.UnavailableReason);

        using var factory = CreateFactory(aiPermits: 2);

        var email = $"leadership-{Guid.NewGuid():N}@flow.test";
        await factory.CreateClientAsAsync(UserRole.Leadership, email);

        // Two sessions, one person: a second device must not double the allowance.
        var phone = factory.CreateClient();
        var laptop = factory.CreateClient();

        foreach (var client in new[] { phone, laptop })
        {
            var auth = await factory.LoginAsync(client, email, "IntegrationTest1!");
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", auth.AccessToken);
        }

        (await CallAiAsync(phone, 2)).Should().NotContain(HttpStatusCode.TooManyRequests);

        using var fromTheOtherDevice = await laptop.PostAsync(AiEndpoint, null);
        fromTheOtherDevice.StatusCode.Should().Be(HttpStatusCode.TooManyRequests,
            because: "the partition key is the user, so their devices draw on one quota");
    }

    // ─── The auth policy is still by address, and still works ───────────────

    [Fact]
    public async Task AuthenticationEndpointsStayLimitedByAddressAcrossDifferentAccounts()
    {
        if (!_mongo.IsAvailable) throw new InvalidOperationException(_mongo.UnavailableReason);

        using var factory = CreateFactory(authPermits: 3);
        var client = factory.CreateClient();

        var statuses = new List<HttpStatusCode>();

        // Credential stuffing does not reuse one account; it walks a list. Partitioning
        // this policy by user would therefore protect nothing, which is why it stays keyed
        // on the address even after the limiter moved behind authentication.
        for (var attempt = 0; attempt < 6; attempt++)
        {
            using var response = await client.PostAsJsonAsync("/api/v1/auth/login", new
            {
                email = $"victim-{attempt}@flow.test",
                password = "SenhaErrada1!"
            });

            statuses.Add(response.StatusCode);
        }

        statuses.Should().Contain(HttpStatusCode.TooManyRequests);
        statuses.Take(3).Should().OnlyContain(status => status == HttpStatusCode.Unauthorized,
            because: "the first attempts are refused on the credentials, not by the limiter");
    }

    [Fact]
    public async Task RegisteringIsLimitedByTheSameAddressPolicy()
    {
        if (!_mongo.IsAvailable) throw new InvalidOperationException(_mongo.UnavailableReason);

        using var factory = CreateFactory(authPermits: 2);
        var client = factory.CreateClient();

        var statuses = new List<HttpStatusCode>();

        for (var attempt = 0; attempt < 4; attempt++)
        {
            using var response = await client.PostAsJsonAsync("/api/v1/auth/register", new
            {
                name = "Massa",
                email = $"massa-{Guid.NewGuid():N}@flow.test",
                password = "IntegrationTest1!"
            });

            statuses.Add(response.StatusCode);
        }

        statuses.Should().Contain(HttpStatusCode.TooManyRequests,
            because: "bulk account creation from one address is exactly what this policy is for");
    }

    // ─── Nothing else regressed ─────────────────────────────────────────────

    [Fact]
    public async Task AuthorizationStillRunsAndStillAnswers403()
    {
        if (!_mongo.IsAvailable) throw new InvalidOperationException(_mongo.UnavailableReason);

        using var factory = CreateFactory();
        var operatorClient = await factory.CreateClientAsAsync(UserRole.Operator);

        // The limiter now runs before authorization. Authorization still has to happen.
        using var response = await operatorClient.PostAsync(AiEndpoint, null);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AnonymousCallsToAProtectedEndpointStillAnswer401()
    {
        if (!_mongo.IsAvailable) throw new InvalidOperationException(_mongo.UnavailableReason);

        using var factory = CreateFactory();
        var anonymous = factory.CreateClient();

        using var response = await anonymous.PostAsync(AiEndpoint, null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            because: "moving the limiter behind authentication must not change who gets in");
    }

    [Fact]
    public async Task OrdinaryEndpointsAreUnaffectedByTheAiPolicy()
    {
        if (!_mongo.IsAvailable) throw new InvalidOperationException(_mongo.UnavailableReason);

        using var factory = CreateFactory(aiPermits: 1);
        var leadership = await factory.CreateClientAsAsync(UserRole.Leadership);

        // Spend the AI allowance, then confirm the rest of the API is untouched.
        await CallAiAsync(leadership, 3);

        for (var i = 0; i < 5; i++)
        {
            using var response = await leadership.GetAsync("/api/v1/guidelines");
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }
    }
}
