using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace Flow.Integration.Tests.Fixtures;

/// <summary>
/// Lets a test pretend the request arrived from a particular machine, and then read back
/// what the pipeline concluded about it.
///
/// Both halves are needed to test forwarded headers honestly. The test host reports no
/// remote address at all, so without the first half nothing would ever match a trusted
/// proxy and the middleware would be a no-op. And the values under test — the client
/// address and the scheme — are not visible in any response the API produces, so without
/// the second half the only thing left to assert would be that Program.cs contains a line
/// of code, which proves nothing about behaviour.
///
/// This is test scaffolding, not product surface: it is registered only by the test host
/// and adds nothing to the application.
/// </summary>
public sealed class ProxyTestHarness : IStartupFilter
{
    /// <summary>Path the echo answers on. Chosen to be one no controller could claim.</summary>
    public const string EchoPath = "/__test__/request-as-seen";

    private readonly IPAddress? _connectionAddress;

    public ProxyTestHarness(IPAddress? connectionAddress) => _connectionAddress = connectionAddress;

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        // Before everything, including UseForwardedHeaders: this stands in for the TCP peer,
        // which is the value the middleware checks against the trusted list.
        if (_connectionAddress is not null)
        {
            app.Use(async (context, nextMiddleware) =>
            {
                context.Connection.RemoteIpAddress = _connectionAddress;
                await nextMiddleware();
            });
        }

        next(app);

        // After everything: no endpoint claims this path, so the request falls through to
        // here carrying whatever the pipeline decided.
        app.Use(async (context, nextMiddleware) =>
        {
            if (!context.Request.Path.Equals(EchoPath, StringComparison.Ordinal))
            {
                await nextMiddleware();
                return;
            }

            context.Response.ContentType = "application/json";

            await context.Response.WriteAsync(JsonSerializer.Serialize(new RequestAsSeen(
                Scheme: context.Request.Scheme,
                RemoteIp: context.Connection.RemoteIpAddress?.ToString(),
                Host: context.Request.Host.Value)));
        });
    };

    public sealed record RequestAsSeen(string Scheme, string? RemoteIp, string? Host);
}
