using Flow.Application.Common.Interfaces;
using Flow.Application.Common.Persistence;
using Flow.Application.Common.Services;
using Flow.Domain.Entities;

namespace Flow.Application.Projects;

/// <summary>
/// Persists a project transition together with everything governance requires: the
/// aggregate itself, a full immutable snapshot, and the audit entry — all inside one
/// transaction, plus any notifications the caller wants published in the same unit.
///
/// Every transition handler used to repeat this trio by hand. Centralising it means a new
/// transition cannot ship with a missing snapshot, which is the failure mode that quietly
/// destroys an audit trail.
/// </summary>
public sealed class ProjectTransitionRecorder
{
    private readonly IProjectRepository _projects;
    private readonly IProjectSnapshotRepository _snapshots;
    private readonly IUnitOfWork _unitOfWork;
    private readonly AuditTrail _audit;
    private readonly IFlowMetrics _metrics;

    public ProjectTransitionRecorder(
        IProjectRepository projects,
        IProjectSnapshotRepository snapshots,
        IUnitOfWork unitOfWork,
        AuditTrail audit,
        IFlowMetrics metrics)
    {
        _projects = projects;
        _snapshots = snapshots;
        _unitOfWork = unitOfWork;
        _audit = audit;
        _metrics = metrics;
    }

    public Guid ActorId => _audit.ActorId;
    public string ActorName => _audit.ActorName;

    /// <param name="isNew">True when the project is being created rather than updated.</param>
    /// <param name="alsoInTransaction">
    /// Extra writes that must share the transaction, typically notifications.
    /// </param>
    public async Task RecordAsync(
        Project project,
        string action,
        string? previousValue = null,
        string? newValue = null,
        string? reason = null,
        bool isNew = false,
        Func<CancellationToken, Task>? alsoInTransaction = null,
        CancellationToken cancellationToken = default)
    {
        var actorId = _audit.ActorId;

        await _unitOfWork.ExecuteAsync(async ct =>
        {
            if (isNew)
                await _projects.AddAsync(project, ct);
            else
                await _projects.UpdateAsync(project, ct);

            await _snapshots.AppendAsync(
                ProjectSnapshot.Create(project, action, actorId), ct);

            await _audit.RecordAsync(
                nameof(Project), project.Id, action,
                oldValue: previousValue,
                newValue: newValue ?? project.Status.ToString(),
                reason: reason,
                cancellationToken: ct);

            if (alsoInTransaction is not null)
                await alsoInTransaction(ct);
        }, cancellationToken);

        // Recorded after the commit: a rolled-back transition never happened, and a
        // counter that includes failures is worse than no counter.
        switch (action)
        {
            case ProjectActions.Created: _metrics.ProjectCreated(); break;
            case ProjectActions.Blocked: _metrics.ProjectBlocked(); break;
            case ProjectActions.Completed: _metrics.ProjectCompleted(); break;
        }
    }
}
