using Flow.Domain.Entities;

namespace Flow.Application.Common.Persistence;

public interface IPointLedgerRepository
{
    Task AppendAsync(PointLedgerEntry entry, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PointLedgerEntry>> GetForUserAsync(
        Guid userId, CancellationToken cancellationToken = default);

    Task<int> GetTotalForUserAsync(Guid userId, CancellationToken cancellationToken = default);
}
