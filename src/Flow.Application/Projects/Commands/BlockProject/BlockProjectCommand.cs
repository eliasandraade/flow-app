using MediatR;

namespace Flow.Application.Projects.Commands.BlockProject;

public record BlockProjectCommand(Guid ProjectId, string Reason) : IRequest;
