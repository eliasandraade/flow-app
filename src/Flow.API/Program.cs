using System.Diagnostics;
using System.Threading.RateLimiting;
using Flow.API;
using Flow.API.Middleware;
using Flow.API.Services;
using Flow.Application;
using Flow.Application.Auth;
using Flow.Application.Common.Interfaces;
using Flow.Infrastructure;
using Flow.Infrastructure.Observability;
using Flow.Infrastructure.Persistence.Mongo;
using Flow.Infrastructure.Seeding;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// Logging — structured from the first line, so startup failures are readable too.
// ---------------------------------------------------------------------------
builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .Enrich.WithMachineName()
    .Enrich.WithProperty("service", FlowTelemetry.ServiceName)
    .WriteTo.Console(outputTemplate:
        "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}"));

// ---------------------------------------------------------------------------
// Application composition
// ---------------------------------------------------------------------------
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.Configure<AuthSettings>(builder.Configuration.GetSection("Auth"));

// ---------------------------------------------------------------------------
// Authentication
// ---------------------------------------------------------------------------
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var jwtSecret = jwtSettings["SecretKey"]
    ?? throw new InvalidOperationException("JwtSettings:SecretKey is missing from configuration.");

if (!builder.Environment.IsDevelopment())
{
    if (jwtSecret.StartsWith("CHANGE-THIS", StringComparison.OrdinalIgnoreCase))
        throw new InvalidOperationException(
            "JwtSettings:SecretKey must be replaced with a secure value before running outside of Development.");

    // HMAC-SHA256 keys shorter than the hash output weaken the signature, and a short
    // secret is the single easiest production mistake to make here.
    if (System.Text.Encoding.UTF8.GetByteCount(jwtSecret) < 32)
        throw new InvalidOperationException(
            "JwtSettings:SecretKey must be at least 32 bytes long.");
}

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer();

builder.Services.ConfigureOptions<JwtBearerOptionsSetup>();
builder.Services.AddAuthorization();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

// ---------------------------------------------------------------------------
// Forwarded headers — behind Traefik the TCP peer is the proxy, not the user.
// ---------------------------------------------------------------------------
builder.Services.Configure<ForwardedHeadersSettings>(
    builder.Configuration.GetSection(ForwardedHeadersSettings.SectionName));

// Bound through options rather than read from builder.Configuration here, and for the same
// reason the rate limits are: configuration supplied by a host that wraps this one — a test
// host, for instance — is not visible before Build(). A proxy setting that silently fails
// to apply is precisely the failure this whole section exists to prevent.
builder.Services.AddOptions<ForwardedHeadersOptions>()
    .Configure<IOptions<ForwardedHeadersSettings>>(
        (options, settings) => ForwardedHeadersSetup.Apply(options, settings.Value));

// ---------------------------------------------------------------------------
// CORS — explicit origins only. A wildcard would be a silent invitation.
// ---------------------------------------------------------------------------
const string CorsPolicy = "flow-clients";

// Accepts either the structured Cors:AllowedOrigins array or a comma-separated
// CORS_ALLOWED_ORIGINS variable, because a list is awkward to express as an env var and
// containers only have env vars.
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

var originsFromEnvironment = builder.Configuration["CORS_ALLOWED_ORIGINS"];
if (!string.IsNullOrWhiteSpace(originsFromEnvironment))
{
    allowedOrigins = originsFromEnvironment
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .ToArray();
}

builder.Services.AddCors(options => options.AddPolicy(CorsPolicy, policy =>
{
    if (allowedOrigins.Length > 0)
        policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod().AllowCredentials();
    else
        // Development convenience only; production configuration must list its origins.
        policy.SetIsOriginAllowed(_ => builder.Environment.IsDevelopment())
              .AllowAnyHeader().AllowAnyMethod();
}));

