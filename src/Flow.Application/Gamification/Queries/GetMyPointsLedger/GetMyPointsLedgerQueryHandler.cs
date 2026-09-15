using Flow.Application.Common.Interfaces;
using Flow.Application.Common.Persistence;
using MediatR;

namespace Flow.Application.Gamification.Queries.GetMyPointsLedger;

public class GetMyPointsLedgerQueryHandler
    : IRequestHandler<GetMyPointsLedgerQuery, IReadOnlyList<PointsLedgerEntryDto>>
{
    private readonly IPointLedgerRepository _ledger;
    private readonly ICurrentUserService _currentUser;

    public GetMyPointsLedgerQueryHandler(
        IPointLedgerRepository ledger, ICurrentUserService currentUser)
    {
        _ledger = ledger;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<PointsLedgerEntryDto>> Handle(
        GetMyPointsLedgerQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId
            ?? throw new InvalidOperationException("Authenticated user identity could not be resolved.");

        var entries = await _ledger.GetForUserAsync(userId, cancellationToken);

        return entries
            .Select(e => new PointsLedgerEntryDto(
                e.Id, e.Points, e.Reason, e.ReferenceType, e.ReferenceId, e.AwardedAt))
            .ToList();
    }
}
