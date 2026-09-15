using Flow.Domain.Enums;
using MediatR;

namespace Flow.Application.Projects.Commands.CreateProject;

public record CreateProjectCommand(
    string Title,
    string Description,
    ProjectPriority Priority,
    Guid OwnerId,
    Guid? LinkedGuidelineId,
    decimal? EstimatedCost,
    DateTimeOffset? Deadline) : IRequest<ProjectSummaryDto>;
