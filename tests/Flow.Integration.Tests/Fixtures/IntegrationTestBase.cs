namespace Flow.Integration.Tests.Fixtures;

/// <summary>
/// Base for tests that need the API and a real MongoDB.
///
/// When no database can be reached the tests report the reason instead of failing with an
/// obscure connection error, so a missing Docker daemon never looks like a broken build.
/// </summary>
[Collection(MongoCollection.Name)]
public abstract class IntegrationTestBase : IAsyncLifetime
{
    private readonly MongoFixture _mongo;
    private FlowApiFactory? _factory;

    protected IntegrationTestBase(MongoFixture mongo) => _mongo = mongo;

    protected FlowApiFactory Factory => _factory
        ?? throw new InvalidOperationException(_mongo.UnavailableReason ?? "Factory not initialised.");

    protected bool DatabaseAvailable => _mongo.IsAvailable;

    protected string? SkipReason => _mongo.UnavailableReason;

    public Task InitializeAsync()
    {
        if (_mongo.IsAvailable) _factory = new FlowApiFactory(_mongo);
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _factory?.Dispose();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Fails loudly rather than passing silently when the database is missing: a green run
    /// that tested nothing is worse than a red one.
    /// </summary>
    protected void RequireDatabase()
    {
        if (!_mongo.IsAvailable)
            throw new InvalidOperationException(_mongo.UnavailableReason);
    }
}
