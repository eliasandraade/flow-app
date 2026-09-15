using System.Net;
using System.Net.Http.Json;
using Flow.API;
using Flow.Domain.Enums;
using Flow.Integration.Tests.Fixtures;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace Flow.Integration.Tests.Scenarios;

/// <summary>
/// Running behind Traefik.
///
/// TLS terminates at the proxy, so the application receives plain http from an address on
/// the container network. Two things depend on getting the real values back: the
/// authentication rate limiter partitions by client address — collapse that and everyone
/// behind the proxy shares one bucket, which is the whole internet — and HTTPS redirection
/// reads the scheme.
///
/// The other half is trust. A forwarded header is a header; anyone can send one. The tests
/// that matter most here are the ones proving a client cannot claim an address just by
/// asking.
/// </summary>
[Collection(MongoCollection.Name)]
public class ForwardedHeadersTests
{
    private readonly MongoFixture _mongo;

    public ForwardedHeadersTests(MongoFixture mongo) => _mongo = mongo;

    /// <summary>The proxy's own address on the container network.</summary>
    private const string ProxyAddress = "172.20.0.7";

    /// <summary>Somewhere else entirely: a client talking to the app directly.</summary>
    private const string DirectClientAddress = "198.51.100.44";

    private FlowApiFactory CreateFactory(
        string connectionAddress,
        bool enabled = true,
        string[]? trustedNetworks = null,
        string[]? trustedProxies = null,
        int authPermits = 3)
    {
        var configuration = new Dictionary<string, string?>
        {
            ["ForwardedHeaders:Enabled"] = enabled ? "true" : "false",
            ["ForwardedHeaders:ForwardLimit"] = "1",
            ["RateLimiting:Enabled"] = "true",
            ["RateLimiting:AuthPermitPerMinute"] = authPermits.ToString(),
            ["RateLimiting:AiPermitPerFiveMinutes"] = "3",
            ["RateLimiting:GlobalPermitPerMinute"] = "1000"
        };

        trustedNetworks ??= ["172.20.0.0/16"];

        for (var i = 0; i < trustedNetworks.Length; i++)
            configuration[$"ForwardedHeaders:TrustedNetworks:{i}"] = trustedNetworks[i];

        for (var i = 0; i < (trustedProxies?.Length ?? 0); i++)
            configuration[$"ForwardedHeaders:TrustedProxies:{i}"] = trustedProxies![i];

        return new FlowApiFactory(
            _mongo,
            overrideServices: services => services.AddSingleton<IStartupFilter>(
                new ProxyTestHarness(IPAddress.Parse(connectionAddress))),
            overrideConfiguration: configuration);
    }

