using Flow.Domain.Enums;
using MediatR;

namespace Flow.Application.Projects.Commands.UpdateProject;

public record UpdateProjectCommand(
    Guid ProjectId,
    string Title,
    string Description,
    ProjectPriority Priority,
    Guid OwnerId,
    decimal? EstimatedCost,
    decimal? ActualCost,
    DateTimeOffset? Deadline) : IRequest;
