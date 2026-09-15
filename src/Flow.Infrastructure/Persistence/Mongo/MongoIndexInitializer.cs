using Flow.Domain.Entities;
using Flow.Infrastructure.Identity;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace Flow.Infrastructure.Persistence.Mongo;

/// <summary>
/// Creates every index the application's query patterns depend on.
///
/// MongoDB needs no schema migration, but it very much needs indexes, so this is the
/// Mongo equivalent of running migrations at startup. CreateOne with the same name and
/// key is a no-op, which makes repeated startups free.
///
/// The rationale for each index is in docs/sprint-2/data-model.md.
/// </summary>
public sealed class MongoIndexInitializer
{
    private readonly FlowMongoContext _context;
    private readonly ILogger<MongoIndexInitializer> _logger;

    public MongoIndexInitializer(FlowMongoContext context, ILogger<MongoIndexInitializer> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task EnsureIndexesAsync(CancellationToken cancellationToken = default)
    {
        await Users(cancellationToken);
        await Roles(cancellationToken);
        await RefreshTokens(cancellationToken);
        await Guidelines(cancellationToken);
        await Ideas(cancellationToken);
        await Projects(cancellationToken);
        await Results(cancellationToken);
        await Governance(cancellationToken);
        await Notifications(cancellationToken);
        await Assistant(cancellationToken);

        _logger.LogInformation("MongoDB indexes verified.");
    }

    private Task Users(CancellationToken ct) => _context.Users.Indexes.CreateManyAsync(
    [
        new CreateIndexModel<User>(
            Builders<User>.IndexKeys.Ascending(x => x.NormalizedEmail),
            new CreateIndexOptions { Name = "ux_users_normalizedEmail", Unique = true }),
        new CreateIndexModel<User>(
            Builders<User>.IndexKeys.Ascending(x => x.NormalizedUserName),
            new CreateIndexOptions { Name = "ux_users_normalizedUserName", Unique = true }),
        new CreateIndexModel<User>(
            Builders<User>.IndexKeys.Ascending(x => x.Roles),
            new CreateIndexOptions { Name = "ix_users_roles" }),
        new CreateIndexModel<User>(
            Builders<User>.IndexKeys.Ascending(x => x.Role),
            new CreateIndexOptions { Name = "ix_users_businessRole" })
    ], ct);

    private Task Roles(CancellationToken ct) => _context.Roles.Indexes.CreateOneAsync(
        new CreateIndexModel<Role>(
            Builders<Role>.IndexKeys.Ascending(x => x.NormalizedName),
            new CreateIndexOptions { Name = "ux_roles_normalizedName", Unique = true }), cancellationToken: ct);

    private Task RefreshTokens(CancellationToken ct) => _context.RefreshTokens.Indexes.CreateManyAsync(
    [
        new CreateIndexModel<RefreshToken>(
            Builders<RefreshToken>.IndexKeys.Ascending(x => x.TokenHash),
            new CreateIndexOptions { Name = "ux_refresh_tokenHash", Unique = true }),
        new CreateIndexModel<RefreshToken>(
            Builders<RefreshToken>.IndexKeys.Ascending(x => x.UserId),
            new CreateIndexOptions { Name = "ix_refresh_userId" }),
        // Expired tokens are rubbish, not history: the decision they belong to lives in
        // the audit log, so letting the server reap them costs nothing.
        new CreateIndexModel<RefreshToken>(
            Builders<RefreshToken>.IndexKeys.Ascending(x => x.ExpiresAt),
            new CreateIndexOptions { Name = "ttl_refresh_expiresAt", ExpireAfter = TimeSpan.Zero })
    ], ct);

    private Task Guidelines(CancellationToken ct) => _context.Guidelines.Indexes.CreateManyAsync(
    [
        new CreateIndexModel<StrategicGuideline>(
            Builders<StrategicGuideline>.IndexKeys
                .Ascending(x => x.ValidFrom).Ascending(x => x.ValidUntil),
            new CreateIndexOptions { Name = "ix_guidelines_validity" }),
        new CreateIndexModel<StrategicGuideline>(
            Builders<StrategicGuideline>.IndexKeys
                .Ascending(x => x.Category).Descending(x => x.ValidFrom),
            new CreateIndexOptions { Name = "ix_guidelines_category" }),
        new CreateIndexModel<StrategicGuideline>(
            Builders<StrategicGuideline>.IndexKeys
                .Ascending(x => x.Campaign).Descending(x => x.ValidFrom),
            new CreateIndexOptions { Name = "ix_guidelines_campaign" })
    ], ct);

    private async Task Ideas(CancellationToken ct)
    {
        await _context.Ideas.Indexes.CreateManyAsync(
        [
            new CreateIndexModel<Idea>(
                Builders<Idea>.IndexKeys.Ascending(x => x.SubmittedBy).Descending(x => x.CreatedAt),
                new CreateIndexOptions { Name = "ix_ideas_submittedBy" }),
            new CreateIndexModel<Idea>(
                Builders<Idea>.IndexKeys.Ascending(x => x.Status).Descending(x => x.CreatedAt),
                new CreateIndexOptions { Name = "ix_ideas_status" }),
            new CreateIndexModel<Idea>(
                Builders<Idea>.IndexKeys.Ascending(x => x.Status).Descending(x => x.Score),
                new CreateIndexOptions { Name = "ix_ideas_status_score" }),
            new CreateIndexModel<Idea>(
                Builders<Idea>.IndexKeys.Ascending(x => x.Priority),
                new CreateIndexOptions { Name = "ix_ideas_priority" }),
            new CreateIndexModel<Idea>(
                Builders<Idea>.IndexKeys.Ascending(x => x.LinkedGuidelineId),
                new CreateIndexOptions { Name = "ix_ideas_guideline" }),
            new CreateIndexModel<Idea>(
                Builders<Idea>.IndexKeys.Descending(x => x.CreatedAt),
                new CreateIndexOptions { Name = "ix_ideas_createdAt" }),
            new CreateIndexModel<Idea>(
                Builders<Idea>.IndexKeys.Descending("flowScore.total"),
                new CreateIndexOptions { Name = "ix_ideas_flowScore" })
        ], ct);

        await _context.IdeaComments.Indexes.CreateOneAsync(
            new CreateIndexModel<IdeaComment>(
                Builders<IdeaComment>.IndexKeys.Ascending(x => x.IdeaId).Ascending(x => x.CreatedAt),
                new CreateIndexOptions { Name = "ix_idea_comments" }), cancellationToken: ct);
    }

    private async Task Projects(CancellationToken ct)
    {
        await _context.Projects.Indexes.CreateManyAsync(
        [
            new CreateIndexModel<Project>(
                Builders<Project>.IndexKeys.Ascending(x => x.Status).Descending(x => x.CreatedAt),
                new CreateIndexOptions { Name = "ix_projects_status" }),
            new CreateIndexModel<Project>(
                Builders<Project>.IndexKeys.Ascending(x => x.Stage),
                new CreateIndexOptions { Name = "ix_projects_stage" }),
            new CreateIndexModel<Project>(
                Builders<Project>.IndexKeys.Ascending(x => x.OwnerId).Ascending(x => x.Status),
                new CreateIndexOptions { Name = "ix_projects_owner" }),
            new CreateIndexModel<Project>(
                Builders<Project>.IndexKeys.Ascending(x => x.SourceIdeaId),
                new CreateIndexOptions { Name = "ix_projects_sourceIdea" }),
            new CreateIndexModel<Project>(
                Builders<Project>.IndexKeys.Ascending(x => x.LinkedGuidelineId),
                new CreateIndexOptions { Name = "ix_projects_guideline" }),
            new CreateIndexModel<Project>(
                Builders<Project>.IndexKeys.Ascending(x => x.Deadline).Ascending(x => x.Status),
                new CreateIndexOptions { Name = "ix_projects_deadline" }),
            new CreateIndexModel<Project>(
                Builders<Project>.IndexKeys.Descending(x => x.ProgressPercentage),
                new CreateIndexOptions { Name = "ix_projects_progress" })
        ], ct);

        await _context.ProjectSnapshots.Indexes.CreateManyAsync(
        [
            new CreateIndexModel<ProjectSnapshot>(
                Builders<ProjectSnapshot>.IndexKeys
                    .Ascending(x => x.ProjectId).Descending(x => x.TakenAt),
                new CreateIndexOptions { Name = "ix_snapshots_project" }),
            new CreateIndexModel<ProjectSnapshot>(
                Builders<ProjectSnapshot>.IndexKeys
                    .Ascending(x => x.ProjectId).Ascending(x => x.TriggerAction).Descending(x => x.TakenAt),
                new CreateIndexOptions { Name = "ix_snapshots_trigger" })
        ], ct);
    }

    private Task Results(CancellationToken ct) => _context.Results.Indexes.CreateOneAsync(
        new CreateIndexModel<Result>(
            Builders<Result>.IndexKeys.Ascending(x => x.ProjectId),
            new CreateIndexOptions { Name = "ux_results_projectId", Unique = true }), cancellationToken: ct);

    private async Task Governance(CancellationToken ct)
    {
        await _context.AuditLogs.Indexes.CreateManyAsync(
        [
            new CreateIndexModel<AuditLog>(
                Builders<AuditLog>.IndexKeys
                    .Ascending(x => x.EntityType).Ascending(x => x.EntityId).Descending(x => x.Timestamp),
                new CreateIndexOptions { Name = "ix_audit_entity" }),
            new CreateIndexModel<AuditLog>(
                Builders<AuditLog>.IndexKeys.Ascending(x => x.ActorId).Descending(x => x.Timestamp),
                new CreateIndexOptions { Name = "ix_audit_actor" }),
            new CreateIndexModel<AuditLog>(
                Builders<AuditLog>.IndexKeys.Descending(x => x.Timestamp),
                new CreateIndexOptions { Name = "ix_audit_timestamp" })
        ], ct);

        await _context.PointLedger.Indexes.CreateOneAsync(
            new CreateIndexModel<PointLedgerEntry>(
                Builders<PointLedgerEntry>.IndexKeys.Ascending(x => x.UserId).Descending(x => x.AwardedAt),
                new CreateIndexOptions { Name = "ix_ledger_user" }), cancellationToken: ct);

        await _context.GuidelineHistory.Indexes.CreateOneAsync(
            new CreateIndexModel<StrategicGuidelineHistoryEntry>(
                Builders<StrategicGuidelineHistoryEntry>.IndexKeys
                    .Ascending(x => x.GuidelineId).Descending(x => x.ChangedAt),
                new CreateIndexOptions { Name = "ix_guideline_history" }), cancellationToken: ct);
    }

    private async Task Notifications(CancellationToken ct)
    {
        await _context.Notifications.Indexes.CreateManyAsync(
        [
            new CreateIndexModel<Notification>(
                Builders<Notification>.IndexKeys.Ascending(x => x.UserId).Descending(x => x.CreatedAt),
                new CreateIndexOptions { Name = "ix_notifications_user" }),
            new CreateIndexModel<Notification>(
                Builders<Notification>.IndexKeys
                    .Ascending(x => x.UserId).Ascending(x => x.ReadAt).Descending(x => x.CreatedAt),
                new CreateIndexOptions { Name = "ix_notifications_unread" })
        ], ct);

        await _context.NotificationOutbox.Indexes.CreateManyAsync(
        [
            new CreateIndexModel<OutboxMessage>(
                Builders<OutboxMessage>.IndexKeys.Ascending(x => x.Status).Ascending(x => x.NextAttemptAt),
                new CreateIndexOptions { Name = "ix_outbox_dispatch" }),
            // Serves the second half of the claim filter: messages abandoned by a worker
            // that never released its lease.
            new CreateIndexModel<OutboxMessage>(
                Builders<OutboxMessage>.IndexKeys.Ascending(x => x.Status).Ascending(x => x.LeaseExpiresAt),
                new CreateIndexOptions { Name = "ix_outbox_lease" }),
            // This unique index is the idempotency guarantee for push delivery.
            new CreateIndexModel<OutboxMessage>(
                Builders<OutboxMessage>.IndexKeys.Ascending(x => x.DedupeKey),
                new CreateIndexOptions { Name = "ux_outbox_dedupe", Unique = true })
        ], ct);
    }

    private Task Assistant(CancellationToken ct) => _context.AssistantRuns.Indexes.CreateManyAsync(
    [
        new CreateIndexModel<AssistantRun>(
            Builders<AssistantRun>.IndexKeys.Ascending(x => x.UserId).Descending(x => x.RequestedAt),
            new CreateIndexOptions { Name = "ix_assistant_user" }),
        new CreateIndexModel<AssistantRun>(
            Builders<AssistantRun>.IndexKeys.Ascending(x => x.Operation).Descending(x => x.RequestedAt),
            new CreateIndexOptions { Name = "ix_assistant_operation" })
    ], ct);
}
