using Flow.Application.Common.Authorization;
using Flow.Application.Common.Exceptions;
using Flow.Application.Common.Persistence;
using MediatR;

namespace Flow.Application.Results.Queries.GetResult;

public class GetResultQueryHandler : IRequestHandler<GetResultQuery, ResultDto>
{
    private readonly IResultRepository _results;
    private readonly IProjectRepository _projects;
    private readonly ResourceAccessPolicy _access;

    public GetResultQueryHandler(
        IResultRepository results,
        IProjectRepository projects,
        ResourceAccessPolicy access)
    {
        _results = results;
        _projects = projects;
        _access = access;
    }

    public async Task<ResultDto> Handle(GetResultQuery request, CancellationToken cancellationToken)
    {
        // The project is loaded first because the authorization rule lives on it, and
        // because revenue, savings and cost are the most sensitive figures in the product:
        // they must not be reachable by anyone who happens to know a project id.
        var project = await _projects.GetByIdAsync(request.ProjectId, cancellationToken)
            ?? throw new NotFoundException("Project", request.ProjectId);

        await _access.EnsureCanReadProjectAsync(project, cancellationToken);

        var result = await _results.GetByProjectIdAsync(request.ProjectId, cancellationToken)
            ?? throw new NotFoundException("Result", request.ProjectId);

        return ResultDto.From(result);
    }
}