// ---------------------------------------------------------------------------
// Rate limiting — authentication and the AI endpoints are the two surfaces where
// abuse is cheap for the attacker and expensive for us.
// ---------------------------------------------------------------------------
builder.Services.Configure<RateLimitOptions>(
    builder.Configuration.GetSection(RateLimitOptions.SectionName));

// Limits are resolved per request from options rather than captured here, because code
// that reads builder.Configuration before Build() cannot see configuration supplied by a
// test host — and a control that silently ignores its configuration is worse than none.
static RateLimitOptions LimitsFor(HttpContext http) =>
    http.RequestServices.GetRequiredService<IOptionsMonitor<RateLimitOptions>>().CurrentValue;

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy(RateLimitPolicies.Auth, http =>
        !LimitsFor(http).Enabled
            ? RateLimitPartition.GetNoLimiter<string>("disabled")
            : RateLimitPartition.GetFixedWindowLimiter(
                http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = LimitsFor(http).AuthPermitPerMinute,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0
                }));

    // Partitioned by user rather than by address: these calls cost real money, and one
    // user behind a shared NAT should not consume everyone else's budget.
    //
    // Two things have to be right for that to work, and both were wrong. The limiter has
    // to run after authentication, or http.User is still anonymous when the key is
    // computed; and the id has to be read the way the token actually arrives, because the
    // bearer handler maps "sub" onto NameIdentifier and a lookup for "sub" alone comes
    // back empty even on an authenticated request. Either mistake silently collapses every
    // user behind one address into a single bucket.
    options.AddPolicy(RateLimitPolicies.Ai, http =>
        !LimitsFor(http).Enabled
            ? RateLimitPartition.GetNoLimiter<string>("disabled")
            : RateLimitPartition.GetFixedWindowLimiter(
                UserIdentity.IdOf(http.User) is { } userId
                    ? $"user:{userId}"
                    : $"ip:{http.Connection.RemoteIpAddress?.ToString() ?? "unknown"}",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = LimitsFor(http).AiPermitPerFiveMinutes,
                    Window = TimeSpan.FromMinutes(5),
                    QueueLimit = 0
                }));

    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(http =>
        LimitsFor(http).Enabled
            ? RateLimitPartition.GetFixedWindowLimiter(
                http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = LimitsFor(http).GlobalPermitPerMinute,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0
                })
            : RateLimitPartition.GetNoLimiter("disabled"));
});

// ---------------------------------------------------------------------------
// Observability
// ---------------------------------------------------------------------------
var otlpEndpoint = builder.Configuration["OpenTelemetry:OtlpEndpoint"];

builder.Services.AddSingleton<FlowMetrics>();
builder.Services.AddSingleton<IFlowMetrics>(sp => sp.GetRequiredService<FlowMetrics>());

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService(
        serviceName: FlowTelemetry.ServiceName,
        serviceVersion: FlowTelemetry.ServiceVersion))
    .WithTracing(tracing =>
    {
        tracing
            .AddAspNetCoreInstrumentation(o => o.Filter = ctx =>
                !ctx.Request.Path.StartsWithSegments("/health"))
            .AddHttpClientInstrumentation()
            .AddSource(FlowTelemetry.ActivitySourceName)
            // Emitted by the MongoDB driver's diagnostics subscriber.
            .AddSource("MongoDB.Driver.Core.Extensions.DiagnosticSources");

        if (!string.IsNullOrWhiteSpace(otlpEndpoint))
            tracing.AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint));
    })
    .WithMetrics(metrics =>
    {
        metrics
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddRuntimeInstrumentation()
            .AddMeter(FlowTelemetry.MeterName);

        if (!string.IsNullOrWhiteSpace(otlpEndpoint))
            metrics.AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint));
    });

// ---------------------------------------------------------------------------
// Health checks
// ---------------------------------------------------------------------------
var mongoConnectionString = builder.Configuration["Mongo:ConnectionString"]
    ?? "mongodb://localhost:27017/?replicaSet=rs0";

