using Flow.Application.Common.Persistence;
using Flow.Domain.Entities;
using MongoDB.Driver;

namespace Flow.Infrastructure.Persistence.Mongo.Repositories;

public sealed class MongoResultRepository : MongoRepositoryBase<Result>, IResultRepository
{
    public MongoResultRepository(FlowMongoContext context, MongoSessionAccessor sessions)
        : base(context.Results, sessions) { }

    public Task<Result?> GetByProjectIdAsync(
        Guid projectId, CancellationToken cancellationToken = default) =>
        Find(Filter.Eq(x => x.ProjectId, projectId)).FirstOrDefaultAsync(cancellationToken)!;

    public async Task<IReadOnlyList<Result>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await Find(Filter.Empty).ToListAsync(cancellationToken);

    /// <summary>Upsert keyed on projectId, which carries a unique index.</summary>
    public Task UpsertAsync(Result result, CancellationToken cancellationToken = default) =>
        ReplaceAsync(Filter.Eq(x => x.ProjectId, result.ProjectId), result, upsert: true, cancellationToken);
}
