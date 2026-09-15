using Flow.Application.Common.Persistence;
using Flow.Domain.Entities;
using MongoDB.Driver;

namespace Flow.Infrastructure.Persistence.Mongo.Repositories;

public sealed class MongoRefreshTokenRepository
    : MongoRepositoryBase<RefreshToken>, IRefreshTokenRepository
{
    public MongoRefreshTokenRepository(FlowMongoContext context, MongoSessionAccessor sessions)
        : base(context.RefreshTokens, sessions) { }

    public Task AddAsync(RefreshToken token, CancellationToken cancellationToken = default) =>
        InsertAsync(token, cancellationToken);

    public Task<RefreshToken?> GetByHashAsync(
        string tokenHash, CancellationToken cancellationToken = default) =>
        Find(Filter.Eq(x => x.TokenHash, tokenHash)).FirstOrDefaultAsync(cancellationToken)!;

    public async Task<IReadOnlyList<RefreshToken>> GetActiveForUserAsync(
        Guid userId, CancellationToken cancellationToken = default) =>
        await Find(Filter.And(
                Filter.Eq(x => x.UserId, userId),
                Filter.Eq(x => x.RevokedAt, null),
                Filter.Gt(x => x.ExpiresAt, DateTimeOffset.UtcNow)))
            .ToListAsync(cancellationToken);

    /// <summary>
    /// Compare-and-set: the state the caller believes in is part of the filter, so the
    /// write only lands if that belief is still true when the server applies it.
    ///
    /// The condition is deliberately the full one — right token, right owner, not yet
    /// revoked, not yet expired — rather than a match on id alone. An update by id would
    /// happily overwrite a rotation another request had just committed.
    /// </summary>
    public async Task<bool> TryConsumeAsync(
        string tokenHash,
        Guid userId,
        string replacementTokenHash,
        DateTimeOffset consumedAt,
        CancellationToken cancellationToken = default)
    {
        var stillUnconsumed = Filter.And(
            Filter.Eq(x => x.TokenHash, tokenHash),
            Filter.Eq(x => x.UserId, userId),
            Filter.Eq(x => x.RevokedAt, null),
            Filter.Gt(x => x.ExpiresAt, consumedAt));

        var consume = Update
            .Set(x => x.RevokedAt, (DateTimeOffset?)consumedAt)
            .Set(x => x.ReplacedByTokenHash, replacementTokenHash);

        var result = await UpdateAsync(stillUnconsumed, consume, cancellationToken);

        // false means one thing only: no document matched, so this caller did not consume
        // the token. Database failures are deliberately not caught here.
        //
        // An earlier version swallowed TransientTransactionError and returned false, which
        // conflated two situations that could not be more different. A lost race is a
        // security signal — the token was already consumed, and the policy revokes the
        // family. A step-down, an election or a write conflict is a cluster event that says
        // nothing about the token. Reporting the second as the first would log a user out
        // of every session because a replica was elected, and would also rob the driver of
        // the retry it was about to perform.
        return result.ModifiedCount == 1;
    }

    public async Task<bool> TryRevokeAsync(
        string tokenHash,
        Guid userId,
        DateTimeOffset revokedAt,
        CancellationToken cancellationToken = default)
    {
        var result = await UpdateAsync(
            Filter.And(
                Filter.Eq(x => x.TokenHash, tokenHash),
                Filter.Eq(x => x.UserId, userId),
                Filter.Eq(x => x.RevokedAt, null),
                Filter.Gt(x => x.ExpiresAt, revokedAt)),
            Update.Set(x => x.RevokedAt, (DateTimeOffset?)revokedAt),
            cancellationToken);

        return result.ModifiedCount == 1;
    }

    /// <summary>
    /// Used when a consumed token is presented again: the safest response is to invalidate
    /// every live session for that user rather than guess which one was stolen.
    /// </summary>
    public Task RevokeAllForUserAsync(Guid userId, CancellationToken cancellationToken = default) =>
        UpdateManyAsync(
            Filter.And(Filter.Eq(x => x.UserId, userId), Filter.Eq(x => x.RevokedAt, null)),
            Update.Set(x => x.RevokedAt, DateTimeOffset.UtcNow),
            cancellationToken);
}
