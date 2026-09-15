using MediatR;

namespace Flow.Application.Notifications.Commands.MarkNotificationRead;

public record MarkNotificationReadCommand(Guid NotificationId) : IRequest;
