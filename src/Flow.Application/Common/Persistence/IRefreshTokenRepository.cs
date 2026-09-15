using Flow.Domain.Entities;

namespace Flow.Application.Common.Persistence;

public interface IRefreshTokenRepository
{
    Task AddAsync(RefreshToken token, CancellationToken cancellationToken = default);
    Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RefreshToken>> GetActiveForUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task RevokeAllForUserAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Revokes one token, conditionally on it belonging to this user and still being
    /// active. Same reasoning as TryConsumeAsync: the ownership check belongs in the write
    /// itself, so logging out cannot race with a rotation and resurrect a dead token.
    /// </summary>
    Task<bool> TryRevokeAsync(
        string tokenHash,
        Guid userId,
        DateTimeOffset revokedAt,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Consumes a refresh token and records its replacement in a single atomic operation,
    /// returning true only for the caller that actually consumed it.
    ///
    /// This is the whole point of the method. Reading a token, checking in memory that it
    /// is still active and then writing it back by id is a check-then-act race: two
    /// requests carrying the same token can both pass the check and both rotate, leaving
    /// two live chains behind one token. The condition therefore has to travel with the
    /// write — the token is only consumed if it is still unconsumed at the instant of the
    /// update — so exactly one caller can win.
    ///
    /// A caller that gets false lost the race, or presented a token that was already
    /// consumed. Those two are indistinguishable by design, and both are treated as reuse.
    /// </summary>
    Task<bool> TryConsumeAsync(
        string tokenHash,
        Guid userId,
        string replacementTokenHash,
        DateTimeOffset consumedAt,
        CancellationToken cancellationToken = default);
}
