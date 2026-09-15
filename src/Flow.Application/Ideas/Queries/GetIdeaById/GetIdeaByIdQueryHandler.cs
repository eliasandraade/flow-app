using Flow.Application.Common.Authorization;
using Flow.Application.Common.Exceptions;
using Flow.Application.Common.Persistence;
using MediatR;

namespace Flow.Application.Ideas.Queries.GetIdeaById;

public class GetIdeaByIdQueryHandler : IRequestHandler<GetIdeaByIdQuery, IdeaDetailDto>
{
    private readonly IIdeaRepository _ideas;
    private readonly IGuidelineRepository _guidelines;
    private readonly ResourceAccessPolicy _access;

    public GetIdeaByIdQueryHandler(
        IIdeaRepository ideas,
        IGuidelineRepository guidelines,
        ResourceAccessPolicy access)
    {
        _ideas = ideas;
        _guidelines = guidelines;
        _access = access;
    }

    public async Task<IdeaDetailDto> Handle(
        GetIdeaByIdQuery request, CancellationToken cancellationToken)
    {
        var idea = await _ideas.GetByIdAsync(request.IdeaId, cancellationToken)
            ?? throw new NotFoundException("Idea", request.IdeaId);

        // Resource-level authorization: holding the role is not enough, and knowing the
        // GUID is not either.
        var isOwner = _access.EnsureCanReadIdea(idea);

        string? guidelineTitle = null;
        if (idea.LinkedGuidelineId is { } guidelineId)
        {
            var guideline = await _guidelines.GetByIdAsync(guidelineId, cancellationToken);
            guidelineTitle = guideline?.Title;
        }

        return IdeaDetailDto.From(idea, guidelineTitle, isOwner);
    }
}
