using Flow.Application.Common.Persistence;
using Flow.Domain.Entities;
using MongoDB.Driver;

namespace Flow.Infrastructure.Persistence.Mongo.Repositories;

public sealed class MongoGuidelineRepository
    : MongoRepositoryBase<StrategicGuideline>, IGuidelineRepository
{
    public MongoGuidelineRepository(FlowMongoContext context, MongoSessionAccessor sessions)
        : base(context.Guidelines, sessions) { }

    public Task<StrategicGuideline?> GetByIdAsync(
        Guid id, CancellationToken cancellationToken = default) =>
        Find(Filter.Eq(x => x.Id, id)).FirstOrDefaultAsync(cancellationToken)!;

    public async Task<IReadOnlyList<StrategicGuideline>> QueryAsync(
        GuidelineFilter filter, CancellationToken cancellationToken = default)
    {
        var conditions = new List<FilterDefinition<StrategicGuideline>>();

        if (filter.Category is { } category)
            conditions.Add(Filter.Eq(x => x.Category, category));

        if (!string.IsNullOrWhiteSpace(filter.Campaign))
            conditions.Add(Filter.Eq(x => x.Campaign, filter.Campaign.Trim()));

        if (filter.CurrentAt is { } at)
        {
            // Validity is derived, so "currently in force" is a date range query rather
            // than a stored flag that would drift out of date on its own.
            conditions.Add(Filter.Lte(x => x.ValidFrom, at));
            conditions.Add(Filter.Or(
                Filter.Eq(x => x.ValidUntil, null),
                Filter.Gte(x => x.ValidUntil, at)));
        }

        var query = conditions.Count == 0 ? Filter.Empty : Filter.And(conditions);

        return await Find(query)
            .Sort(Sort.Descending(x => x.ValidFrom).Ascending(x => x.Title))
            .Skip(filter.Skip)
            .Limit(filter.Take)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<StrategicGuideline>> GetByIdsAsync(
        IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default)
    {
        if (ids.Count == 0) return [];
        return await Find(Filter.In(x => x.Id, ids)).ToListAsync(cancellationToken);
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default) =>
        await CountAsync(Filter.Eq(x => x.Id, id), cancellationToken) > 0;

    public Task AddAsync(StrategicGuideline guideline, CancellationToken cancellationToken = default) =>
        InsertAsync(guideline, cancellationToken);

    public Task UpdateAsync(StrategicGuideline guideline, CancellationToken cancellationToken = default) =>
        ReplaceAsync(Filter.Eq(x => x.Id, guideline.Id), guideline, upsert: false, cancellationToken);

    public Task RemoveAsync(Guid id, CancellationToken cancellationToken = default) =>
        DeleteAsync(Filter.Eq(x => x.Id, id), cancellationToken);
}
