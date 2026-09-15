using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Flow.Infrastructure.Observability;

/// <summary>Names used by the ActivitySource and Meter, registered once with OpenTelemetry.</summary>
public static class FlowTelemetry
{
    public const string ServiceName = "flow-api";
    public const string ServiceVersion = "2.0.0";
    public const string ActivitySourceName = "Flow";
    public const string MeterName = "Flow";

    public static readonly ActivitySource ActivitySource = new(ActivitySourceName, ServiceVersion);
}