builder.Services.AddHealthChecks()
    .AddMongoDb(
        clientFactory: sp => sp.GetRequiredService<MongoDB.Driver.IMongoClient>(),
        databaseNameFactory: _ => builder.Configuration["Mongo:Database"] ?? "flow",
        name: "mongodb",
        tags: ["ready"]);

// ---------------------------------------------------------------------------
// HTTP API
// ---------------------------------------------------------------------------
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(
            new System.Text.Json.Serialization.JsonStringEnumConverter());
    })
    .ConfigureApiBehaviorOptions(options =>
    {
        // FluentValidation is the single validation authority, and it answers 422 with a
        // traceId. Model binding still rejects malformed JSON and wrong types before any
        // handler runs, and without this those come back as a 400 in a different shape —
        // two contracts for the same class of problem. This makes binding failures speak
        // the same language as everything else.
        options.InvalidModelStateResponseFactory = context =>
        {
            var errors = context.ModelState
                .Where(entry => entry.Value?.Errors.Count > 0)
                .ToDictionary(
                    entry => entry.Key,
                    entry => entry.Value!.Errors.Select(error => error.ErrorMessage).ToArray());

            var problem = new ProblemDetails
            {
                Title = "Validation failed",
                Status = StatusCodes.Status422UnprocessableEntity,
                Type = "https://httpstatuses.io/422",
                Instance = context.HttpContext.Request.Path
            };

            problem.Extensions["errors"] = errors;
            problem.Extensions["traceId"] =
                Activity.Current?.TraceId.ToString() ?? context.HttpContext.TraceIdentifier;

            return new ObjectResult(problem)
            {
                StatusCode = StatusCodes.Status422UnprocessableEntity,
                ContentTypes = { "application/problem+json" }
            };
        };
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(SwaggerConfiguration.Configure);

var app = builder.Build();

app.UseSerilogRequestLogging(options =>
{
    options.GetLevel = (http, _, ex) =>
        ex is not null ? Serilog.Events.LogEventLevel.Error
        : http.Response.StatusCode >= 500 ? Serilog.Events.LogEventLevel.Error
        : http.Request.Path.StartsWithSegments("/health") ? Serilog.Events.LogEventLevel.Verbose
        : Serilog.Events.LogEventLevel.Information;
});

// First in the pipeline, before anything reads the address or the scheme: HTTPS
// redirection, the rate limiter and the request log all have to see the client rather than
// the proxy.
var forwardedSettings = app.Services
    .GetRequiredService<IOptions<ForwardedHeadersSettings>>().Value;

ForwardedHeadersSetup.Validate(forwardedSettings, app.Environment.IsDevelopment());

if (forwardedSettings.Enabled)
    app.UseForwardedHeaders();

app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("Swagger:Enabled"))
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Flow API v1"));
}

if (!app.Environment.IsDevelopment())
    app.UseHttpsRedirection();

// Order matters here, and not only for style.
//
// UseCors, UseAuthentication and UseAuthorization must appear in that order — that is a
// framework requirement. UseRateLimiter has to come after UseRouting because the policies
// are selected by endpoint attributes, and it is placed after UseAuthentication so the AI
// policy can see who is calling: before it, http.User is anonymous and every user behind
// one address shares a bucket.
//
// It sits before UseAuthorization on purpose: a caller hammering an endpoint they are not
// allowed to use should still meet the limiter, rather than being waved through to a 403
// on every attempt.
app.UseRouting();
app.UseCors(CorsPolicy);
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();
app.MapControllers();

// Liveness answers "is the process up"; readiness answers "can it actually serve".
// Keeping them apart stops an orchestrator from killing a healthy pod during a brief
// database blip.
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false,
    ResponseWriter = HealthResponseWriter.WriteAsync
});

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
    ResponseWriter = HealthResponseWriter.WriteAsync
});

await app.InitialiseDatabaseAsync();

app.Run();

/// <summary>Exposed so the integration test host can reference the entry point assembly.</summary>
public partial class Program { }
