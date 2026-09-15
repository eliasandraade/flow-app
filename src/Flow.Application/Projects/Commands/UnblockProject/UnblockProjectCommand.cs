using MediatR;

namespace Flow.Application.Projects.Commands.UnblockProject;

public record UnblockProjectCommand(Guid ProjectId) : IRequest;
