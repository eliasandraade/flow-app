using MediatR;

namespace Flow.Application.Projects.Commands.CompleteProject;

public record CompleteProjectCommand(Guid ProjectId) : IRequest;
