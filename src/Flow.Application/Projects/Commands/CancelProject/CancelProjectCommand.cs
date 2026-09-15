using MediatR;

namespace Flow.Application.Projects.Commands.CancelProject;

public record CancelProjectCommand(Guid ProjectId, string Reason) : IRequest;
