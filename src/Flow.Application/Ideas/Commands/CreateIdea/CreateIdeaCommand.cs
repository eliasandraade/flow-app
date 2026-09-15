using MediatR;

namespace Flow.Application.Ideas.Commands.CreateIdea;

public record CreateIdeaCommand(
    string Title,
    string Description,
    string Problem,
    Guid? LinkedGuidelineId) : IRequest<IdeaSummaryDto>;
