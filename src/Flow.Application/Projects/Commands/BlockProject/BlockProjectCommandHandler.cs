using Flow.Application.Common.Exceptions;
using Flow.Application.Common.Persistence;
using Flow.Application.Common.Services;
using Flow.Domain.Enums;
using MediatR;

namespace Flow.Application.Projects.Commands.BlockProject;

public class BlockProjectCommandHandler : IRequestHandler<BlockProjectCommand>
{
    private readonly IProjectRepository _projects;
    private readonly IUserRepository _users;
    private readonly ProjectTransitionRecorder _recorder;
    private readonly NotificationPublisher _notifications;

    public BlockProjectCommandHandler(
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

    public async Task Handle(BlockProjectCommand request, CancellationToken cancellationToken)
    {
        var project = await _projects.GetByIdAsync(request.ProjectId, cancellationToken)
            ?? throw new NotFoundException("Project", request.ProjectId);

        var leadership = await _users.GetByRoleAsync(UserRole.Leadership, cancellationToken);

        var previous = project.Status.ToString();
        project.Block(request.Reason);

        var blockKey = project.BlockedSince!.Value.Ticks;

        await _recorder.RecordAsync(
            project, ProjectActions.Blocked,
            previousValue: previous,
            reason: request.Reason,
            alsoInTransaction: async ct =>
            {
                await _notifications.PublishAsync(
                    project.OwnerId,
                    NotificationType.ProjectBlocked,
                    "Projeto bloqueado",
                    $"\"{project.Title}\": {request.Reason}",
                    $"flow://projects/{project.Id}",
                    $"ProjectBlocked:{project.Id}:{blockKey}:{project.OwnerId}",
                    ct);

                // Blockers are the product's headline bottleneck signal, so leadership
                // learns about them without having to open the dashboard.
                await _notifications.PublishManyAsync(
                    leadership.Select(l => l.Id).Where(id => id != project.OwnerId),
                    NotificationType.ProjectBlocked,
                    "Projeto bloqueado",
                    $"\"{project.Title}\": {request.Reason}",
                    $"flow://projects/{project.Id}",
                    leaderId => $"ProjectBlocked:{project.Id}:{blockKey}:{leaderId}",
                    ct);
            },
            cancellationToken: cancellationToken);
    }
}
