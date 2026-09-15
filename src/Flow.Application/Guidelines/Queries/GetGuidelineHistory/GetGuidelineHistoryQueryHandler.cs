using Flow.Application.Common.Exceptions;
using Flow.Application.Common.Persistence;
using MediatR;

namespace Flow.Application.Guidelines.Queries.GetGuidelineHistory;

public class GetGuidelineHistoryQueryHandler
    : IRequestHandler<GetGuidelineHistoryQuery, IReadOnlyList<GuidelineHistoryDto>>
{
    private readonly IGuidelineRepository _guidelines;
    private readonly IGuidelineHistoryRepository _history;

    public GetGuidelineHistoryQueryHandler(
        IGuidelineRepository guidelines, IGuidelineHistoryRepository history)
    {
        _guidelines = guidelines;
        _history = history;
    }

    public async Task<IReadOnlyList<GuidelineHistoryDto>> Handle(
        GetGuidelineHistoryQuery request, CancellationToken cancellationToken)
    {
        if (!await _guidelines.ExistsAsync(request.GuidelineId, cancellationToken))
            throw new NotFoundException("Guideline", request.GuidelineId);

        var entries = await _history.GetForGuidelineAsync(request.GuidelineId, cancellationToken);
        return entries.Select(GuidelineHistoryDto.From).ToList();
    }
}
