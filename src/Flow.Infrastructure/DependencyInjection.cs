using Flow.Application.Common.Persistence;
using Flow.Application.Common.Interfaces;
using Flow.Domain.Entities;
using Flow.Application.Assistant;
using Flow.Infrastructure.Assistant;
using Flow.Infrastructure.Auth;
using Flow.Infrastructure.Identity;
using Flow.Infrastructure.Notifications;
using Flow.Infrastructure.Observability;
using Flow.Infrastructure.Persistence.Mongo;
using Flow.Infrastructure.Persistence.Mongo.Repositories;
using Flow.Infrastructure.Seeding;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MongoDB.Driver;
using MongoDB.Driver.Core.Extensions.DiagnosticSources;

namespace Flow.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddMongoPersistence(configuration);
        services.AddIdentityStores();

        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddSingleton<ICorrelationIdAccessor, ActivityCorrelationIdAccessor>();

        services.AddSingleton(sp =>
        {
            var options = configuration.GetSection(DemoSeedOptions.SectionName).Get<DemoSeedOptions>()
                ?? new DemoSeedOptions();

            // Environment variable wins so that a demo environment can set the password
            // without it ever living in a committed file.
            var fromEnvironment = Environment.GetEnvironmentVariable("SEED_DEMO_PASSWORD");
            if (!string.IsNullOrWhiteSpace(fromEnvironment)) options.Password = fromEnvironment;

            return options;
        });

        services.AddScoped<DemoDataSeeder>();

        services.AddNotifications(configuration);
        services.AddAssistant(configuration);

        return services;
    }

    private static IServiceCollection AddAssistant(
        this IServiceCollection services, IConfiguration configuration)
    {
        // One clock for the whole application, so anything time-dependent can be tested by
        // moving it instead of by waiting.
        services.TryAddSingleton(TimeProvider.System);

        services.Configure<GeminiOptions>(configuration.GetSection(GeminiOptions.SectionName));

        // The breaker holds state across requests, so it has to outlive them.
        services.AddSingleton<CircuitBreaker>();
        services.AddSingleton<GeminiStructuredClient>();

        services.AddScoped<IInnovationAssistant, GeminiInnovationAssistant>();
        services.AddScoped<IExecutiveInsightService, GeminiExecutiveInsightService>();

        return services;
    }

    private static IServiceCollection AddNotifications(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<OneSignalOptions>(configuration.GetSection(OneSignalOptions.SectionName));
        services.Configure<OutboxOptions>(configuration.GetSection(OutboxOptions.SectionName));

        var oneSignal = configuration.GetSection(OneSignalOptions.SectionName).Get<OneSignalOptions>()
            ?? new OneSignalOptions();

        services
            .AddHttpClient<IPushNotificationSender, OneSignalPushSender>(
                OneSignalPushSender.HttpClientName, client =>
                {
                    client.BaseAddress = new Uri(oneSignal.BaseUrl);
                    client.Timeout = oneSignal.Timeout;
                })
            // Retrying a push is safe: delivery is idempotent through the dedupe key, both
            // in our outbox and on the provider side.
            .AddStandardResilienceHandler(options =>
            {
                options.Retry.MaxRetryAttempts = 2;
                options.Retry.UseJitter = true;
                options.AttemptTimeout.Timeout = oneSignal.Timeout;
                options.TotalRequestTimeout.Timeout = oneSignal.Timeout * 4;
                options.CircuitBreaker.SamplingDuration = oneSignal.Timeout * 8;
            });

        services.AddHostedService<OutboxDispatcherHostedService>();

        return services;
    }

    private static IServiceCollection AddMongoPersistence(
        this IServiceCollection services, IConfiguration configuration)
    {
        // Serializer registration is global in the driver and must happen before the first
        // serialisation, so it runs here rather than lazily on first use.
        MongoMapping.Register();

        services.Configure<MongoOptions>(configuration.GetSection(MongoOptions.SectionName));

        services.AddSingleton<IMongoClient>(sp =>
        {
            var options = configuration.GetSection(MongoOptions.SectionName).Get<MongoOptions>()
                ?? new MongoOptions();

            var settings = MongoClientSettings.FromConnectionString(options.ConnectionString);

            // Emits ActivitySource spans that OpenTelemetry picks up, so database calls
            // appear inside the request trace instead of as unexplained latency.
            settings.ClusterConfigurator = cb =>
                cb.Subscribe(new DiagnosticsActivityEventSubscriber(
                    new InstrumentationOptions { CaptureCommandText = false }));

            return new MongoClient(settings);
        });

        services.AddSingleton(sp =>
        {
            var options = configuration.GetSection(MongoOptions.SectionName).Get<MongoOptions>()
                ?? new MongoOptions();

            return new FlowMongoContext(sp.GetRequiredService<IMongoClient>(), options.Database);
        });

        services.AddSingleton<MongoIndexInitializer>();

        // The transaction session is per request scope; the client and context are not.
        services.AddScoped<MongoSessionAccessor>();
        services.AddScoped<IUnitOfWork, MongoUnitOfWork>();

        services.AddScoped<IUserRepository, MongoUserRepository>();
        services.AddScoped<IRefreshTokenRepository, MongoRefreshTokenRepository>();
        services.AddScoped<IGuidelineRepository, MongoGuidelineRepository>();
        services.AddScoped<IGuidelineHistoryRepository, MongoGuidelineHistoryRepository>();
        services.AddScoped<IIdeaRepository, MongoIdeaRepository>();
        services.AddScoped<IIdeaCommentRepository, MongoIdeaCommentRepository>();
        services.AddScoped<IProjectRepository, MongoProjectRepository>();
        services.AddScoped<IProjectSnapshotRepository, MongoProjectSnapshotRepository>();
        services.AddScoped<IResultRepository, MongoResultRepository>();
        services.AddScoped<IPointLedgerRepository, MongoPointLedgerRepository>();
        services.AddScoped<IAuditLogRepository, MongoAuditLogRepository>();
        services.AddScoped<INotificationRepository, MongoNotificationRepository>();
        services.AddScoped<IOutboxRepository, MongoOutboxRepository>();
        services.AddScoped<IAssistantRunRepository, MongoAssistantRunRepository>();
        services.AddScoped<IDashboardReadRepository, MongoDashboardReadRepository>();

        return services;
    }

    private static IServiceCollection AddIdentityStores(this IServiceCollection services)
    {
        services.AddScoped<IUserStore<User>, MongoUserStore>();
        services.AddScoped<IRoleStore<Role>, MongoRoleStore>();

        services
            .AddIdentityCore<User>(options =>
            {
                options.Password.RequireDigit = true;
                options.Password.RequiredLength = 8;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequireUppercase = false;
                options.User.RequireUniqueEmail = true;
            })
            .AddRoles<Role>();

        return services;
    }
}
