using MediatR;

namespace Flow.Application.Ideas.Queries.GetIdeaComments;

public record GetIdeaCommentsQuery(Guid IdeaId) : IRequest<IReadOnlyList<IdeaCommentDto>>;
