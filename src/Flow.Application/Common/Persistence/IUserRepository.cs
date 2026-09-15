using Flow.Domain.Entities;
using Flow.Domain.Enums;

namespace Flow.Application.Common.Persistence;

/// <summary>
/// Business-side access to users.
///
/// Identity concerns (password hashing, security stamps, role bookkeeping) stay with
/// Identity's UserManager. What lives here is the read side plus the one business
/// mutation that must take part in a domain transaction: the gamification points.
/// </summary>
public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<User>> GetByIdsAsync(
        IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<User>> GetByRoleAsync(UserRole role, CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<Guid, string>> GetNamesAsync(
        IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomic increment. Deliberately not a read-modify-write: two concurrent approvals
    /// of ideas from the same author would otherwise lose one award.
    /// </summary>
    Task IncrementPointsAsync(Guid userId, int points, CancellationToken cancellationToken = default);
}
