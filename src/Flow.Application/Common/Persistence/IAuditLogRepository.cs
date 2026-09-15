using Flow.Domain.Entities;

namespace Flow.Application.Common.Persistence;

/// <summary>
/// Append-only by construction. There is intentionally no update or delete member, so no
/// code path can rewrite governance history.
/// </summary>
public interface IAuditLogRepository
{
    Task AppendAsync(AuditLog entry, CancellationToken cancellationToken = default);

    Task AppendRangeAsync(IEnumerable<AuditLog> entries, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AuditLog>> GetForEntityAsync(
        string entityType, Guid entityId, CancellationToken cancellationToken = default);
}
