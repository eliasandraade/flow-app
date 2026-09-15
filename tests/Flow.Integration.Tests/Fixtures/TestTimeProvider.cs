namespace Flow.Integration.Tests.Fixtures;

/// <summary>
/// A clock the test moves by hand.
///
/// Lease expiry is a time-dependent rule, and the honest way to test it is to control the
/// time rather than to sleep past it. Sleeping makes the suite slow and, worse, flaky: the
/// margin that passes on a quiet machine fails on a loaded one.
///
/// This is <see cref="TimeProvider"/> from the framework rather than an abstraction of our
/// own, so production code depends on nothing that exists only for tests.
/// </summary>
public sealed class TestTimeProvider : TimeProvider
{
    private DateTimeOffset _now;

    /// <summary>
    /// Starts at the real current instant by default, because the rows under test are
    /// created through the API and carry real timestamps. What matters is not where the
    /// clock starts but that it only moves when the test says so.
    /// </summary>
    public TestTimeProvider(DateTimeOffset? start = null) =>
        _now = start ?? DateTimeOffset.UtcNow;

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan by) => _now = _now.Add(by);

    public void Set(DateTimeOffset to) => _now = to;
}
