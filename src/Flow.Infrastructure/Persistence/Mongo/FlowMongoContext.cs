using Flow.Domain.Entities;
using Flow.Infrastructure.Identity;
using MongoDB.Driver;

namespace Flow.Infrastructure.Persistence.Mongo;

/// <summary>
/// Typed access to the collections. Registered as a singleton alongside the MongoClient:
/// the driver owns the connection pool internally, so creating a client per request is
/// the classic way to exhaust connections under load.
/// </summary>
public sealed class FlowMongoContext
{
    public FlowMongoContext(IMongoClient client, string databaseName)
    {
        Client = client;
        Database = client.GetDatabase(databaseName);
    }

    public IMongoClient Client { get; }
    public IMongoDatabase Database { get; }

    public IMongoCollection<User> Users => Database.GetCollection<User>("users");
    public IMongoCollection<Role> Roles => Database.GetCollection<Role>("roles");
    public IMongoCollection<RefreshToken> RefreshTokens => Database.GetCollection<RefreshToken>("refresh_tokens");
    public IMongoCollection<StrategicGuideline> Guidelines => Database.GetCollection<StrategicGuideline>("strategic_guidelines");
    public IMongoCollection<StrategicGuidelineHistoryEntry> GuidelineHistory => Database.GetCollection<StrategicGuidelineHistoryEntry>("strategic_guideline_history");
    public IMongoCollection<Idea> Ideas => Database.GetCollection<Idea>("ideas");
    public IMongoCollection<IdeaComment> IdeaComments => Database.GetCollection<IdeaComment>("idea_comments");
    public IMongoCollection<Project> Projects => Database.GetCollection<Project>("projects");
    public IMongoCollection<ProjectSnapshot> ProjectSnapshots => Database.GetCollection<ProjectSnapshot>("project_snapshots");
    public IMongoCollection<Result> Results => Database.GetCollection<Result>("results");
    public IMongoCollection<PointLedgerEntry> PointLedger => Database.GetCollection<PointLedgerEntry>("point_ledger");
    public IMongoCollection<AuditLog> AuditLogs => Database.GetCollection<AuditLog>("audit_logs");
    public IMongoCollection<Notification> Notifications => Database.GetCollection<Notification>("notifications");
    public IMongoCollection<OutboxMessage> NotificationOutbox => Database.GetCollection<OutboxMessage>("notification_outbox");
    public IMongoCollection<AssistantRun> AssistantRuns => Database.GetCollection<AssistantRun>("assistant_runs");
}
