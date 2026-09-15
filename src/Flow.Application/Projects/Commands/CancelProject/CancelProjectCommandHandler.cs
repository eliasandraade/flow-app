using Flow.Application.Common.Exceptions;
using Flow.Application.Common.Persistence;
using Flow.Application.Common.Services;
using Flow.Domain.Enums;
using MediatR;

namespace Flow.Application.Projects.Commands.CancelProject;

public class CancelProjectCommandHandler : IRequestHandler<CancelProjectCommand>
{
    private readonly IProjectRepository _projects;
    private readonly ProjectTransitionRecorder _recorder;
    private readonly NotificationPublisher _notifications;

    public CancelProjectCommandHandler(
        IProjectRepository projects,
        ProjectTransitionRecorder recorder,
        NotificationPublisher notifications)
    {
        _projects = projects;
        _recorder = recorder;
        _notifications = notifications;
    }

    public async Task Handle(CancelProjectCommand request, CancellationToken cancellationToken)
    {
        var project = await _projects.GetByIdAsync(request.ProjectId, cancellationToken)
            ?? throw new NotFoundException("Project", request.ProjectId);

        var previous = project.Status.ToString();
        project.Cancel(request.Reason);

        await _recorder.RecordAsync(
            project, ProjectActions.Cancelled,
            previousValue: previous,
            reason: request.Reason,
            alsoInTransaction: ct => _notifications.PublishAsync(
                project.OwnerId,
                NotificationType.ProjectCancelled,
                "Projeto cancelado",
                $"\"{project.Title}\": {request.Reason}",
                $"flow://projects/{project.Id}",
                $"ProjectCancelled:{project.Id}:{project.OwnerId}",
                ct),
            cancellationToken: cancellationToken);
    }
}
