using Flow.Domain.Enums;
using Flow.Domain.Exceptions;

namespace Flow.Domain.Entities;

/// <summary>
/// Intent to deliver a push notification, written inside the same transaction as the
/// domain change and dispatched outside of it. This is what keeps provider availability
/// from deciding whether an idea approval is persisted.
/// </summary>
public class OutboxMessage
{
    public const int DefaultMaxAttempts = 6;

    public Guid Id { get; private set; }
    public Guid NotificationId { get; private set; }

    /// <summary>
    /// Idempotency key. A unique index on this field turns a reprocessed event into a
    /// failed insert instead of a duplicate push.
    /// </summary>
    public string DedupeKey { get; private set; } = string.Empty;

    public Guid UserId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Body { get; private set; } = string.Empty;
    public string? DeepLink { get; private set; }
    public OutboxStatus Status { get; private set; }
    public int AttemptCount { get; private set; }
    public DateTimeOffset NextAttemptAt { get; private set; }
    public string? LastError { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? DispatchedAt { get; private set; }

    /// <summary>Which worker holds the claim. Diagnostic; the token is what enforces it.</summary>
    public string? LeaseOwner { get; private set; }

    /// <summary>
    /// Fencing token, fresh on every claim.
    ///
    /// The owner alone is not enough to fence a write: the same worker can claim the same
    /// message again later, after its first lease lapsed and someone else worked on it, and
    /// a stale attempt would still match on the owner name. A token minted per claim makes
    /// "the claim I am holding" a different value from "the claim I held", which is exactly
    /// what a late write has to fail against.
    /// </summary>
    public Guid? LeaseToken { get; private set; }

    /// <summary>
    /// When the claim stops being respected. A worker that crashes after claiming does not
    /// take the message with it: once this passes, another worker may claim it again.
    /// </summary>
    public DateTimeOffset? LeaseExpiresAt { get; private set; }

    public DateTimeOffset? ClaimedAt { get; private set; }

    /// <summary>True when a claimed message may be taken over by another worker.</summary>
    public bool IsLeaseExpiredAt(DateTimeOffset now) =>
        Status == OutboxStatus.Processing && LeaseExpiresAt <= now;

    private OutboxMessage() { }

    public static OutboxMessage For(Notification notification, string dedupeKey)
    {
        ArgumentNullException.ThrowIfNull(notification);
        if (string.IsNullOrWhiteSpace(dedupeKey))
            throw new DomainException("Outbox message requires a dedupe key.");

        var now = DateTimeOffset.UtcNow;
        return new OutboxMessage
        {
            Id = Guid.NewGuid(),
            NotificationId = notification.Id,
            DedupeKey = dedupeKey,
            UserId = notification.UserId,
            Title = notification.Title,
            Body = notification.Body,
            DeepLink = notification.DeepLink,
            Status = OutboxStatus.Pending,
            AttemptCount = 0,
            NextAttemptAt = now,
            CreatedAt = now
        };
    }

    public void MarkDispatched(DateTimeOffset? now = null)
    {
        Status = OutboxStatus.Dispatched;
        DispatchedAt = now ?? DateTimeOffset.UtcNow;
        LastError = null;
        ReleaseLease();
    }

    /// <summary>
    /// Records a failed attempt and schedules the next one with exponential backoff plus
    /// jitter, so a provider outage does not produce a synchronised retry storm.
    /// </summary>
    public void MarkFailed(string error, int maxAttempts = DefaultMaxAttempts, DateTimeOffset? now = null)
    {
        AttemptCount++;
        LastError = Truncate(error, 1000);
        ReleaseLease();

        if (AttemptCount >= maxAttempts)
        {
            Status = OutboxStatus.DeadLettered;
            return;
        }

        Status = OutboxStatus.Failed;
        NextAttemptAt = (now ?? DateTimeOffset.UtcNow).Add(BackoffFor(AttemptCount));
    }

    /// <summary>
    /// Hands a claim back without counting an attempt, returning the message to the queue
    /// as if it had never been picked up. Used when the worker discovers it cannot even try
    /// — no push credentials, for instance — so nothing is charged against the message.
    /// </summary>
    public void ReleaseClaim()
    {
        if (Status != OutboxStatus.Processing) return;

        Status = AttemptCount == 0 ? OutboxStatus.Pending : OutboxStatus.Failed;
        ReleaseLease();
    }

    /// <summary>
    /// The claim ends with the attempt, whatever its outcome. Leaving a stale owner behind
    /// would make the next claim look like a takeover rather than an ordinary retry.
    /// </summary>
    private void ReleaseLease()
    {
        LeaseOwner = null;
        LeaseExpiresAt = null;
        LeaseToken = null;
        ClaimedAt = null;
    }

    internal static TimeSpan BackoffFor(int attempt)
    {
        var seconds = Math.Min(Math.Pow(2, attempt) * 5, 900);
        var jitter = Random.Shared.NextDouble() * seconds * 0.25;
        return TimeSpan.FromSeconds(seconds + jitter);
    }

    private static string Truncate(string value, int max) =>
        string.IsNullOrEmpty(value) || value.Length <= max ? value : value[..max];
}
