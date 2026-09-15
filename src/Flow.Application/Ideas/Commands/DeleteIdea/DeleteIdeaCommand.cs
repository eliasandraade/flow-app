using MediatR;

namespace Flow.Application.Ideas.Commands.DeleteIdea;

public record DeleteIdeaCommand(Guid IdeaId) : IRequest;
