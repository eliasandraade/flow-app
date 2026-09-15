using Flow.Application.Common.Exceptions;
using Flow.Application.Common.Persistence;
using Flow.Application.Common.Services;
using Flow.Domain.Enums;
using MediatR;

namespace Flow.Application.Projects.Commands.CompleteProject;

public class CompleteProjectCommandHandler : IRequestHandler<CompleteProjectCommand>
{
    private readonly IProjectRepository _projects;
    private readonly IUserRepository _users;
    private readonly ProjectTransitionRecorder _recorder;
    private readonly NotificationPublisher _notifications;

    public CompleteProjectCommandHandler(
        IProjectRepository projects,
        IUserRepository users,
        ProjectTransitionRecorder recorder,
        NotificationPublisher notifications)
    {
        _projects = projects;
        _users = users;
        _recorder = recorder;
        _notifications = notifications;
    }

    public async Task Handle(CompleteProjectCommand request, CancellationToken cancellationToken)
    {
        var project = await _projects.GetByIdAsync(request.ProjectId, cancellationToken)
            ?? throw new NotFoundException("Project", request.ProjectId);

        var leadership = await _users.GetByRoleAsync(UserRole.Leadership, cancellationToken);

        var previous = project.Status.ToString();
        project.Complete();

        await _recorder.RecordAsync(
            project, ProjectActions.Completed,
            previousValue: previous,
            alsoInTransaction: async ct =>
            {
                await _notifications.PublishAsync(
                    project.OwnerId,
                    NotificationType.ProjectCompleted,
                    "Projeto concluído",
                    $"\"{project.Title}\" foi concluído. Registre os resultados realizados.",
                    $"flow://projects/{project.Id}/result",
                    $"ProjectCompleted:{project.Id}:{project.OwnerId}",
                    ct);

                await _notifications.PublishManyAsync(
                    leadership.Select(l => l.Id).Where(id => id != project.OwnerId),
                    NotificationType.ProjectCompleted,
                    "Projeto concluído",
                    $"\"{project.Title}\" foi concluído.",
                    $"flow://projects/{project.Id}",
                    leaderId => $"ProjectCompleted:{project.Id}:{leaderId}",
                    ct);
            },
            cancellationToken: cancellationToken);
    }
}
