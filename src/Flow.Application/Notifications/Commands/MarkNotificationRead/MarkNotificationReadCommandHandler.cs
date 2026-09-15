using Flow.Application.Common.Exceptions;
using Flow.Application.Common.Interfaces;
using Flow.Application.Common.Persistence;
using MediatR;

namespace Flow.Application.Notifications.Commands.MarkNotificationRead;

public class MarkNotificationReadCommandHandler : IRequestHandler<MarkNotificationReadCommand>
{
    private readonly INotificationRepository _notifications;
    private readonly ICurrentUserService _currentUser;

    public MarkNotificationReadCommandHandler(
        INotificationRepository notifications, ICurrentUserService currentUser)
    {
        _notifications = notifications;
        _currentUser = currentUser;
    }

    public async Task Handle(
        MarkNotificationReadCommand request, CancellationToken cancellationToken)
    {
        var notification = await _notifications.GetByIdAsync(request.NotificationId, cancellationToken)
            ?? throw new NotFoundException("Notification", request.NotificationId);

        // Notifications are personal: reading someone else's is not a 404 by accident,
        // it is a deliberate authorization decision.
        if (notification.UserId != _currentUser.UserId)
            throw new ForbiddenException("This notification does not belong to you.");

        if (notification.IsRead) return;

        notification.MarkAsRead();
        await _notifications.UpdateAsync(notification, cancellationToken);
    }
}
