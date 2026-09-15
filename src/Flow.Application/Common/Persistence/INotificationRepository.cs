using Flow.Domain.Entities;

namespace Flow.Application.Common.Persistence;

public interface INotificationRepository
{
    Task AddAsync(Notification notification, CancellationToken cancellationToken = default);
    Task<Notification?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Notification>> QueryAsync(NotificationFilter filter, CancellationToken cancellationToken = default);
    Task<int> CountUnreadAsync(Guid userId, CancellationToken cancellationToken = default);
    Task UpdateAsync(Notification notification, CancellationToken cancellationToken = default);
    Task MarkAllReadAsync(Guid userId, CancellationToken cancellationToken = default);
}
