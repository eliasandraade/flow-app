using Flow.Application.Common.Exceptions;
using Flow.Application.Common.Persistence;
using Flow.Application.Common.Services;
using Flow.Domain.Enums;
using MediatR;

namespace Flow.Application.Projects.Commands.UpdateProjectProgress;

public class UpdateProjectProgressCommandHandler : IRequestHandler<UpdateProjectProgressCommand>
{
    private readonly IProjectRepository _projects;
    private readonly IUserRepository _users;
    private readonly ProjectTransitionRecorder _recorder;
    private readonly NotificationPublisher _notifications;

    public UpdateProjectProgressCommandHandler(
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

    public async Task Handle(
        UpdateProjectProgressCommand request, CancellationToken cancellationToken)
    {
        var project = await _projects.GetByIdAsync(request.ProjectId, cancellationToken)
            ?? throw new NotFoundException("Project", request.ProjectId);

        var previous = project.ProgressPercentage;
        project.UpdateProgress(request.ProgressPercentage);

        var wasAtRisk = project.IsAtRiskAt(DateTimeOffset.UtcNow);
        var leadership = wasAtRisk
            ? await _users.GetByRoleAsync(UserRole.Leadership, cancellationToken)
            : [];

        await _recorder.RecordAsync(
            project, ProjectActions.ProgressUpdated,
            previousValue: previous.ToString(),
            newValue: project.ProgressPercentage.ToString(),
            alsoInTransaction: async ct =>
            {
                if (!wasAtRisk) return;

                // Reporting progress that still leaves the project behind schedule is
                // exactly the moment leadership needs to hear about it.
                await _notifications.PublishManyAsync(
                    leadership.Select(l => l.Id),
                    NotificationType.ProjectDeadlineAtRisk,
                    "Projeto com prazo em risco",
                    $"\"{project.Title}\" está em {project.ProgressPercentage}% com prazo próximo.",
                    $"flow://projects/{project.Id}",
                    leaderId => $"ProjectAtRisk:{project.Id}:{DateTimeOffset.UtcNow:yyyyMMdd}:{leaderId}",
                    ct);
            },
            cancellationToken: cancellationToken);
    }
}
