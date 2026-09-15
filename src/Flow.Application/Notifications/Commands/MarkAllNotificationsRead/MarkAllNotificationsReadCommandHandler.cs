using Flow.Application.Common.Interfaces;
using Flow.Application.Common.Persistence;
using MediatR;

namespace Flow.Application.Notifications.Commands.MarkAllNotificationsRead;

public class MarkAllNotificationsReadCommandHandler
    : IRequestHandler<MarkAllNotificationsReadCommand>
{
    private readonly INotificationRepository _notifications;
    private readonly ICurrentUserService _currentUser;

    public MarkAllNotificationsReadCommandHandler(
        INotificationRepository notifications, ICurrentUserService currentUser)
    {
        _notifications = notifications;
        _currentUser = currentUser;
    }

    public Task Handle(
        MarkAllNotificationsReadCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId
            ?? throw new InvalidOperationException("Authenticated user identity could not be resolved.");

        return _notifications.MarkAllReadAsync(userId, cancellationToken);
    }
}
