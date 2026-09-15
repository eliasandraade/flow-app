using Flow.Application.Common.Persistence;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace Flow.Infrastructure.Persistence.Mongo;

/// <summary>
/// Runs a use case inside a MongoDB multi-document transaction.
///
/// Under EF Core the atomicity of "aggregate + audit log + snapshot" came free from a
/// single SaveChanges. With explicit document writes that guarantee has to be created
/// deliberately, and this is where it is created. Requires a replica set.
///
/// Execution goes through the driver's <c>WithTransactionAsync</c> rather than a hand-rolled
/// StartTransaction/Commit pair, because a correct transaction is not just "start, write,
/// commit". Two distinct retry rules have to hold, and getting either wrong is invisible
/// until a cluster event happens in production:
///
/// - <c>TransientTransactionError</c> — the transaction as a whole may be retried. A primary
///   step-down, an election or a write conflict lands here.
/// - <c>UnknownTransactionCommitResult</c> — the commit alone may be retried, without
///   re-running the work, because the commit may already have succeeded.
///
/// The driver implements both. Reimplementing them here would be duplicating subtle logic
/// that the vendor already maintains, so the callback is handed to the official API instead.
/// </summary>
public sealed class MongoUnitOfWork : IUnitOfWork
{
    /// <summary>
    /// Majority on both ends, stated rather than inherited.
    ///
    /// The audit trail is the product's evidence of what happened; a commit acknowledged by
    /// a minority that a later election discards would take that evidence with it. Majority
    /// write concern is also what makes the commit-retry rule meaningful in the first place.
    /// </summary>
    private static readonly TransactionOptions Options = new(
        readConcern: ReadConcern.Majority,
        writeConcern: WriteConcern.WMajority);

    private readonly FlowMongoContext _context;
    private readonly MongoSessionAccessor _sessionAccessor;
    private readonly ILogger<MongoUnitOfWork> _logger;

    public MongoUnitOfWork(
        FlowMongoContext context,
        MongoSessionAccessor sessionAccessor,
        ILogger<MongoUnitOfWork> logger)
    {
        _context = context;
        _sessionAccessor = sessionAccessor;
        _logger = logger;
    }

    public Task ExecuteAsync(
        Func<CancellationToken, Task> operation, CancellationToken cancellationToken = default) =>
        ExecuteAsync<object?>(async ct =>
        {
            await operation(ct);
            return null;
        }, cancellationToken);

    public async Task<TResult> ExecuteAsync<TResult>(
        Func<CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken = default)
    {
        // Nested calls join the transaction that is already open rather than starting a
        // second one, so composing handlers stays safe.
        if (_sessionAccessor.InTransaction)
            return await operation(cancellationToken);

        using var session = await _context.Client.StartSessionAsync(
            cancellationToken: cancellationToken);

        using var scope = _sessionAccessor.Use(session);

        try
        {
            // The callback can run more than once: that is the point of the transient
            // rule, and it is safe here because everything inside writes through this
            // session, so a discarded attempt leaves nothing behind. Anything that must
            // happen exactly once — emitting metrics, for instance — belongs after this
            // call returns, not inside it.
            return await session.WithTransactionAsync(
                async (_, ct) => await operation(ct),
                Options,
                cancellationToken);
        }
        catch (Exception ex)
        {
            // Reached only once the driver has stopped retrying: either the failure was
            // never transient, or the retry budget ran out. Nothing was persisted — a state
            // transition without its audit entry is worse than a failed request.
            _logger.LogWarning(ex, "Transaction rolled back: {Message}", ex.Message);
            throw;
        }
    }
}
