using Flow.Application.Common.Exceptions;
using Flow.Application.Common.Persistence;
using Flow.Domain.Entities;
using Flow.Domain.Enums;
using MediatR;

namespace Flow.Application.Ideas.Queries.CompareIdeas;

/// <summary>
/// Side-by-side comparison for the manager's prioritisation call. Everything here is
/// computed from stored data: no model is involved, so the comparison works offline and
/// always agrees with what the queue shows.
/// </summary>
public class CompareIdeasQueryHandler : IRequestHandler<CompareIdeasQuery, IdeaComparisonDto>
{
    private const int MaxIdeasPerComparison = 5;

    private readonly IIdeaRepository _ideas;
    private readonly IGuidelineRepository _guidelines;
    private readonly IIdeaCommentRepository _comments;

    public CompareIdeasQueryHandler(
        IIdeaRepository ideas,
        IGuidelineRepository guidelines,
        IIdeaCommentRepository comments)
    {
        _ideas = ideas;
        _guidelines = guidelines;
        _comments = comments;
    }

    public async Task<IdeaComparisonDto> Handle(
        CompareIdeasQuery request, CancellationToken cancellationToken)
    {
        var ids = request.IdeaIds.Distinct().ToList();

        if (ids.Count < 2)
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["ideaIds"] = ["At least two ideas are required for a comparison."]
            });

        if (ids.Count > MaxIdeasPerComparison)
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["ideaIds"] = [$"At most {MaxIdeasPerComparison} ideas can be compared at once."]
            });

        var ideas = await _ideas.GetByIdsAsync(ids, cancellationToken);

        var missing = ids.Except(ideas.Select(i => i.Id)).ToList();
        if (missing.Count > 0)
            throw new NotFoundException("Idea", missing[0]);

        var now = DateTimeOffset.UtcNow;

        // Guidelines are fetched in one batch rather than one lookup per idea.
        var guidelineIds = ideas
            .Where(i => i.LinkedGuidelineId is not null)
            .Select(i => i.LinkedGuidelineId!.Value)
            .Distinct()
            .ToList();

        var guidelines = guidelineIds.Count == 0
            ? []
            : await _guidelines.GetByIdsAsync(guidelineIds, cancellationToken);

        var guidelineById = guidelines.ToDictionary(g => g.Id);

        var rows = new List<IdeaComparisonRowDto>(ideas.Count);
        foreach (var idea in ideas)
        {
            StrategicGuideline? guideline = null;
            if (idea.LinkedGuidelineId is { } gid)
                guidelineById.TryGetValue(gid, out guideline);

            var commentCount = (await _comments.GetForIdeaAsync(idea.Id, cancellationToken)).Count;

            var daysUnderReview = idea.Status == IdeaStatus.UnderReview
                ? (int)(now - idea.UpdatedAt).TotalDays
                : 0;

            rows.Add(new IdeaComparisonRowDto(
                idea.Id,
                idea.Title,
                idea.Status.ToString(),
                idea.Priority.ToString(),
                idea.Score,
                idea.FlowScore?.Total,
                FlowScoreDto.From(idea.FlowScore),
                idea.LinkedGuidelineId,
                guideline?.Title,
                guideline?.IsCurrentAt(now) ?? false,
                commentCount,
                idea.CreatedAt,
                daysUnderReview));
        }

        // Ordered by the calculated score so the comparison opens on the recommendation,
        // while leaving the manual score visible right next to it.
        rows = rows.OrderByDescending(r => r.FlowScore ?? -1).ToList();

        return new IdeaComparisonDto(
            rows,
            HighestFlowScoreId: rows.Where(r => r.FlowScore is not null)
                .OrderByDescending(r => r.FlowScore).Select(r => (Guid?)r.Id).FirstOrDefault(),
            HighestScoreId: rows.Where(r => r.Score is not null)
                .OrderByDescending(r => r.Score).Select(r => (Guid?)r.Id).FirstOrDefault(),
            GeneratedAt: now);
    }
}
