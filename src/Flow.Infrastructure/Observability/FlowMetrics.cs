using System.Diagnostics.Metrics;
using Flow.Application.Common.Interfaces;

namespace Flow.Infrastructure.Observability;

/// <summary>
/// Business metrics for the innovation pipeline.
///
/// Deliberately free of per-user labels: userId in a metric explodes cardinality and
/// breaks the backend long before it answers a useful question. Attribution per user
/// already lives in the audit log and in the traces.
/// </summary>
public sealed class FlowMetrics : IFlowMetrics
{
    private readonly Counter<long> _ideasSubmitted;
    private readonly Counter<long> _ideasApproved;
    private readonly Counter<long> _ideasRejected;
    private readonly Counter<long> _projectsCreated;
    private readonly Counter<long> _projectsBlocked;
    private readonly Counter<long> _projectsCompleted;
    private readonly Counter<long> _aiRequests;
    private readonly Counter<long> _aiFailures;
    private readonly Counter<long> _notificationsSent;
    private readonly Counter<long> _notificationsFailed;
    private readonly Histogram<double> _aiLatency;

    public FlowMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(FlowTelemetry.MeterName, FlowTelemetry.ServiceVersion);

        _ideasSubmitted = meter.CreateCounter<long>("flow_ideas_submitted");
        _ideasApproved = meter.CreateCounter<long>("flow_ideas_approved");
        _ideasRejected = meter.CreateCounter<long>("flow_ideas_rejected");
        _projectsCreated = meter.CreateCounter<long>("flow_projects_created");
        _projectsBlocked = meter.CreateCounter<long>("flow_projects_blocked");
        _projectsCompleted = meter.CreateCounter<long>("flow_projects_completed");
        _aiRequests = meter.CreateCounter<long>("flow_ai_requests");
        _aiFailures = meter.CreateCounter<long>("flow_ai_failures");
        _notificationsSent = meter.CreateCounter<long>("flow_notifications_sent");
        _notificationsFailed = meter.CreateCounter<long>("flow_notifications_failed");
        _aiLatency = meter.CreateHistogram<double>("flow_ai_latency_ms");
    }

    public void IdeaSubmitted() => _ideasSubmitted.Add(1);
    public void IdeaApproved() => _ideasApproved.Add(1);
    public void IdeaRejected() => _ideasRejected.Add(1);
    public void ProjectCreated() => _projectsCreated.Add(1);
    public void ProjectBlocked() => _projectsBlocked.Add(1);
    public void ProjectCompleted() => _projectsCompleted.Add(1);

    public void AiRequest(string operation) =>
        _aiRequests.Add(1, new KeyValuePair<string, object?>("operation", operation));

    public void AiFailure(string operation, string kind) => _aiFailures.Add(1,
        new KeyValuePair<string, object?>("operation", operation),
        new KeyValuePair<string, object?>("kind", kind));

    public void AiLatency(string operation, double milliseconds) =>
        _aiLatency.Record(milliseconds, new KeyValuePair<string, object?>("operation", operation));

    public void NotificationSent() => _notificationsSent.Add(1);
    public void NotificationFailed(string kind) =>
        _notificationsFailed.Add(1, new KeyValuePair<string, object?>("kind", kind));
}
