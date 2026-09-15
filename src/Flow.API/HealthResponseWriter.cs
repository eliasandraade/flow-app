using System.Text.Json;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Flow.API;

/// <summary>
/// Health payload that names the failing dependency. A bare "Unhealthy" tells an
/// on-call engineer nothing they can act on.
/// </summary>
public static class HealthResponseWriter
{
    private static readonly JsonSerializerOptions Options =
        new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public static Task WriteAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json";

        var payload = new
        {
            status = report.Status.ToString(),
            totalDurationMs = report.TotalDuration.TotalMilliseconds,
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                durationMs = e.Value.Duration.TotalMilliseconds,
                // The exception message is deliberately omitted: it can leak connection
                // strings and internal hostnames to anyone who can reach the endpoint.
                description = e.Value.Description
            })
        };

        return context.Response.WriteAsync(JsonSerializer.Serialize(payload, Options));
    }
}
