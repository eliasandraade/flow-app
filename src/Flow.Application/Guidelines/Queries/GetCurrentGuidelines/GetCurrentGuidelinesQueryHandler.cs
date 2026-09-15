using Flow.Application.Common.Persistence;
using MediatR;

namespace Flow.Application.Guidelines.Queries.GetCurrentGuidelines;

public class GetCurrentGuidelinesQueryHandler
    : IRequestHandler<GetCurrentGuidelinesQuery, IReadOnlyList<GuidelineDto>>
{
    private readonly IGuidelineRepository _guidelines;

    public GetCurrentGuidelinesQueryHandler(IGuidelineRepository guidelines) => _guidelines = guidelines;

    public async Task<IReadOnlyList<GuidelineDto>> Handle(
        GetCurrentGuidelinesQuery request, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var results = await _guidelines.QueryAsync(
            new GuidelineFilter { CurrentAt = now }, cancellationToken);

        return results.Select(g => GuidelineDto.From(g, now)).ToList();
    }
}
