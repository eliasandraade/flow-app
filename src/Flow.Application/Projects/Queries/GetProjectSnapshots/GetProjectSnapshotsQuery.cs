using MediatR;

namespace Flow.Application.Projects.Queries.GetProjectSnapshots;

public record GetProjectSnapshotsQuery(Guid ProjectId) : IRequest<IReadOnlyList<ProjectSnapshotDto>>;
