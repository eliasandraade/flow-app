using MediatR;

namespace Flow.Application.Ideas.Commands.AddIdeaComment;

public record AddIdeaCommentCommand(Guid IdeaId, string Body) : IRequest<IdeaCommentDto>;
