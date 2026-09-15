namespace Flow.Application.Projects;

/// <summary>
/// The action names recorded on every project transition.
///
/// These strings are persisted in <c>audit_logs.action</c> and read back by the timeline,
/// so their values are part of the stored history and must not change. They live here
/// because the transition recorder also switches on them to emit business metrics, and a
/// typo in a literal at one of the ten call sites would produce a counter that silently
/// stays at zero.
/// </summary>
public static class ProjectActions
{
    public const string Created = "Created";
    public const string Updated = "Updated";
    public const string Started = "Started";
    public const string StageChanged = "StageChanged";
    public const string ProgressUpdated = "ProgressUpdated";
    public const string Blocked = "Blocked";
    public const string Unblocked = "Unblocked";
    public const string Completed = "Completed";
    public const string Cancelled = "Cancelled";
}
