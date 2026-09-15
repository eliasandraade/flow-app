using System.Net;
using System.Net.Http.Json;
using Flow.Integration.Tests.Fixtures;
using FluentAssertions;

namespace Flow.Integration.Tests.Scenarios;

/// <summary>
/// The rest of the suite runs with the limiter off so it is not fighting a production
/// control on every request. This class turns it back on and proves it works.
/// </summary>
[Collection(MongoCollection.Name)]
public class RateLimitTests
{
    private readonly MongoFixture _mongo;

    public RateLimitTests(MongoFixture mongo) => _mongo = mongo;

    [Fact]
    public async Task RepeatedLoginAttempts_AreEventuallyRefusedWith429()
    {
        if (!_mongo.IsAvailable) throw new InvalidOperationException(_mongo.UnavailableReason);

        using var factory = new FlowApiFactory(_mongo, overrideConfiguration: new()
        {
            ["RateLimiting:Enabled"] = "true",
            ["RateLimiting:AuthPermitPerMinute"] = "3",
        });

        var client = factory.CreateClient();

        var statuses = new List<HttpStatusCode>();

        for (var attempt = 0; attempt < 6; attempt++)
        {
            var response = await client.PostAsJsonAsync("/api/v1/auth/login",
                new { email = "quem@flow.test", password = "SenhaErrada1!" });

            statuses.Add(response.StatusCode);
        }

        statuses.Should().Contain(HttpStatusCode.TooManyRequests,
            because: "credential stuffing must hit a wall well before it becomes useful");

        // The refusal has to come from the limiter, not from the credentials being wrong:
        // the first attempts get the ordinary authentication failure.
        statuses.Take(3).Should().OnlyContain(status => status == HttpStatusCode.Unauthorized);
    }
}
