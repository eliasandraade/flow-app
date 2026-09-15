using MongoDB.Driver;

namespace Flow.Infrastructure.Persistence.Mongo;

/// <summary>
/// Shared plumbing for the repositories: every write goes through the ambient transaction
/// session when one is open, and runs standalone when one is not.
///
/// Doing this in one place is what makes "the audit entry commits with the aggregate" a
/// property of the infrastructure rather than something each handler has to remember.
/// </summary>
public abstract class MongoRepositoryBase<TDocument>
{
    protected MongoRepositoryBase(IMongoCollection<TDocument> collection, MongoSessionAccessor sessions)
    {
        Collection = collection;
        Sessions = sessions;
    }

    protected IMongoCollection<TDocument> Collection { get; }
    protected MongoSessionAccessor Sessions { get; }

    /// <summary>
    /// The transaction session when one is open. Protected so a repository can reach for a
    /// driver operation the base class does not wrap, such as FindOneAndUpdate.
    /// </summary>
    protected IClientSessionHandle? ActiveSession => Sessions.InTransaction ? Sessions.Session : null;

    protected Task InsertAsync(TDocument document, CancellationToken cancellationToken)
    {
        var session = ActiveSession;
        return session is null
            ? Collection.InsertOneAsync(document, cancellationToken: cancellationToken)
            : Collection.InsertOneAsync(session, document, cancellationToken: cancellationToken);
    }

    protected Task InsertManyAsync(IEnumerable<TDocument> documents, CancellationToken cancellationToken)
    {
        var list = documents as IList<TDocument> ?? documents.ToList();
        if (list.Count == 0) return Task.CompletedTask;

        var session = ActiveSession;
        return session is null
            ? Collection.InsertManyAsync(list, cancellationToken: cancellationToken)
            : Collection.InsertManyAsync(session, list, cancellationToken: cancellationToken);
    }

    protected Task<ReplaceOneResult> ReplaceAsync(
        FilterDefinition<TDocument> filter,
        TDocument document,
        bool upsert,
        CancellationToken cancellationToken)
    {
        var options = new ReplaceOptions { IsUpsert = upsert };
        var session = ActiveSession;

        return session is null
            ? Collection.ReplaceOneAsync(filter, document, options, cancellationToken)
            : Collection.ReplaceOneAsync(session, filter, document, options, cancellationToken);
    }

    protected Task<UpdateResult> UpdateAsync(
        FilterDefinition<TDocument> filter,
        UpdateDefinition<TDocument> update,
        CancellationToken cancellationToken)
    {
        var session = ActiveSession;
        return session is null
            ? Collection.UpdateOneAsync(filter, update, cancellationToken: cancellationToken)
            : Collection.UpdateOneAsync(session, filter, update, cancellationToken: cancellationToken);
    }

    protected Task<UpdateResult> UpdateManyAsync(
        FilterDefinition<TDocument> filter,
        UpdateDefinition<TDocument> update,
        CancellationToken cancellationToken)
    {
        var session = ActiveSession;
        return session is null
            ? Collection.UpdateManyAsync(filter, update, cancellationToken: cancellationToken)
            : Collection.UpdateManyAsync(session, filter, update, cancellationToken: cancellationToken);
    }

    protected Task<DeleteResult> DeleteAsync(
        FilterDefinition<TDocument> filter, CancellationToken cancellationToken)
    {
        var session = ActiveSession;
        return session is null
            ? Collection.DeleteOneAsync(filter, cancellationToken)
            : Collection.DeleteOneAsync(session, filter, options: null, cancellationToken);
    }

    protected Task<DeleteResult> DeleteManyAsync(
        FilterDefinition<TDocument> filter, CancellationToken cancellationToken)
    {
        var session = ActiveSession;
        return session is null
            ? Collection.DeleteManyAsync(filter, cancellationToken)
            : Collection.DeleteManyAsync(session, filter, options: null, cancellationToken);
    }

    /// <summary>
    /// Reads also join the transaction when one is open, so a handler sees its own writes
    /// before the commit.
    /// </summary>
    protected IFindFluent<TDocument, TDocument> Find(FilterDefinition<TDocument> filter)
    {
        var session = ActiveSession;
        return session is null ? Collection.Find(filter) : Collection.Find(session, filter);
    }

    protected Task<long> CountAsync(
        FilterDefinition<TDocument> filter, CancellationToken cancellationToken)
    {
        var session = ActiveSession;
        return session is null
            ? Collection.CountDocumentsAsync(filter, cancellationToken: cancellationToken)
            : Collection.CountDocumentsAsync(session, filter, cancellationToken: cancellationToken);
    }

    protected static FilterDefinitionBuilder<TDocument> Filter => Builders<TDocument>.Filter;
    protected static UpdateDefinitionBuilder<TDocument> Update => Builders<TDocument>.Update;
    protected static SortDefinitionBuilder<TDocument> Sort => Builders<TDocument>.Sort;
}
