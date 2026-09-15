using Flow.Application.Common.Persistence;
using Flow.Domain.Entities;
using MongoDB.Driver;

namespace Flow.Infrastructure.Persistence.Mongo.Repositories;

public sealed class MongoProjectRepository : MongoRepositoryBase<Project>, IProjectRepository
{
    public MongoProjectRepository(FlowMongoContext context, MongoSessionAccessor sessions)
        : base(context.Projects, sessions) { }

    public Task<Project?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Find(Filter.Eq(x => x.Id, id)).FirstOrDefaultAsync(cancellationToken)!;

    public async Task<IReadOnlyList<Project>> QueryAsync(
        ProjectFilter filter, CancellationToken cancellationToken = default)
    {
        var conditions = new List<FilterDefinition<Project>>();

        if (filter.Status is { } status) conditions.Add(Filter.Eq(x => x.Status, status));
        if (filter.Stage is { } stage) conditions.Add(Filter.Eq(x => x.Stage, stage));
        if (filter.OwnerId is { } ownerId) conditions.Add(Filter.Eq(x => x.OwnerId, ownerId));
        if (filter.LinkedGuidelineId is { } guidelineId)
            conditions.Add(Filter.Eq(x => x.LinkedGuidelineId, guidelineId));
        if (filter.SourceIdeaId is { } ideaId)
            conditions.Add(Filter.Eq(x => x.SourceIdeaId, ideaId));

        // Authorization scope, applied last and never widened by the caller's own filters:
        // an operator sees the projects they own plus the ones their ideas produced.
        if (filter.RestrictToOperator is { } scope)
        {
            conditions.Add(Filter.Or(
                Filter.Eq(x => x.OwnerId, scope.OperatorId),
                Filter.In(x => x.SourceIdeaId, scope.OwnIdeaIds.Cast<Guid?>())));
        }

        var query = conditions.Count == 0 ? Filter.Empty : Filter.And(conditions);

        return await Find(query)
            .Sort(Sort.Descending(x => x.CreatedAt))
            .Skip(filter.Skip)
            .Limit(filter.Take)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Project>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await Find(Filter.Empty).Sort(Sort.Descending(x => x.CreatedAt)).ToListAsync(cancellationToken);

    public async Task<bool> ExistsForSourceIdeaAsync(
        Guid ideaId, CancellationToken cancellationToken = default) =>
        await CountAsync(Filter.Eq(x => x.SourceIdeaId, ideaId), cancellationToken) > 0;

    public Task AddAsync(Project project, CancellationToken cancellationToken = default) =>
        InsertAsync(project, cancellationToken);

    public Task UpdateAsync(Project project, CancellationToken cancellationToken = default) =>
        ReplaceAsync(Filter.Eq(x => x.Id, project.Id), project, upsert: false, cancellationToken);
}
