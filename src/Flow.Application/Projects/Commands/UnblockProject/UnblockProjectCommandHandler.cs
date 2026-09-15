using Flow.Application.Common.Exceptions;
using Flow.Application.Common.Persistence;
using Flow.Application.Common.Services;
using Flow.Domain.Enums;
using MediatR;

namespace Flow.Application.Projects.Commands.UnblockProject;

public class UnblockProjectCommandHandler : IRequestHandler<UnblockProjectCommand>
{
    private readonly IProjectRepository _projects;
    private readonly ProjectTransitionRecorder _recorder;
    private readonly NotificationPublisher _notifications;

    public UnblockProjectCommandHandler(
        IProjectRepository projects,
        ProjectTransitionRecorder recorder,
        NotificationPublisher notifications)
    {
        _projects = projects;
        _recorder = recorder;
        _notifications = notifications;
    }

    public async Task Handle(UnblockProjectCommand request, CancellationToken cancellationToken)
    {
        var project = await _projects.GetByIdAsync(request.ProjectId, cancellationToken)
            ?? throw new NotFoundException("Project", request.ProjectId);

        var previous = project.Status.ToString();
        var blockedSince = project.BlockedSince;
        project.Unblock();

        var daysBlocked = blockedSince is null
            ? (int?)null
            : (int)(DateTimeOffset.UtcNow - blockedSince.Value).TotalDays;

        await _recorder.RecordAsync(
            project, ProjectActions.Unblocked,
            previousValue: previous,
            reason: daysBlocked is null ? null : $"Blocked for {daysBlocked} day(s)",
            alsoInTransaction: ct => _notifications.PublishAsync(
                project.OwnerId,
                NotificationType.ProjectUnblocked,
                "Projeto desbloqueado",
                $"\"{project.Title}\" voltou a andar.",
                $"flow://projects/{project.Id}",
                $"ProjectUnblocked:{project.Id}:{DateTimeOffset.UtcNow.Ticks}",
                ct),
            cancellationToken: cancellationToken);
    }
}
