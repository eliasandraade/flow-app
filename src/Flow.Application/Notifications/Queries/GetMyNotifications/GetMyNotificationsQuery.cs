using MediatR;

namespace Flow.Application.Notifications.Queries.GetMyNotifications;

public record GetMyNotificationsQuery(
    bool UnreadOnly = false,
    int Skip = 0,
    int Take = 50) : IRequest<NotificationPageDto>;
