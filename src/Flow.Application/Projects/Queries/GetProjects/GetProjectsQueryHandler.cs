using Flow.Application.Common.Authorization;
using Flow.Application.Common.Persistence;
using MediatR;

namespace Flow.Application.Projects.Queries.GetProjects;

public class GetProjectsQueryHandler
    : IRequestHandler<GetProjectsQuery, IReadOnlyList<ProjectSummaryDto>>
{
    private readonly IProjectRepository _projects;
    private readonly ResourceAccessPolicy _access;

    public GetProjectsQueryHandler(IProjectRepository projects, ResourceAccessPolicy access)
    {
        _projects = projects;
        _access = access;
    }

    public async Task<IReadOnlyList<ProjectSummaryDto>> Handle(
        GetProjectsQuery request, CancellationToken cancellationToken)
    {
        var filter = new ProjectFilter
        {
            OwnerId = request.OwnerId,
            Status = request.Status,
            Stage = request.Stage,
            LinkedGuidelineId = request.LinkedGuidelineId,
            Skip = Math.Max(0, request.Skip),
            Take = Math.Clamp(request.Take, 1, 200),

            // Narrows the query itself rather than the results, so paging cannot be used
            // to infer how many documents were filtered out.
            RestrictToOperator = await _access.ProjectListScopeAsync(cancellationToken)
        };

        var now = DateTimeOffset.UtcNow;
        var results = await _projects.QueryAsync(filter, cancellationToken);
        return results.Select(p => ProjectSummaryDto.From(p, now)).ToList();
    }
}
