using Flow.Application.Common.Persistence;
using Flow.Domain.Entities;
using MongoDB.Driver;

namespace Flow.Infrastructure.Persistence.Mongo.Repositories;

public sealed class MongoIdeaCommentRepository
    : MongoRepositoryBase<IdeaComment>, IIdeaCommentRepository
{
    public MongoIdeaCommentRepository(FlowMongoContext context, MongoSessionAccessor sessions)
        : base(context.IdeaComments, sessions) { }

    public Task AddAsync(IdeaComment comment, CancellationToken cancellationToken = default) =>
        InsertAsync(comment, cancellationToken);

    public async Task<IReadOnlyList<IdeaComment>> GetForIdeaAsync(
        Guid ideaId, CancellationToken cancellationToken = default) =>
        await Find(Filter.Eq(x => x.IdeaId, ideaId))
            .Sort(Sort.Ascending(x => x.CreatedAt))
            .ToListAsync(cancellationToken);

    public Task RemoveForIdeaAsync(Guid ideaId, CancellationToken cancellationToken = default) =>
        DeleteManyAsync(Filter.Eq(x => x.IdeaId, ideaId), cancellationToken);
}
