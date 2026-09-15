using MediatR;

namespace Flow.Application.Projects.Commands.UpdateProjectProgress;

/// <summary>Progress is its own operation, never a side effect of editing a project.</summary>
public record UpdateProjectProgressCommand(Guid ProjectId, int ProgressPercentage) : IRequest;
