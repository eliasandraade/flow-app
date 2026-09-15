using MediatR;

namespace Flow.Application.Ideas.Commands.ApproveIdea;

public record ApproveIdeaCommand(Guid IdeaId, string? ManagerComment) : IRequest;
