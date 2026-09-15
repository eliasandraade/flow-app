using MongoDB.Driver;

namespace Flow.Infrastructure.Persistence.Mongo;

/// <summary>
/// Holds the transaction session for the current request scope.
///
/// This type is internal to Infrastructure on purpose: it is how repositories find the
/// ambient transaction without IClientSessionHandle ever appearing in an Application
/// signature.
/// </summary>
public sealed class MongoSessionAccessor
{
    public IClientSessionHandle? Session { get; private set; }

    public bool InTransaction => Session is { IsInTransaction: true };

    public IDisposable Use(IClientSessionHandle session)
    {
        Session = session;
        return new Scope(this);
    }

    private sealed class Scope : IDisposable
    {
        private readonly MongoSessionAccessor _accessor;
        public Scope(MongoSessionAccessor accessor) => _accessor = accessor;
        public void Dispose() => _accessor.Session = null;
    }
}
