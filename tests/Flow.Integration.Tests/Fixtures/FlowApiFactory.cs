using System.Net.Http.Headers;
using System.Net.Http.Json;
using Flow.Domain.Entities;
using Flow.Domain.Enums;
using Flow.Infrastructure.Persistence.Mongo;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Flow.Integration.Tests.Fixtures;

/// <summary>
/// Boots the real API against the throwaway MongoDB. Nothing is substituted: the same
/// composition root, the same Mongo repositories, the same Identity stores.
/// </summary>
public sealed class FlowApiFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString;
    private readonly string _databaseName;

    private readonly Action<IServiceCollection>? _overrideServices;
    private readonly Dictionary<string, string?>? _overrideConfiguration;

    public FlowApiFactory(
        MongoFixture mongo,
        Action<IServiceCollection>? overrideServices = null,
        Dictionary<string, string?>? overrideConfiguration = null)
    {
        _connectionString = mongo.ConnectionString;
        // A database per factory keeps parallel test classes from seeing each other's data.
        _databaseName = $"{mongo.DatabaseName}_{Guid.NewGuid():N}"[..26];
        _overrideServices = overrideServices;
        _overrideConfiguration = overrideConfiguration;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        // Used only to inject a deliberate failure and prove the transaction rolls back.
        if (_overrideServices is not null) builder.ConfigureTestServices(_overrideServices);

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Mongo:ConnectionString"] = _connectionString,
                ["Mongo:Database"] = _databaseName,
                ["Mongo:EnsureIndexes"] = "true",
                ["JwtSettings:SecretKey"] = "integration-test-secret-key-at-least-32-bytes-long",
                ["JwtSettings:Issuer"] = "FlowAPI",
                ["JwtSettings:Audience"] = "FlowApp",
                ["JwtSettings:ExpiryMinutes"] = "15",
                ["Auth:RefreshTokenDays"] = "7",
                ["SEED_DEMO_DATA"] = "false",
                // The suite drives hundreds of requests from a single address. The limiter
                // is exercised by its own test rather than fought by every other one.
                ["RateLimiting:Enabled"] = "false",
                ["Swagger:Enabled"] = "false"
            });

            // Applied last so a test can override any of the defaults above. Program reads
            // some settings straight from configuration, so overriding them in DI would
            // have no effect.
            if (_overrideConfiguration is not null)
                config.AddInMemoryCollection(_overrideConfiguration);
        });
    }

    public FlowMongoContext Mongo => Services.GetRequiredService<FlowMongoContext>();

    /// <summary>Creates a user with the given role and returns a client already authenticated as them.</summary>
    public async Task<HttpClient> CreateClientAsAsync(UserRole role, string? email = null)
    {
        var (client, _) = await CreateClientWithUserAsync(role, email);
        return client;
    }

    public async Task<(HttpClient Client, Guid UserId)> CreateClientWithUserAsync(
        UserRole role, string? email = null)
    {
        email ??= $"{role.ToString().ToLowerInvariant()}-{Guid.NewGuid():N}@flow.test";
        const string password = "IntegrationTest1!";

        using (var scope = Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();

            var user = User.Create($"Test {role}", email, role);
            var created = await userManager.CreateAsync(user, password);

            if (!created.Succeeded)
            {
                throw new InvalidOperationException(
                    "Failed to create test user: "
                    + string.Join(", ", created.Errors.Select(e => e.Description)));
            }

            await userManager.AddToRoleAsync(user, role.ToString());
        }

        var client = CreateClient();
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password });
        login.EnsureSuccessStatusCode();

        var auth = await login.Content.ReadFromJsonAsync<AuthResponse>()
            ?? throw new InvalidOperationException("Login returned no payload.");

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        return (client, auth.UserId);
    }

    public async Task<AuthResponse> LoginAsync(HttpClient client, string email, string password)
    {
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password });
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<AuthResponse>()
            ?? throw new InvalidOperationException("Login returned no payload.");
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !string.IsNullOrEmpty(_connectionString))
        {
            try
            {
                Services.GetRequiredService<FlowMongoContext>().Client.DropDatabase(_databaseName);
            }
            catch
            {
                // A leftover test database is noise, not a failure worth masking the run over.
            }
        }

        base.Dispose(disposing);
    }

    public sealed record AuthResponse(
        string AccessToken, string RefreshToken, Guid UserId, string Name, string Email, string Role);
}
