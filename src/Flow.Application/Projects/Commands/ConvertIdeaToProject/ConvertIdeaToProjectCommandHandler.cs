using Flow.Application.Common.Exceptions;
using Flow.Application.Common.Persistence;
using Flow.Application.Common.Services;
using Flow.Domain.Entities;
using Flow.Domain.Enums;
using Flow.Domain.Exceptions;
using MediatR;

namespace Flow.Application.Projects.Commands.ConvertIdeaToProject;

public class ConvertIdeaToProjectCommandHandler
    : IRequestHandler<ConvertIdeaToProjectCommand, ProjectSummaryDto>
{
    private readonly IIdeaRepository _ideas;
    private readonly IUserRepository _users;
    private readonly IProjectRepository _projects;
    private readonly IAssistantRunRepository _assistantRuns;
    private readonly ProjectTransitionRecorder _recorder;
    private readonly NotificationPublisher _notifications;

    public ConvertIdeaToProjectCommandHandler(
        IIdeaRepository ideas,
        IUserRepository users,
        IProjectRepository projects,
        IAssistantRunRepository assistantRuns,
        ProjectTransitionRecorder recorder,
        NotificationPublisher notifications)
    {
        _ideas = ideas;
        _users = users;
        _projects = projects;
        _assistantRuns = assistantRuns;
        _recorder = recorder;
        _notifications = notifications;
    }

    public async Task<ProjectSummaryDto> Handle(
        ConvertIdeaToProjectCommand request, CancellationToken cancellationToken)
    {
        var idea = await _ideas.GetByIdAsync(request.IdeaId, cancellationToken)
            ?? throw new NotFoundException("Idea", request.IdeaId);

        if (idea.Status != IdeaStatus.Approved)
            throw new DomainException("Only approved ideas can be converted to projects.");

        if (await _projects.ExistsForSourceIdeaAsync(idea.Id, cancellationToken))
            throw new ConflictException("This idea has already been converted into a project.");

        var owner = await _users.GetByIdAsync(request.OwnerId, cancellationToken)
            ?? throw new NotFoundException("User", request.OwnerId);

        // The guideline travels from the idea to the project so the strategy-to-result
        // chain survives even if the idea is later re-linked.
        var project = Project.Create(
            request.Title,
            request.Description,
            owner.Id,
            owner.Name,
            request.Priority,
            sourceIdeaId: idea.Id,
            linkedGuidelineId: idea.LinkedGuidelineId,
            estimatedCost: request.EstimatedCost,
            deadline: request.Deadline);

        // If this project came from an accepted assistant suggestion, close the governance
        // loop by recording that the suggestion was actually acted on.
        var assistantRun = request.AssistantRunId is { } runId
            ? await _assistantRuns.GetByIdAsync(runId, cancellationToken)
            : null;

        await _recorder.RecordAsync(
            project,
            action: ProjectActions.Created,
            newValue: project.Status.ToString(),
            reason: $"Converted from idea {idea.Id}",
            isNew: true,
            alsoInTransaction: async ct =>
            {
                if (assistantRun is not null)
                {
                    assistantRun.MarkSuggestionAccepted(nameof(Project), project.Id);
                    await _assistantRuns.UpdateAsync(assistantRun, ct);
                }

                await _notifications.PublishAsync(
                    idea.SubmittedBy,
                    NotificationType.ProjectCreated,
                    "Sua ideia virou projeto",
                    $"\"{idea.Title}\" foi convertida no projeto \"{project.Title}\".",
                    $"flow://projects/{project.Id}",
                    $"IdeaConverted:{project.Id}:{idea.SubmittedBy}",
                    ct);

                if (owner.Id != idea.SubmittedBy)
                {
                    await _notifications.PublishAsync(
                        owner.Id,
                        NotificationType.ProjectCreated,
                        "Você é responsável por um novo projeto",
                        $"\"{project.Title}\" foi criado a partir de uma ideia aprovada.",
                        $"flow://projects/{project.Id}",
                        $"ProjectCreated:{project.Id}:{owner.Id}",
                        ct);
                }
            },
            cancellationToken: cancellationToken);

        return ProjectSummaryDto.From(project, DateTimeOffset.UtcNow);
    }
}
