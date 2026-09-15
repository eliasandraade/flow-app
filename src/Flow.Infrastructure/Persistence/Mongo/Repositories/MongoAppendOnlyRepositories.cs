using Flow.Application.Common.Persistence;
using Flow.Domain.Entities;
using MongoDB.Driver;

namespace Flow.Infrastructure.Persistence.Mongo.Repositories;

/// <summary>
/// Append-only store for governance records. There is no update and no delete path here,
/// by design: the audit trail is only trustworthy if nothing can rewrite it.
/// </summary>
public sealed class MongoAuditLogRepository : MongoRepositoryBase<AuditLog>, IAuditLogRepository
{
    public MongoAuditLogRepository(FlowMongoContext context, MongoSessionAccessor sessions)
        : base(context.AuditLogs, sessions) { }

    public Task AppendAsync(AuditLog entry, CancellationToken cancellationToken = default) =>
        InsertAsync(entry, cancellationToken);

    public Task AppendRangeAsync(
        IEnumerable<AuditLog> entries, CancellationToken cancellationToken = default) =>
        InsertManyAsync(entries, cancellationToken);

    public async Task<IReadOnlyList<AuditLog>> GetForEntityAsync(
        string entityType, Guid entityId, CancellationToken cancellationToken = default) =>
        await Find(Filter.And(
                Filter.Eq(x => x.EntityType, entityType),
                Filter.Eq(x => x.EntityId, entityId)))
            .Sort(Sort.Ascending(x => x.Timestamp))
            .ToListAsync(cancellationToken);
}

/// <summary>Append-only, immutable project snapshots.</summary>
public sealed class MongoProjectSnapshotRepository
    : MongoRepositoryBase<ProjectSnapshot>, IProjectSnapshotRepository
{
    public MongoProjectSnapshotRepository(FlowMongoContext context, MongoSessionAccessor sessions)
        : base(context.ProjectSnapshots, sessions) { }

    public Task AppendAsync(ProjectSnapshot snapshot, CancellationToken cancellationToken = default) =>
        InsertAsync(snapshot, cancellationToken);

    public async Task<IReadOnlyList<ProjectSnapshot>> GetForProjectAsync(
        Guid projectId, CancellationToken cancellationToken = default) =>
        await Find(Filter.Eq(x => x.ProjectId, projectId))
            .Sort(Sort.Descending(x => x.TakenAt))
            .ToListAsync(cancellationToken);
}

public sealed class MongoPointLedgerRepository
    : MongoRepositoryBase<PointLedgerEntry>, IPointLedgerRepository
{
    public MongoPointLedgerRepository(FlowMongoContext context, MongoSessionAccessor sessions)
        : base(context.PointLedger, sessions) { }

    public Task AppendAsync(PointLedgerEntry entry, CancellationToken cancellationToken = default) =>
        InsertAsync(entry, cancellationToken);

    public async Task<IReadOnlyList<PointLedgerEntry>> GetForUserAsync(
        Guid userId, CancellationToken cancellationToken = default) =>
        await Find(Filter.Eq(x => x.UserId, userId))
            .Sort(Sort.Descending(x => x.AwardedAt))
            .ToListAsync(cancellationToken);

    public async Task<int> GetTotalForUserAsync(
        Guid userId, CancellationToken cancellationToken = default)
    {
        var points = await Find(Filter.Eq(x => x.UserId, userId))
            .Project(x => x.Points)
            .ToListAsync(cancellationToken);

        return points.Sum();
    }
}

public sealed class MongoGuidelineHistoryRepository
    : MongoRepositoryBase<StrategicGuidelineHistoryEntry>, IGuidelineHistoryRepository
{
    public MongoGuidelineHistoryRepository(FlowMongoContext context, MongoSessionAccessor sessions)
        : base(context.GuidelineHistory, sessions) { }

    public Task AppendAsync(
        StrategicGuidelineHistoryEntry entry, CancellationToken cancellationToken = default) =>
        InsertAsync(entry, cancellationToken);

    public async Task<IReadOnlyList<StrategicGuidelineHistoryEntry>> GetForGuidelineAsync(
        Guid guidelineId, CancellationToken cancellationToken = default) =>
        await Find(Filter.Eq(x => x.GuidelineId, guidelineId))
            .Sort(Sort.Descending(x => x.ChangedAt))
            .ToListAsync(cancellationToken);
}
