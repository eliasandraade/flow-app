using MediatR;

namespace Flow.Application.Ideas.Commands.UpdateIdea;

public record UpdateIdeaCommand(
    Guid IdeaId,
    string Title,
    string Description,
    string Problem,
    Guid? LinkedGuidelineId) : IRequest;
