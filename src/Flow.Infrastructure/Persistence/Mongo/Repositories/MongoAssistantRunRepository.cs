using Flow.Application.Common.Persistence;
using Flow.Domain.Entities;
using MongoDB.Driver;

namespace Flow.Infrastructure.Persistence.Mongo.Repositories;

public sealed class MongoAssistantRunRepository
    : MongoRepositoryBase<AssistantRun>, IAssistantRunRepository
{
    public MongoAssistantRunRepository(FlowMongoContext context, MongoSessionAccessor sessions)
        : base(context.AssistantRuns, sessions) { }

    public Task AddAsync(AssistantRun run, CancellationToken cancellationToken = default) =>
        InsertAsync(run, cancellationToken);

    public Task<AssistantRun?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Find(Filter.Eq(x => x.Id, id)).FirstOrDefaultAsync(cancellationToken)!;

    public Task UpdateAsync(AssistantRun run, CancellationToken cancellationToken = default) =>
        ReplaceAsync(Filter.Eq(x => x.Id, run.Id), run, upsert: false, cancellationToken);

    public async Task<IReadOnlyList<AssistantRun>> GetForUserAsync(
        Guid userId, int take, CancellationToken cancellationToken = default) =>
        await Find(Filter.Eq(x => x.UserId, userId))
            .Sort(Sort.Descending(x => x.RequestedAt))
            .Limit(take)
            .ToListAsync(cancellationToken);
}
