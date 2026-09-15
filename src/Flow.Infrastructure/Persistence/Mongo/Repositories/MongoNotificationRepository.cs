using Flow.Application.Common.Persistence;
using Flow.Domain.Entities;
using MongoDB.Driver;

namespace Flow.Infrastructure.Persistence.Mongo.Repositories;

public sealed class MongoNotificationRepository
    : MongoRepositoryBase<Notification>, INotificationRepository
{
    public MongoNotificationRepository(FlowMongoContext context, MongoSessionAccessor sessions)
        : base(context.Notifications, sessions) { }

    public Task AddAsync(Notification notification, CancellationToken cancellationToken = default) =>
        InsertAsync(notification, cancellationToken);

    public Task<Notification?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Find(Filter.Eq(x => x.Id, id)).FirstOrDefaultAsync(cancellationToken)!;

    public async Task<IReadOnlyList<Notification>> QueryAsync(
        NotificationFilter filter, CancellationToken cancellationToken = default)
    {
        var query = filter.UnreadOnly
            ? Filter.And(Filter.Eq(x => x.UserId, filter.UserId), Filter.Eq(x => x.ReadAt, null))
            : Filter.Eq(x => x.UserId, filter.UserId);

        return await Find(query)
            .Sort(Sort.Descending(x => x.CreatedAt))
            .Skip(filter.Skip)
            .Limit(filter.Take)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> CountUnreadAsync(Guid userId, CancellationToken cancellationToken = default) =>
        (int)await CountAsync(
            Filter.And(Filter.Eq(x => x.UserId, userId), Filter.Eq(x => x.ReadAt, null)),
            cancellationToken);

    public Task UpdateAsync(Notification notification, CancellationToken cancellationToken = default) =>
        ReplaceAsync(Filter.Eq(x => x.Id, notification.Id), notification, upsert: false, cancellationToken);

    public Task MarkAllReadAsync(Guid userId, CancellationToken cancellationToken = default) =>
        UpdateManyAsync(
            Filter.And(Filter.Eq(x => x.UserId, userId), Filter.Eq(x => x.ReadAt, null)),
            Update.Set(x => x.ReadAt, DateTimeOffset.UtcNow),
            cancellationToken);
}
