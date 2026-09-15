namespace Flow.Infrastructure.Persistence.Mongo;

public sealed class MongoOptions
{
    public const string SectionName = "Mongo";

    /// <summary>
    /// Must point at a replica set. Multi-document transactions are not available on a
    /// standalone server, and the audit guarantees depend on them.
    /// </summary>
    public string ConnectionString { get; set; } = "mongodb://localhost:27017/?replicaSet=rs0";

    public string Database { get; set; } = "flow";

    /// <summary>Creating indexes at startup is idempotent; disable only for read-only replicas.</summary>
    public bool EnsureIndexes { get; set; } = true;
}
