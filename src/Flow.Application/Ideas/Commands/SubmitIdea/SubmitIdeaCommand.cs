using MediatR;

namespace Flow.Application.Ideas.Commands.SubmitIdea;

public record SubmitIdeaCommand(Guid IdeaId) : IRequest;
