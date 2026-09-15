using MediatR;

namespace Flow.Application.Dashboard.Queries.GetStrategyDashboard;

public record GetStrategyDashboardQuery(Guid GuidelineId) : IRequest<StrategyDashboardDto>;

public record StrategyDashboardDto(
    Guid GuidelineId,
    string Title,
    string Description,
    string Category,
    string? Campaign,
    DateTimeOffset ValidFrom,
    DateTimeOffset? ValidUntil,
    bool IsCurrent,
    int IdeaCount,
    IReadOnlyList<DistributionSliceDto> IdeasByStatus,
    int ProjectCount,
    IReadOnlyList<DistributionSliceDto> ProjectsByStatus,
    decimal EstimatedNetValue,
    decimal ActualNetValue,
    DateTimeOffset GeneratedAt);
