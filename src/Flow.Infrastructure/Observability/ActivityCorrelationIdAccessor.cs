using System.Diagnostics;
using Flow.Application.Common.Interfaces;

namespace Flow.Infrastructure.Observability;

/// <summary>
/// Uses the ambient Activity trace id as the correlation id.
///
/// Taking it from Activity rather than from HttpContext means audit entries written by a
/// background worker are correlated too, and it keeps this out of the HTTP layer.
/// </summary>
public sealed class ActivityCorrelationIdAccessor : ICorrelationIdAccessor
{
    public string? CorrelationId => Activity.Current?.TraceId.ToString();
}
