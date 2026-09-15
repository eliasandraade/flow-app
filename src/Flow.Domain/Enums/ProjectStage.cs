namespace Flow.Domain.Enums;

/// <summary>
/// Execution phase of a project. Orthogonal to <see cref="ProjectStatus"/>:
/// a Blocked project keeps the stage it had reached.
/// </summary>
public enum ProjectStage
{
    Discovery,
    Planning,
    Execution,
    Validation,
    Rollout
}
