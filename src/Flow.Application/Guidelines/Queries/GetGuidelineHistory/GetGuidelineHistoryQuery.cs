using MediatR;

namespace Flow.Application.Guidelines.Queries.GetGuidelineHistory;

public record GetGuidelineHistoryQuery(Guid GuidelineId) : IRequest<IReadOnlyList<GuidelineHistoryDto>>;
