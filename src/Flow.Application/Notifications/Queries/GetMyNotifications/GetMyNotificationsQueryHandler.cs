using Flow.Application.Common.Interfaces;
using Flow.Application.Common.Persistence;
using MediatR;

namespace Flow.Application.Notifications.Queries.GetMyNotifications;

public class GetMyNotificationsQueryHandler
    : IRequestHandler<GetMyNotificationsQuery, NotificationPageDto>
{
    private readonly INotificationRepository _notifications;
    private readonly ICurrentUserService _currentUser;

    public GetMyNotificationsQueryHandler(
        INotificationRepository notifications, ICurrentUserService currentUser)
    {
        _notifications = notifications;
        _currentUser = currentUser;
    }

    public async Task<NotificationPageDto> Handle(
        GetMyNotificationsQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId
            ?? throw new InvalidOperationException("Authenticated user identity could not be resolved.");

        var items = await _notifications.QueryAsync(new NotificationFilter
        {
            UserId = userId,
            UnreadOnly = request.UnreadOnly,
            Skip = Math.Max(0, request.Skip),
            Take = Math.Clamp(request.Take, 1, 100)
        }, cancellationToken);

        var unread = await _notifications.CountUnreadAsync(userId, cancellationToken);

        return new NotificationPageDto(items.Select(NotificationDto.From).ToList(), unread);
    }
}
