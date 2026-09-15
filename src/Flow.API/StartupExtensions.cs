using Flow.Domain.Entities;
using Flow.Domain.Enums;
using Flow.Infrastructure.Identity;
using Flow.Infrastructure.Persistence.Mongo;
using Flow.Infrastructure.Seeding;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Flow.API;

public static class StartupExtensions
{
    /// <summary>
    /// Prepares the database at startup: indexes, the fixed set of Identity roles, and
    /// the demo dataset when it is explicitly asked for.
    ///
    /// MongoDB needs no schema migration, but it does need indexes, so this is the
    /// equivalent of running migrations before serving traffic. All of it is idempotent.
    /// </summary>
    public static async Task InitialiseDatabaseAsync(this WebApplication app)
    {
        var logger = app.Services.GetRequiredService<ILogger<Program>>();

        try
        {
            using var scope = app.Services.CreateScope();
            var services = scope.ServiceProvider;

            var mongoOptions = services.GetRequiredService<IOptions<MongoOptions>>().Value;

            if (mongoOptions.EnsureIndexes)
            {
                await services.GetRequiredService<MongoIndexInitializer>().EnsureIndexesAsync();
            }

            await SeedRolesAsync(services, logger);

            // Demo data never appears unless someone turns it on. Production must never
            // ship fabricated records.
            var seedDemo = app.Configuration.GetValue<bool>("SEED_DEMO_DATA")
                || string.Equals(
                    Environment.GetEnvironmentVariable("SEED_DEMO_DATA"), "true",
                    StringComparison.OrdinalIgnoreCase);

            if (seedDemo)
            {
                logger.LogWarning("SEED_DEMO_DATA is enabled. Seeding the demonstration dataset.");
                await services.GetRequiredService<DemoDataSeeder>().SeedAsync();
            }
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "Application failed to initialise the database. Startup aborted.");
            throw;
        }
    }

    private static async Task SeedRolesAsync(IServiceProvider services, ILogger logger)
    {
        var roleManager = services.GetRequiredService<RoleManager<Role>>();

        foreach (var role in Enum.GetNames<UserRole>())
        {
            if (await roleManager.RoleExistsAsync(role)) continue;

            var result = await roleManager.CreateAsync(Role.Create(role));
            if (!result.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Failed to create role '{role}': "
                    + string.Join(", ", result.Errors.Select(e => e.Description)));
            }

            logger.LogInformation("Created Identity role {Role}.", role);
        }
    }
}
