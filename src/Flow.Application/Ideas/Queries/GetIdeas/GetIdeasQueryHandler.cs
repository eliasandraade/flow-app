using Flow.Application.Common.Interfaces;
using Flow.Application.Common.Persistence;
using Flow.Domain.Enums;
using MediatR;

namespace Flow.Application.Ideas.Queries.GetIdeas;

public class GetIdeasQueryHandler : IRequestHandler<GetIdeasQuery, IReadOnlyList<IdeaSummaryDto>>
{
    private readonly IIdeaRepository _ideas;
    private readonly ICurrentUserService _currentUser;

    public GetIdeasQueryHandler(IIdeaRepository ideas, ICurrentUserService currentUser)
    {
        _ideas = ideas;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<IdeaSummaryDto>> Handle(
        GetIdeasQuery request, CancellationToken cancellationToken)
    {
        // An Operator only ever sees their own ideas, regardless of what the query asks for.
        // Scoping here rather than in the controller keeps the rule with the data access.
        var submittedBy = _currentUser.IsInRole(UserRole.Operator)
            ? _currentUser.UserId
            : request.SubmittedById;

        var filter = new IdeaFilter
        {
            SubmittedBy = submittedBy,
            Status = request.Status,
            Priority = request.Priority,
            LinkedGuidelineId = request.LinkedGuidelineId,
            MinScore = request.MinScore,
            SortBy = ParseSort(request.SortBy),
            Skip = Math.Max(0, request.Skip),
            Take = Math.Clamp(request.Take, 1, 200)
        };

        var results = await _ideas.QueryAsync(filter, cancellationToken);
        return results.Select(IdeaSummaryDto.From).ToList();
    }

    private static IdeaSortOrder ParseSort(string? sortBy) => sortBy?.ToLowerInvariant() switch
    {
        "score" => IdeaSortOrder.ScoreDesc,
        "flowscore" => IdeaSortOrder.FlowScoreDesc,
        "priority" => IdeaSortOrder.PriorityDesc,
        _ => IdeaSortOrder.CreatedAtDesc
    };
}
