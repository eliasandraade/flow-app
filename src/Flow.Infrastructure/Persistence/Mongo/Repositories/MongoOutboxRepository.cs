using Flow.Application.Common.Persistence;
using Flow.Domain.Entities;
using Flow.Domain.Enums;
using MongoDB.Driver;

namespace Flow.Infrastructure.Persistence.Mongo.Repositories;

public sealed class MongoOutboxRepository : MongoRepositoryBase<OutboxMessage>, IOutboxRepository
{
    public MongoOutboxRepository(FlowMongoContext context, MongoSessionAccessor sessions)
        : base(context.NotificationOutbox, sessions) { }

    /// <summary>
    /// Idempotency is enforced by the unique index on dedupeKey rather than by a
    /// read-then-write check, which would race with a concurrent dispatch.
    /// </summary>
    public async Task<bool> TryAddAsync(
        OutboxMessage message, CancellationToken cancellationToken = default)
    {
        try
        {
            await InsertAsync(message, cancellationToken);
            return true;
        }
        catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            return false;
        }
        catch (MongoBulkWriteException ex)
            when (ex.WriteErrors.Any(e => e.Category == ServerErrorCategory.DuplicateKey))
        {
            return false;
        }
    }

    /// <summary>
    /// One FindOneAndUpdate per message: the server picks a due document and flips it to
    /// Processing in the same operation, so a second worker arriving at the same instant
    /// finds it no longer matching and moves on to the next one.
    ///
    /// Deliberately not a find followed by an update. That leaves a window between reading
    /// and writing, and it is precisely in that window that two replicas dispatch the same
    /// notification.
    ///
    /// Deliberately not a batch update either: MongoDB can only tell us how many documents
    /// an updateMany touched, not which ones, and a worker has to know exactly what it owns.
    /// </summary>
    public async Task<IReadOnlyList<OutboxMessage>> ClaimDueAsync(
        string owner,
        DateTimeOffset now,
        TimeSpan lease,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var claimable = Filter.Or(
            // Waiting for a first attempt, or backing off after a failed one.
            Filter.And(
                Filter.In(x => x.Status, new[] { OutboxStatus.Pending, OutboxStatus.Failed }),
                Filter.Lte(x => x.NextAttemptAt, now)),
            // Abandoned by a worker that never came back.
            Filter.And(
                Filter.Eq(x => x.Status, OutboxStatus.Processing),
                Filter.Lte(x => x.LeaseExpiresAt, now)));

        var options = new FindOneAndUpdateOptions<OutboxMessage>
        {
            Sort = Sort.Ascending(x => x.NextAttemptAt),
            ReturnDocument = ReturnDocument.After
        };

        var claimed = new List<OutboxMessage>(limit);

        for (var i = 0; i < limit; i++)
        {
            // A fresh token per claim, so a write from a previous claim of the same message
            // by the same worker is still rejected.
            var claim = Update
                .Set(x => x.Status, OutboxStatus.Processing)
                .Set(x => x.LeaseOwner, owner)
                .Set(x => x.LeaseToken, (Guid?)Guid.NewGuid())
                .Set(x => x.LeaseExpiresAt, (DateTimeOffset?)now.Add(lease))
                .Set(x => x.ClaimedAt, (DateTimeOffset?)now);

            var session = ActiveSession;

            var message = session is null
                ? await Collection.FindOneAndUpdateAsync(
                    claimable, claim, options, cancellationToken)
                : await Collection.FindOneAndUpdateAsync(
                    session, claimable, claim, options, cancellationToken);

            if (message is null) break;

            claimed.Add(message);
        }

        return claimed;
    }

    /// <summary>
    /// Fenced write: the claim this worker is holding is part of the filter, so a write
    /// arriving after the lease lapsed finds nothing to replace and reports it.
    ///
    /// MatchedCount rather than ModifiedCount, because the question is ownership, not
    /// whether the document happened to change.
    /// </summary>
    public async Task<bool> TryCompleteAsync(
        OutboxMessage message,
        Guid leaseToken,
        CancellationToken cancellationToken = default)
    {
        var stillOurs = Filter.And(
            Filter.Eq(x => x.Id, message.Id),
            Filter.Eq(x => x.Status, OutboxStatus.Processing),
            Filter.Eq(x => x.LeaseToken, (Guid?)leaseToken));

        var result = await ReplaceAsync(stillOurs, message, upsert: false, cancellationToken);

        return result.MatchedCount == 1;
    }

    public async Task<int> CountByStatusAsync(
        OutboxStatus status, CancellationToken cancellationToken = default) =>
        (int)await CountAsync(Filter.Eq(x => x.Status, status), cancellationToken);
}
