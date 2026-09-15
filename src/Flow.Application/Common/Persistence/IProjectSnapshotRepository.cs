using Flow.Domain.Entities;

namespace Flow.Application.Common.Persistence;

/// <summary>Append-only by construction, like the audit log.</summary>
public interface IProjectSnapshotRepository
{
    Task AppendAsync(ProjectSnapshot snapshot, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProjectSnapshot>> GetForProjectAsync(
        Guid projectId, CancellationToken cancellationToken = default);
}
