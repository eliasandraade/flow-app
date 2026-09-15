using Flow.Application.Common.Authorization;
using Flow.Application.Common.Exceptions;
using Flow.Application.Common.Persistence;
using Flow.Domain.Entities;
using MediatR;

namespace Flow.Application.Projects.Queries.GetProjectTimeline;

public class GetProjectTimelineQueryHandler
    : IRequestHandler<GetProjectTimelineQuery, IReadOnlyList<TimelineEntryDto>>
{
    private readonly IProjectRepository _projects;
    private readonly IAuditLogRepository _auditLogs;
    private readonly ResourceAccessPolicy _access;

    public GetProjectTimelineQueryHandler(
        IProjectRepository projects,
        IAuditLogRepository auditLogs,
        ResourceAccessPolicy access)
    {
        _projects = projects;
        _auditLogs = auditLogs;
        _access = access;
    }

    public async Task<IReadOnlyList<TimelineEntryDto>> Handle(
        GetProjectTimelineQuery request, CancellationToken cancellationToken)
    {
        var project = await _projects.GetByIdAsync(request.ProjectId, cancellationToken)
            ?? throw new NotFoundException("Project", request.ProjectId);

        // The timeline names who did what, and why a project was blocked or cancelled. It
        // is governance history, and it follows the access rule of the project itself.
        await _access.EnsureCanReadProjectAsync(project, cancellationToken);

        var entries = await _auditLogs.GetForEntityAsync(
            nameof(Project), request.ProjectId, cancellationToken);

        return entries.Select(TimelineEntryDto.From).ToList();
    }
}
