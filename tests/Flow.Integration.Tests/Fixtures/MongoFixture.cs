using Testcontainers.MongoDb;

namespace Flow.Integration.Tests.Fixtures;

/// <summary>
/// Provides a real, disposable MongoDB for the integration suite.
///
/// A replica set is mandatory, not a preference: multi-document transactions do not exist
/// on a standalone server, and the audit guarantees these tests exist to prove are built
/// on those transactions. An in-memory fake would pass while proving nothing.
///
/// Two sources are supported:
///   1. FLOW_TEST_MONGO_URI — an already-running replica set. Useful on a developer
///      machine and on CI runners without a Docker daemon.
///   2. Testcontainers — a throwaway single-node replica set, the default.
///
/// Either way each run gets its own database name, so runs cannot contaminate each other.
/// </summary>
public sealed class MongoFixture : IAsyncLifetime
{
    public const string ExternalUriVariable = "FLOW_TEST_MONGO_URI";

    private MongoDbContainer? _container;

    public string ConnectionString { get; private set; } = string.Empty;

    // MongoDB caps database names at 63 characters, and the factory appends its own
    // suffix on top of this one, so both halves stay deliberately short.
    public string DatabaseName { get; } = $"flowtest_{Guid.NewGuid():N}"[..17];

    /// <summary>Set when no Mongo could be reached, so tests can skip rather than fail noisily.</summary>
    public string? UnavailableReason { get; private set; }

    public bool IsAvailable => UnavailableReason is null;

    public async Task InitializeAsync()
    {
        var external = Environment.GetEnvironmentVariable(ExternalUriVariable);

        if (!string.IsNullOrWhiteSpace(external))
        {
            ConnectionString = external;
            return;
        }

        try
        {
            // The image goes to the constructor: the parameterless overload is obsolete
            // in Testcontainers 4.x.
            _container = new MongoDbBuilder("mongo:8.0")
                .WithReplicaSet()
                .Build();

            await _container.StartAsync();
            ConnectionString = _container.GetConnectionString();
        }
        catch (Exception ex)
        {
            UnavailableReason =
                $"No MongoDB available for integration tests. Start Docker, or point {ExternalUriVariable} "
                + $"at a replica set. Underlying error: {ex.Message}";
        }
    }

    public async Task DisposeAsync()
    {
        if (_container is not null) await _container.DisposeAsync();
    }
}

[CollectionDefinition(Name)]
public sealed class MongoCollection : ICollectionFixture<MongoFixture>
{
    public const string Name = "mongo";
}
