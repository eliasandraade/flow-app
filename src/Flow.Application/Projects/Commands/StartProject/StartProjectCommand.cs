using MediatR;

namespace Flow.Application.Projects.Commands.StartProject;

public record StartProjectCommand(Guid ProjectId) : IRequest;
