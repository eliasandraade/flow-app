using Flow.Application.Common.Exceptions;
using Flow.Application.Common.Persistence;
using MediatR;

namespace Flow.Application.Dashboard.Queries.GetStrategyDashboard;

public class GetStrategyDashboardQueryHandler
    : IRequestHandler<GetStrategyDashboardQuery, StrategyDashboardDto>
{
    private readonly IGuidelineRepository _guidelines;
    private readonly IDashboardReadRepository _dashboard;

    public GetStrategyDashboardQueryHandler(
        IGuidelineRepository guidelines, IDashboardReadRepository dashboard)
    {
        _guidelines = guidelines;
        _dashboard = dashboard;
    }

    public async Task<StrategyDashboardDto> Handle(
        GetStrategyDashboardQuery request, CancellationToken cancellationToken)
    {
        var guideline = await _guidelines.GetByIdAsync(request.GuidelineId, cancellationToken)
            ?? throw new NotFoundException("Guideline", request.GuidelineId);

        var now = DateTimeOffset.UtcNow;
        var aggregates = await _dashboard.GetStrategyDetailAggregatesAsync(guideline.Id, cancellationToken);

        return new StrategyDashboardDto(
            guideline.Id,
            guideline.Title,
            guideline.Description,
            guideline.Category.ToString(),
            guideline.Campaign,
            guideline.ValidFrom,
            guideline.ValidUntil,
            guideline.IsCurrentAt(now),
            aggregates.IdeaCount,
            ToDistribution(aggregates.IdeasByStatus, aggregates.IdeaCount),
            aggregates.ProjectCount,
            ToDistribution(aggregates.ProjectsByStatus, aggregates.ProjectCount),
            aggregates.EstimatedNetValue,
            aggregates.ActualNetValue,
            now);
    }

    private static IReadOnlyList<DistributionSliceDto> ToDistribution(
        IReadOnlyList<StatusCount> counts, int total) =>
        counts
            .Select(c => new DistributionSliceDto(
                c.Status, c.Count, total <= 0 ? 0 : Math.Round((double)c.Count / total * 100, 1)))
            .OrderByDescending(s => s.Count)
            .ToList();
}
