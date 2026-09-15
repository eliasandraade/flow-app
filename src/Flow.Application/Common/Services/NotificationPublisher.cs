using Flow.Application.Common.Persistence;
using Flow.Domain.Entities;
using Flow.Domain.Enums;

namespace Flow.Application.Common.Services;

/// <summary>
/// Publishes a notification to the Flow notification centre and enqueues the push intent.
///
/// Both writes happen inside the caller's transaction. The actual delivery to the push
/// provider happens later, outside of it, so provider availability never decides whether
/// a domain change is persisted.
/// </summary>
public sealed class NotificationPublisher
{
    private readonly INotificationRepository _notifications;
    private readonly IOutboxRepository _outbox;

    public NotificationPublisher(INotificationRepository notifications, IOutboxRepository outbox)
    {
        _notifications = notifications;
        _outbox = outbox;
    }

    public async Task PublishAsync(
        Guid userId,
        NotificationType type,
        string title,
        string body,
        string? deepLink,
        string dedupeKey,
        CancellationToken cancellationToken = default)
    {
        var notification = Notification.Create(userId, type, title, body, deepLink);
        await _notifications.AddAsync(notification, cancellationToken);

        var message = OutboxMessage.For(notification, dedupeKey);
        await _outbox.TryAddAsync(message, cancellationToken);
    }

    public async Task PublishManyAsync(
        IEnumerable<Guid> userIds,
        NotificationType type,
        string title,
        string body,
        string? deepLink,
        Func<Guid, string> dedupeKeyFactory,
        CancellationToken cancellationToken = default)
    {
        foreach (var userId in userIds.Distinct())
        {
            await PublishAsync(
                userId, type, title, body, deepLink, dedupeKeyFactory(userId), cancellationToken);
        }
    }
}
