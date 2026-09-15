namespace Flow.Application.Common.Interfaces;

/// <summary>
/// Delivers a push notification through an external provider.
///
/// Implementations must report honestly: a provider that is not configured returns
/// <see cref="PushDeliveryOutcome.NotConfigured"/> rather than pretending to have
/// delivered anything. A notification the user never received must never be recorded
/// as sent.
/// </summary>
public interface IPushNotificationSender
{
    bool IsConfigured { get; }

    Task<PushDeliveryResult> SendAsync(
        PushNotificationRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// One delivery attempt, carrying two different idempotency keys on purpose.
///
/// <paramref name="DedupeKey"/> is ours, shaped for our own storage: readable, meaningful
/// and unique per business event — "IdeaApproved:{ideaId}:{userId}". A unique index on it
/// turns a reprocessed event into a failed insert instead of a duplicate notification. It
/// is deliberately not what goes to the provider: it is not a UUID, and a provider that
/// requires one would reject or ignore it.
///
/// <paramref name="DeliveryId"/> is the provider's, and is the outbox message id. It has to
/// be stable across retries — that is the entire point of an idempotency key — so it is
/// carried in rather than generated here. Generating one per attempt would make every retry
/// look like a new message and defeat the deduplication it was asked for.
/// </summary>
public sealed record PushNotificationRequest(
    Guid UserId,
    string Title,
    string Body,
    string? DeepLink,
    string DedupeKey,
    Guid DeliveryId);

public enum PushDeliveryOutcome
{
    /// <summary>The provider accepted the message.</summary>
    Delivered,

    /// <summary>Worth retrying: timeout, 5xx, rate limit.</summary>
    TransientFailure,

    /// <summary>Not worth retrying: malformed request, unknown recipient, rejected credentials.</summary>
    PermanentFailure,

    /// <summary>No credentials configured. The message stays queued rather than being faked.</summary>
    NotConfigured
}

public sealed record PushDeliveryResult(
    PushDeliveryOutcome Outcome,
    string? Error = null,
    string? ProviderMessageId = null)
{
    public static PushDeliveryResult Delivered(string? providerMessageId = null) =>
        new(PushDeliveryOutcome.Delivered, ProviderMessageId: providerMessageId);

    public static PushDeliveryResult Transient(string error) =>
        new(PushDeliveryOutcome.TransientFailure, error);

    public static PushDeliveryResult Permanent(string error) =>
        new(PushDeliveryOutcome.PermanentFailure, error);

    public static PushDeliveryResult NotConfigured() =>
        new(PushDeliveryOutcome.NotConfigured, "Push provider is not configured.");
}
