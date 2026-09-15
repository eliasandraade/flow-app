using Flow.Application.Common.Persistence;
using Flow.Domain.Entities;
using Flow.Domain.Enums;
using MongoDB.Driver;

namespace Flow.Infrastructure.Persistence.Mongo.Repositories;

public sealed class MongoIdeaRepository : MongoRepositoryBase<Idea>, IIdeaRepository
{
    public MongoIdeaRepository(FlowMongoContext context, MongoSessionAccessor sessions)
        : base(context.Ideas, sessions) { }

    public Task<Idea?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Find(Filter.Eq(x => x.Id, id)).FirstOrDefaultAsync(cancellationToken)!;

    public async Task<IReadOnlyList<Idea>> QueryAsync(
        IdeaFilter filter, CancellationToken cancellationToken = default)
    {
        var conditions = new List<FilterDefinition<Idea>>();

        if (filter.SubmittedBy is { } submittedBy)
            conditions.Add(Filter.Eq(x => x.SubmittedBy, submittedBy));
        if (filter.Status is { } status)
            conditions.Add(Filter.Eq(x => x.Status, status));
        if (filter.Priority is { } priority)
            conditions.Add(Filter.Eq(x => x.Priority, priority));
        if (filter.LinkedGuidelineId is { } guidelineId)
            conditions.Add(Filter.Eq(x => x.LinkedGuidelineId, guidelineId));
        if (filter.MinScore is { } minScore)
            conditions.Add(Filter.Gte(x => x.Score, minScore));

        var query = conditions.Count == 0 ? Filter.Empty : Filter.And(conditions);

        var sort = filter.SortBy switch
        {
            IdeaSortOrder.ScoreDesc => Sort.Descending(x => x.Score).Descending(x => x.CreatedAt),
            IdeaSortOrder.FlowScoreDesc => Sort.Descending("flowScore.total").Descending(x => x.CreatedAt),
            IdeaSortOrder.PriorityDesc => Sort.Descending(x => x.Priority).Descending(x => x.CreatedAt),
            _ => Sort.Descending(x => x.CreatedAt)
        };

        return await Find(query)
            .Sort(sort)
            .Skip(filter.Skip)
            .Limit(filter.Take)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Idea>> GetByIdsAsync(
        IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default)
    {
        if (ids.Count == 0) return [];
        return await Find(Filter.In(x => x.Id, ids)).ToListAsync(cancellationToken);
    }

    public async Task<int> CountByStatusAsync(
        IdeaStatus status, CancellationToken cancellationToken = default) =>
        (int)await CountAsync(Filter.Eq(x => x.Status, status), cancellationToken);

    public async Task<int> CountAsync(CancellationToken cancellationToken = default) =>
        (int)await CountAsync(Filter.Empty, cancellationToken);

    public async Task<IReadOnlyList<Guid>> GetIdsSubmittedByAsync(
        Guid userId, CancellationToken cancellationToken = default) =>
        await Find(Filter.Eq(x => x.SubmittedBy, userId))
            .Project(x => x.Id)
            .ToListAsync(cancellationToken);

    public Task AddAsync(Idea idea, CancellationToken cancellationToken = default) =>
        InsertAsync(idea, cancellationToken);

    public Task UpdateAsync(Idea idea, CancellationToken cancellationToken = default) =>
        ReplaceAsync(Filter.Eq(x => x.Id, idea.Id), idea, upsert: false, cancellationToken);

    public Task RemoveAsync(Guid id, CancellationToken cancellationToken = default) =>
        DeleteAsync(Filter.Eq(x => x.Id, id), cancellationToken);
}
