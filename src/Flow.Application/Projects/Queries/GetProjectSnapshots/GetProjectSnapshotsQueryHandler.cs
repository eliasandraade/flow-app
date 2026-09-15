using Flow.Application.Common.Exceptions;
using Flow.Application.Common.Persistence;
using MediatR;

namespace Flow.Application.Projects.Queries.GetProjectSnapshots;

public class GetProjectSnapshotsQueryHandler
    : IRequestHandler<GetProjectSnapshotsQuery, IReadOnlyList<ProjectSnapshotDto>>
{
    private readonly IProjectRepository _projects;
    private readonly IProjectSnapshotRepository _snapshots;

    public GetProjectSnapshotsQueryHandler(
        IProjectRepository projects, IProjectSnapshotRepository snapshots)
    {
        _projects = projects;
        _snapshots = snapshots;
    }

    public async Task<IReadOnlyList<ProjectSnapshotDto>> Handle(
        GetProjectSnapshotsQuery request, CancellationToken cancellationToken)
    {
        _ = await _projects.GetByIdAsync(request.ProjectId, cancellationToken)
            ?? throw new NotFoundException("Project", request.ProjectId);

        var snapshots = await _snapshots.GetForProjectAsync(request.ProjectId, cancellationToken);
        return snapshots.Select(ProjectSnapshotDto.From).ToList();
    }
}
