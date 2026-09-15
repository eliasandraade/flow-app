using Flow.Application.Common.Persistence;
using Flow.Domain.Entities;
using Flow.Domain.Enums;
using MongoDB.Driver;

namespace Flow.Infrastructure.Persistence.Mongo.Repositories;

public sealed class MongoUserRepository : MongoRepositoryBase<User>, IUserRepository
{
    public MongoUserRepository(FlowMongoContext context, MongoSessionAccessor sessions)
        : base(context.Users, sessions) { }

    public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Find(Filter.Eq(x => x.Id, id)).FirstOrDefaultAsync(cancellationToken)!;

    public async Task<IReadOnlyList<User>> GetByIdsAsync(
        IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default)
    {
        if (ids.Count == 0) return [];
        return await Find(Filter.In(x => x.Id, ids)).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<User>> GetByRoleAsync(
        UserRole role, CancellationToken cancellationToken = default) =>
        await Find(Filter.Eq(x => x.Role, role)).ToListAsync(cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, string>> GetNamesAsync(
        IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default)
    {
        if (ids.Count == 0) return new Dictionary<Guid, string>();

        // Projection keeps this to the two fields the caller actually needs.
        var rows = await Find(Filter.In(x => x.Id, ids))
            .Project(x => new { x.Id, x.Name })
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(r => r.Id, r => r.Name);
    }

    /// <summary>
    /// Atomic increment rather than read-modify-write: two ideas from the same author
    /// approved concurrently must not lose one award.
    /// </summary>
    public Task IncrementPointsAsync(
        Guid userId, int points, CancellationToken cancellationToken = default) =>
        UpdateAsync(
            Filter.Eq(x => x.Id, userId),
            Update.Inc(x => x.Points, points).Set(x => x.UpdatedAt, DateTimeOffset.UtcNow),
            cancellationToken);
}
