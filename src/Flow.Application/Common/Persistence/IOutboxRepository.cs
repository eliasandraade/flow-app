using Flow.Domain.Entities;

namespace Flow.Application.Common.Persistence;

public interface IOutboxRepository
{
    /// <summary>
    /// Returns false when the dedupe key already exists, which is how a reprocessed event
    /// is prevented from producing a duplicate push.
    /// </summary>
    Task<bool> TryAddAsync(OutboxMessage message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Takes exclusive ownership of up to <paramref name="limit"/> due messages, moving
    /// each one to Processing in the same operation that selects it.
    ///
    /// Reading the due messages and then marking them is not equivalent, and that gap is
    /// the whole problem: two replicas polling at the same moment both see the same Pending
    /// document before either writes, and the recipient gets the notification twice. The
    /// selection and the claim therefore have to be one operation, decided by the database.
    ///
    /// A claim carries a lease. A worker that is killed between claiming and delivering
    /// would otherwise strand the message in Processing forever, so once the lease lapses
    /// another worker may take it over — which covers a crashed process, a restarted pod
    /// and a deploy in the middle of a batch.
    ///
    /// Each claim carries a fresh <see cref="OutboxMessage.LeaseToken"/>, which the caller
    /// must present to <see cref="TryCompleteAsync"/> for its write to be accepted.
    /// </summary>
    /// <param name="owner">Identifies the claiming worker. Diagnostic only.</param>
    Task<IReadOnlyList<OutboxMessage>> ClaimDueAsync(
        string owner,
        DateTimeOffset now,
        TimeSpan lease,
        int limit,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes the outcome of an attempt, but only if this worker still owns the claim.
    /// Returns false when it does not, meaning the lease lapsed and someone else took over.
    ///
    /// An unconditional write by id is not good enough, and the gap is real: a worker can
    /// stall on a slow provider call for longer than its lease, another worker legitimately
    /// recovers the message and processes it, and then the first one wakes up and saves the
    /// conclusion it reached ages ago — flattening the newer state and the newer lease. The
    /// document carrying a LeaseOwner does not help unless the write actually checks it.
    ///
    /// The token is passed in rather than read from the message because completing clears
    /// the lease in memory first; the caller keeps the value it was given at claim time.
    /// </summary>
    Task<bool> TryCompleteAsync(
        OutboxMessage message,
        Guid leaseToken,
        CancellationToken cancellationToken = default);

    Task<int> CountByStatusAsync(Domain.Enums.OutboxStatus status, CancellationToken cancellationToken = default);
}
