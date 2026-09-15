using MediatR;

namespace Flow.Application.Gamification.Queries.GetMyPointsLedger;

public record GetMyPointsLedgerQuery : IRequest<IReadOnlyList<PointsLedgerEntryDto>>;
