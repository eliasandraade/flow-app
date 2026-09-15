using Flow.Domain.Enums;
using MediatR;

namespace Flow.Application.Projects.Commands.ConvertIdeaToProject;

public record ConvertIdeaToProjectCommand(
    Guid IdeaId,
    string Title,
    string Description,
    ProjectPriority Priority,
    Guid OwnerId,
    decimal? EstimatedCost,
    DateTimeOffset? Deadline,
    Guid? AssistantRunId = null) : IRequest<ProjectSummaryDto>;
