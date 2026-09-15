using Flow.Domain.Entities;

namespace Flow.Application.Notifications;

public record NotificationDto(
    Guid Id,
    string Type,
    string Title,
    string Body,
    string? DeepLink,
    bool IsRead,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ReadAt)
{
    public static NotificationDto From(Notification n) => new(
        n.Id, n.Type.ToString(), n.Title, n.Body, n.DeepLink, n.IsRead, n.CreatedAt, n.ReadAt);
}

public record NotificationPageDto(
    IReadOnlyList<NotificationDto> Items,
    int UnreadCount);
