using Flow.Application.Common.Exceptions;
using Flow.Application.Common.Persistence;
using Flow.Application.Common.Services;
using Flow.Domain.Entities;
using Flow.Domain.Enums;
using MediatR;

namespace Flow.Application.Projects.Commands.CreateProject;

public class CreateProjectCommandHandler : IRequestHandler<CreateProjectCommand, ProjectSummaryDto>
{
    private readonly IUserRepository _users;
    private readonly IGuidelineRepository _guidelines;
    private readonly ProjectTransitionRecorder _recorder;
    private readonly NotificationPublisher _notifications;

    public CreateProjectCommandHandler(
        IUserRepository users,
        IGuidelineRepository guidelines,
        ProjectTransitionRecorder recorder,
        NotificationPublisher notifications)
    {
        _users = users;
        _guidelines = guidelines;
        _recorder = recorder;
        _notifications = notifications;
    }

    public async Task<ProjectSummaryDto> Handle(
        CreateProjectCommand request, CancellationToken cancellationToken)
    {
        var owner = await _users.GetByIdAsync(request.OwnerId, cancellationToken)
            ?? throw new NotFoundException("User", request.OwnerId);

        if (request.LinkedGuidelineId is { } guidelineId
            && !await _guidelines.ExistsAsync(guidelineId, cancellationToken))
        {
            throw new NotFoundException("Guideline", guidelineId);
        }

        var project = Project.Create(
            request.Title,
            request.Description,
            owner.Id,
            owner.Name,
            request.Priority,
            sourceIdeaId: null,
            linkedGuidelineId: request.LinkedGuidelineId,
            estimatedCost: request.EstimatedCost,
            deadline: request.Deadline);

        await _recorder.RecordAsync(
            project,
            action: ProjectActions.Created,
            isNew: true,
            alsoInTransaction: ct => _notifications.PublishAsync(
                owner.Id,
                NotificationType.ProjectCreated,
                "Você é responsável por um novo projeto",
                $"\"{project.Title}\" foi criado e está sob sua responsabilidade.",
                $"flow://projects/{project.Id}",
                $"ProjectCreated:{project.Id}:{owner.Id}",
                ct),
            cancellationToken: cancellationToken);

        return ProjectSummaryDto.From(project, DateTimeOffset.UtcNow);
    }
}
