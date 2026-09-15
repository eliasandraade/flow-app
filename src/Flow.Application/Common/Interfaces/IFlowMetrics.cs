namespace Flow.Application.Common.Interfaces;

/// <summary>
/// Business metrics for the innovation pipeline.
///
/// Declared here so handlers can record what happened without the Application layer
/// knowing anything about OpenTelemetry. The counters answer questions the dashboard
/// cannot: the dashboard shows the current state, these show the rate of change.
///
/// No method takes a user identifier. Per-user labels explode metric cardinality, and
/// attribution already lives in the audit log and in the traces.
/// </summary>
public interface IFlowMetrics
{
    void IdeaSubmitted();
    void IdeaApproved();
    void IdeaRejected();

    void ProjectCreated();
    void ProjectBlocked();
    void ProjectCompleted();

    void AiRequest(string operation);
    void AiFailure(string operation, string kind);
    void AiLatency(string operation, double milliseconds);

    void NotificationSent();
    void NotificationFailed(string kind);
}

/// <summary>
/// Used where metrics are not configured — notably in unit tests, which should not need a
/// meter registered to exercise a handler.
/// </summary>
public sealed class NullFlowMetrics : IFlowMetrics
{
    public static readonly NullFlowMetrics Instance = new();

    public void IdeaSubmitted() { }
    public void IdeaApproved() { }
    public void IdeaRejected() { }
    public void ProjectCreated() { }
    public void ProjectBlocked() { }
    public void ProjectCompleted() { }
    public void AiRequest(string operation) { }
    public void AiFailure(string operation, string kind) { }
    public void AiLatency(string operation, double milliseconds) { }
    public void NotificationSent() { }
    public void NotificationFailed(string kind) { }
}
