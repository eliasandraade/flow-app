using Flow.Application.Common.Authorization;
using Flow.Application.Common.Exceptions;
using Flow.Application.Common.Persistence;
using MediatR;

namespace Flow.Application.Ideas.Queries.GetIdeaComments;

public class GetIdeaCommentsQueryHandler
    : IRequestHandler<GetIdeaCommentsQuery, IReadOnlyList<IdeaCommentDto>>
{
    private readonly IIdeaRepository _ideas;
    private readonly IIdeaCommentRepository _comments;
    private readonly ResourceAccessPolicy _access;

    public GetIdeaCommentsQueryHandler(
        IIdeaRepository ideas,
        IIdeaCommentRepository comments,
        ResourceAccessPolicy access)
    {
        _ideas = ideas;
        _comments = comments;
        _access = access;
    }

    public async Task<IReadOnlyList<IdeaCommentDto>> Handle(
        GetIdeaCommentsQuery request, CancellationToken cancellationToken)
    {
        var idea = await _ideas.GetByIdAsync(request.IdeaId, cancellationToken)
            ?? throw new NotFoundException("Idea", request.IdeaId);

        // The comments carry the assessment written by the manager, so they are exactly as
        // sensitive as the idea itself and get the same rule.
        _access.EnsureCanReadIdea(idea);

        var comments = await _comments.GetForIdeaAsync(idea.Id, cancellationToken);
        return comments.Select(IdeaCommentDto.From).ToList();
    }
}