    private static async Task<ProxyTestHarness.RequestAsSeen> AskAsync(
        HttpClient client, params (string Name, string Value)[] headers)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, ProxyTestHarness.EchoPath);

        foreach (var (name, value) in headers)
            request.Headers.TryAddWithoutValidation(name, value);

        using var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<ProxyTestHarness.RequestAsSeen>())!;
    }

    // ─── The proxy is believed ──────────────────────────────────────────────

    [Fact]
    public async Task ATrustedProxyCanReportTheRealClientAddress()
    {
        if (!_mongo.IsAvailable) throw new InvalidOperationException(_mongo.UnavailableReason);

        using var factory = CreateFactory(ProxyAddress);

        var seen = await AskAsync(factory.CreateClient(), ("X-Forwarded-For", "203.0.113.10"));

        seen.RemoteIp.Should().Be("203.0.113.10",
            because: "without this the application only ever sees the proxy");
    }

    [Fact]
    public async Task ATrustedProxyCanReportThatTheClientIsOnHttps()
    {
        if (!_mongo.IsAvailable) throw new InvalidOperationException(_mongo.UnavailableReason);

        using var factory = CreateFactory(ProxyAddress);

        var seen = await AskAsync(
            factory.CreateClient(),
            ("X-Forwarded-For", "203.0.113.10"),
            ("X-Forwarded-Proto", "https"));

        seen.Scheme.Should().Be("https",
            because: "TLS terminates at the proxy, and redirection logic reads this");
    }

    [Fact]
    public async Task AnExactTrustedProxyAddressWorksTheSameAsANetwork()
    {
        if (!_mongo.IsAvailable) throw new InvalidOperationException(_mongo.UnavailableReason);

        using var factory = CreateFactory(
            ProxyAddress, trustedNetworks: [], trustedProxies: [ProxyAddress]);

        var seen = await AskAsync(factory.CreateClient(), ("X-Forwarded-For", "203.0.113.10"));

        seen.RemoteIp.Should().Be("203.0.113.10");
    }

    // ─── Nobody else is ─────────────────────────────────────────────────────

    [Fact]
    public async Task AClientCannotClaimAnAddressWhenItDidNotComeThroughTheProxy()
    {
        if (!_mongo.IsAvailable) throw new InvalidOperationException(_mongo.UnavailableReason);

        // Same header, but the connection is from an address nobody trusts.
        using var factory = CreateFactory(DirectClientAddress);

        var seen = await AskAsync(
            factory.CreateClient(),
            ("X-Forwarded-For", "203.0.113.10"),
            ("X-Forwarded-Proto", "https"));

        seen.RemoteIp.Should().Be(DirectClientAddress,
            because: "a forwarded header is only evidence when it comes from a proxy we run");
        seen.Scheme.Should().Be("http",
            because: "an untrusted client must not be able to declare its own scheme either");
    }

    [Fact]
    public async Task WithTheFeatureOffForwardedHeadersAreIgnoredEntirely()
    {
        if (!_mongo.IsAvailable) throw new InvalidOperationException(_mongo.UnavailableReason);

        using var factory = CreateFactory(ProxyAddress, enabled: false);

        var seen = await AskAsync(
            factory.CreateClient(),
            ("X-Forwarded-For", "203.0.113.10"),
            ("X-Forwarded-Proto", "https"));

        seen.RemoteIp.Should().Be(ProxyAddress);
        seen.Scheme.Should().Be("http");
    }

    [Fact]
    public async Task OnlyTheRightmostHopIsTakenFromAChainOfForwardedAddresses()
    {
        if (!_mongo.IsAvailable) throw new InvalidOperationException(_mongo.UnavailableReason);

        using var factory = CreateFactory(ProxyAddress);

        // A client that prepends a fake entry to the chain. With one trusted hop, only the
        // value the proxy itself appended is read.
        var seen = await AskAsync(
            factory.CreateClient(),
            ("X-Forwarded-For", "10.9.9.9, 203.0.113.10"));

        seen.RemoteIp.Should().Be("203.0.113.10",
            because: "ForwardLimit is 1, so a spoofed earlier hop is never reached");
    }

    // ─── What the rate limiter does with it ─────────────────────────────────

    [Fact]
    public async Task TwoClientsBehindTheSameProxyDoNotShareTheAuthenticationBucket()
    {
        if (!_mongo.IsAvailable) throw new InvalidOperationException(_mongo.UnavailableReason);

        using var factory = CreateFactory(ProxyAddress, authPermits: 3);
        var client = factory.CreateClient();

        async Task<HttpStatusCode> LoginFromAsync(string clientAddress)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login")
            {
                Content = JsonContent.Create(new
                {
                    email = $"quem-{Guid.NewGuid():N}@flow.test",
                    password = "SenhaErrada1!"
                })
            };

            request.Headers.TryAddWithoutValidation("X-Forwarded-For", clientAddress);

            using var response = await client.SendAsync(request);
            return response.StatusCode;
        }

        // One client burns its whole allowance.
        for (var i = 0; i < 3; i++)
            (await LoginFromAsync("203.0.113.10")).Should().Be(HttpStatusCode.Unauthorized);

        (await LoginFromAsync("203.0.113.10")).Should().Be(HttpStatusCode.TooManyRequests);

        // A different client, same proxy, untouched allowance. Without forwarded headers
        // this is the request that would be refused for something it never did.
        (await LoginFromAsync("198.51.100.77")).Should().Be(HttpStatusCode.Unauthorized,
            because: "the bucket belongs to the client, not to the proxy in front of them");
    }

    [Fact]
    public async Task TheAiLimiterStillPartitionsByUserNotByForwardedAddress()
    {
        if (!_mongo.IsAvailable) throw new InvalidOperationException(_mongo.UnavailableReason);

        using var factory = CreateFactory(ProxyAddress);
        var leadership = await factory.CreateClientAsAsync(UserRole.Leadership);

        async Task<HttpStatusCode> InsightsFromAsync(string clientAddress)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/dashboard/insights");
            request.Headers.TryAddWithoutValidation("X-Forwarded-For", clientAddress);

            using var response = await leadership.SendAsync(request);
            return response.StatusCode;
        }

        // The same person moving between networks — a phone leaving wifi — keeps one quota.
        for (var i = 0; i < 3; i++)
            (await InsightsFromAsync($"203.0.113.{10 + i}")).Should().NotBe(HttpStatusCode.TooManyRequests);

        (await InsightsFromAsync("203.0.113.99")).Should().Be(HttpStatusCode.TooManyRequests,
            because: "changing address must not hand a user a fresh AI allowance");
    }

    // ─── Nothing else moved ─────────────────────────────────────────────────

    [Fact]
    public async Task AuthenticationAndAuthorizationAreUnaffected()
    {
        if (!_mongo.IsAvailable) throw new InvalidOperationException(_mongo.UnavailableReason);

        using var factory = CreateFactory(ProxyAddress);

        var anonymous = factory.CreateClient();
        anonymous.DefaultRequestHeaders.TryAddWithoutValidation("X-Forwarded-For", "203.0.113.10");

        (await anonymous.GetAsync("/api/v1/projects")).StatusCode
            .Should().Be(HttpStatusCode.Unauthorized);

        var operatorClient = await factory.CreateClientAsAsync(UserRole.Operator);
        operatorClient.DefaultRequestHeaders.TryAddWithoutValidation("X-Forwarded-For", "203.0.113.10");

        (await operatorClient.PostAsync("/api/v1/dashboard/insights", null)).StatusCode
            .Should().Be(HttpStatusCode.Forbidden);
    }

    // ─── The configuration refuses to be quietly useless ────────────────────

    [Theory]
    [InlineData("not-an-ip")]
    [InlineData("172.20.0.0")]
    [InlineData("172.20.0.0/99")]
    public void AMalformedTrustedNetworkIsRejectedRatherThanIgnored(string network)
    {
        var settings = new ForwardedHeadersSettings { Enabled = true, TrustedNetworks = [network] };

        var act = () => ForwardedHeadersSetup.Apply(
            new Microsoft.AspNetCore.Builder.ForwardedHeadersOptions(), settings);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void EnablingForwardedHeadersWithoutTrustingAnyoneIsRefusedOutsideDevelopment()
    {
        var settings = new ForwardedHeadersSettings { Enabled = true };

        // Silently trusting only loopback behind a container proxy would leave every client
        // in one rate-limit bucket while appearing to be configured.
        var act = () => ForwardedHeadersSetup.Validate(settings, isDevelopment: false);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*TrustedProxies*");

        // Development is allowed to run without a proxy at all.
        ForwardedHeadersSetup.Validate(settings, isDevelopment: true);
    }

    // ─── The shape Compose actually produces ────────────────────────────────
    //
    // docker-compose.yml materialises both ForwardedHeaders__TrustedNetworks__0 and
    // ForwardedHeaders__TrustedProxies__0 unconditionally, so whichever one the operator
    // did not set arrives as an empty string rather than as an absent key. The deployment
    // guide tells people to configure only TrustedNetworks behind Traefik, which means the
    // documented configuration is exactly the one that carries an empty sibling.
    //
    // An empty entry is the operator saying nothing. It must not be parsed, and it must not
    // count as a trusted source either — otherwise "no proxy configured" would look
    // configured and slip past the startup check.

    [Fact]
    public async Task TheConfigurationComposeProducesForTraefikIsAccepted()
    {
        if (!_mongo.IsAvailable) throw new InvalidOperationException(_mongo.UnavailableReason);

        // FORWARDED_TRUSTED_NETWORKS set, FORWARDED_TRUSTED_PROXIES left alone.
        using var factory = CreateFactory(
            ProxyAddress,
            trustedNetworks: ["172.16.0.0/12"],
            trustedProxies: [""]);

        var seen = await AskAsync(factory.CreateClient(), ("X-Forwarded-For", "203.0.113.10"));

        seen.RemoteIp.Should().Be("203.0.113.10",
            because: "the documented Traefik configuration must actually start and work");
    }

    [Fact]
    public async Task AnEmptyNetworkEntryDoesNotDiscardAConfiguredProxy()
    {
        if (!_mongo.IsAvailable) throw new InvalidOperationException(_mongo.UnavailableReason);

        // The mirror image: FORWARDED_TRUSTED_PROXIES set, FORWARDED_TRUSTED_NETWORKS not.
        using var factory = CreateFactory(
            ProxyAddress,
            trustedNetworks: [""],
            trustedProxies: [ProxyAddress]);

        var seen = await AskAsync(factory.CreateClient(), ("X-Forwarded-For", "203.0.113.10"));

        seen.RemoteIp.Should().Be("203.0.113.10");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    public void ABlankEntryIsNotATrustedSource(string? blank)
    {
        var settings = new ForwardedHeadersSettings
        {
            Enabled = true,
            TrustedNetworks = [blank!],
            TrustedProxies = [blank!]
        };

        settings.HasTrustedSources.Should().BeFalse(
            because: "an empty variable is an operator who configured nothing");

        var act = () => ForwardedHeadersSetup.Validate(settings, isDevelopment: false);

        act.Should().Throw<InvalidOperationException>(
            because: "this is the case the startup check exists for");
    }

    [Fact]
    public void BlankEntriesLeaveTheLoopbackDefaultsInPlaceRatherThanTrustingEveryone()
    {
        var options = new Microsoft.AspNetCore.Builder.ForwardedHeadersOptions();
        var defaults = new Microsoft.AspNetCore.Builder.ForwardedHeadersOptions();

        ForwardedHeadersSetup.Apply(options, new ForwardedHeadersSettings
        {
            Enabled = true,
            TrustedNetworks = [""],
            TrustedProxies = [""]
        });

        // Discarding an empty entry is not the same as deciding to believe anyone. Nothing
        // was configured, so nothing beyond the framework's own loopback default is trusted.
        options.KnownProxies.Should().HaveCount(defaults.KnownProxies.Count);
        options.KnownNetworks.Should().HaveCount(defaults.KnownNetworks.Count);
    }

    [Theory]
    [InlineData("banana")]
    [InlineData("172.16.0.0")]
    [InlineData("172.16.0.0/99")]
    public void ANonEmptyNetworkThatIsStillWrongKeepsFailing(string network)
    {
        // Ignoring blanks must not turn into ignoring mistakes: a typo in a real value has
        // to stop startup, not silently shrink the allow-list.
        var settings = new ForwardedHeadersSettings
        {
            Enabled = true,
            TrustedNetworks = ["", network]
        };

        var act = () => ForwardedHeadersSetup.Apply(
            new Microsoft.AspNetCore.Builder.ForwardedHeadersOptions(), settings);

        act.Should().Throw<InvalidOperationException>().WithMessage($"*{network}*");
    }

    [Theory]
    [InlineData("banana")]
    [InlineData("172.16.0.0/12")]
    public void ANonEmptyProxyThatIsStillWrongKeepsFailing(string proxy)
    {
        var settings = new ForwardedHeadersSettings
        {
            Enabled = true,
            TrustedProxies = [" ", proxy]
        };

        var act = () => ForwardedHeadersSetup.Apply(
            new Microsoft.AspNetCore.Builder.ForwardedHeadersOptions(), settings);

        act.Should().Throw<InvalidOperationException>().WithMessage($"*{proxy}*");
    }
}
