using Flow.Application.Common.Persistence;
using Flow.Domain.Entities;
using MongoDB.Driver;

namespace Flow.Integration.Tests.Fixtures;

/// <summary>
/// Shared, observable state for <see cref="FaultInjectingRefreshTokenRepository"/>.
///
/// Registered as a singleton so a test can script a failure and then read what the
/// application did about it, across the request scopes the repository lives in.
/// </summary>
public sealed class RefreshTokenFaultPlan
{
    private int _consumeAttempts;
    private int _revokeAllCalls;

    /// <summary>How many times <c>TryConsumeAsync</c> throws a transient error before working.</summary>
    public int TransientFailuresToInject { get; set; }

    /// <summary>When set, <c>TryConsumeAsync</c> always throws this instead of touching the database.</summary>
    public Exception? PermanentFault { get; set; }

    public int ConsumeAttempts => Volatile.Read(ref _consumeAttempts);

    /// <summary>
    /// The security lever. If this moves, the application decided the token was reused —
    /// which an infrastructure failure must never cause.
    /// </summary>
    public int RevokeAllCalls => Volatile.Read(ref _revokeAllCalls);

    internal void CountConsume() => Interlocked.Increment(ref _consumeAttempts);
    internal void CountRevokeAll() => Interlocked.Increment(ref _revokeAllCalls);

    /// <summary>
    /// A failure carrying the label the driver uses to decide a transaction may be retried.
    /// This is the shape of a primary step-down, an election or a write conflict.
    /// </summary>
    public static MongoException TransientTransactionError(string message)
    {
        var error = new MongoException(message);
        error.AddErrorLabel("TransientTransactionError");
        return error;
    }
}

/// <summary>
/// Wraps the real Mongo repository and can fail on demand.
///
/// Everything it does not fail is delegated, so the test still exercises real documents in
/// a real database: the fault injection replaces a failure mode that cannot be provoked on
/// command, not the storage itself.
/// </summary>
public sealed class FaultInjectingRefreshTokenRepository : IRefreshTokenRepository
{
    private readonly IRefreshTokenRepository _inner;
    private readonly RefreshTokenFaultPlan _plan;

    public FaultInjectingRefreshTokenRepository(
        IRefreshTokenRepository inner, RefreshTokenFaultPlan plan)
    {
        _inner = inner;
        _plan = plan;
    }

    public Task AddAsync(RefreshToken token, CancellationToken cancellationToken = default) =>
        _inner.AddAsync(token, cancellationToken);

    public Task<RefreshToken?> GetByHashAsync(
        string tokenHash, CancellationToken cancellationToken = default) =>
        _inner.GetByHashAsync(tokenHash, cancellationToken);

    public Task<IReadOnlyList<RefreshToken>> GetActiveForUserAsync(
        Guid userId, CancellationToken cancellationToken = default) =>
        _inner.GetActiveForUserAsync(userId, cancellationToken);

    public Task RevokeAllForUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        _plan.CountRevokeAll();
        return _inner.RevokeAllForUserAsync(userId, cancellationToken);
    }

    public Task<bool> TryConsumeAsync(
        string tokenHash,
        Guid userId,
        string replacementTokenHash,
        DateTimeOffset consumedAt,
        CancellationToken cancellationToken = default)
    {
        _plan.CountConsume();

        if (_plan.PermanentFault is { } fault) throw fault;

        if (_plan.TransientFailuresToInject > 0)
        {
            _plan.TransientFailuresToInject--;
            throw RefreshTokenFaultPlan.TransientTransactionError(
                "Simulated primary step-down while consuming the refresh token.");
        }

        return _inner.TryConsumeAsync(
            tokenHash, userId, replacementTokenHash, consumedAt, cancellationToken);
    }

    public Task<bool> TryRevokeAsync(
        string tokenHash,
        Guid userId,
        DateTimeOffset revokedAt,
        CancellationToken cancellationToken = default) =>
        _inner.TryRevokeAsync(tokenHash, userId, revokedAt, cancellationToken);
}
