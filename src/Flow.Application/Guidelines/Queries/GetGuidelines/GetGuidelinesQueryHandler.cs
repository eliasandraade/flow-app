using Flow.Application.Common.Persistence;
using MediatR;

namespace Flow.Application.Guidelines.Queries.GetGuidelines;

public class GetGuidelinesQueryHandler : IRequestHandler<GetGuidelinesQuery, IReadOnlyList<GuidelineDto>>
{
    private readonly IGuidelineRepository _guidelines;

    public GetGuidelinesQueryHandler(IGuidelineRepository guidelines) => _guidelines = guidelines;

    public async Task<IReadOnlyList<GuidelineDto>> Handle(
        GetGuidelinesQuery request, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;

        var filter = new GuidelineFilter
        {
            Category = request.Category,
            Campaign = request.Campaign,
            CurrentAt = request.CurrentOnly ? now : null
        };

        var results = await _guidelines.QueryAsync(filter, cancellationToken);
        return results.Select(g => GuidelineDto.From(g, now)).ToList();
    }
}
