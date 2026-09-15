using MediatR;

namespace Flow.Application.Ideas.Queries.CompareIdeas;

public record CompareIdeasQuery(IReadOnlyList<Guid> IdeaIds) : IRequest<IdeaComparisonDto>;

public record IdeaComparisonRowDto(
    Guid Id,
    string Title,
    string Status,
    string Priority,
    int? Score,
    int? FlowScore,
    FlowScoreDto? FlowScoreBreakdown,
    Guid? LinkedGuidelineId,
    string? LinkedGuidelineTitle,
    bool GuidelineIsCurrent,
    int CommentCount,
    DateTimeOffset CreatedAt,
    int DaysUnderReview);

public record IdeaComparisonDto(
    IReadOnlyList<IdeaComparisonRowDto> Ideas,
    Guid? HighestFlowScoreId,
    Guid? HighestScoreId,
    DateTimeOffset GeneratedAt);
