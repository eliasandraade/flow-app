using Flow.Domain.Enums;
using Flow.Domain.Exceptions;

namespace Flow.Domain.Entities;

/// <summary>
/// Notification stored inside Flow itself. It exists independently of any push provider,
/// so the notification centre keeps working even when the provider is unavailable.
/// </summary>
public class Notification
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Body { get; private set; } = string.Empty;
    public NotificationType Type { get; private set; }
    public string? DeepLink { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? ReadAt { get; private set; }

    public bool IsRead => ReadAt is not null;

    private Notification() { }

    public static Notification Create(
        Guid userId,
        NotificationType type,
        string title,
        string body,
        string? deepLink = null)
    {
        if (userId == Guid.Empty)
            throw new DomainException("Notification requires a valid recipient.");
        if (string.IsNullOrWhiteSpace(title))
            throw new DomainException("Notification title is required.");

        return new Notification
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Type = type,
            Title = title,
            Body = body,
            DeepLink = deepLink,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    public void MarkAsRead()
    {
        ReadAt ??= DateTimeOffset.UtcNow;
    }
}
