using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Flow.Infrastructure.Assistant;

/// <summary>
/// Stops hammering a provider that is clearly down.
///
/// After a run of consecutive failures the circuit opens for a cooldown and calls fail
/// fast, so a Gemini outage costs a few milliseconds per request instead of a full timeout
/// each. One trial request is allowed through when the cooldown expires.
///
/// Small and self-contained on purpose: the SDK does not go through IHttpClientFactory, so
/// the standard resilience handler does not apply here.
/// </summary>
public sealed class CircuitBreaker
{
    private readonly GeminiOptions _options;
    private readonly ILogger<CircuitBreaker> _logger;
    private readonly TimeProvider _time;
    private readonly object _gate = new();

    private int _consecutiveFailures;
    private DateTimeOffset? _openedAt;

    /// <summary>
    /// The clock is injected so the cooldown can be tested by moving time rather than by
    /// waiting for it. Reading DateTimeOffset.UtcNow directly forced the tests to use
    /// windows of a few dozen milliseconds and hope the machine kept up; on a shared CI
    /// runner it did not, and a correct breaker reported a failure that was really a
    /// scheduling delay.
    /// </summary>
    public CircuitBreaker(
        IOptions<GeminiOptions> options,
        ILogger<CircuitBreaker> logger,
        TimeProvider? timeProvider = null)
    {
        _options = options.Value;
        _logger = logger;
        _time = timeProvider ?? TimeProvider.System;
    }

    public bool IsOpen
    {
        get
        {
            lock (_gate) return _openedAt is not null;
        }
    }

    public bool AllowRequest()
    {
        lock (_gate)
        {
            if (_openedAt is null) return true;

            if (_time.GetUtcNow() - _openedAt.Value < _options.CircuitBreakerCooldown)
                return false;

            // Cooldown elapsed: let exactly one request through to test the water.
            _openedAt = null;
            _consecutiveFailures = _options.CircuitBreakerFailureThreshold - 1;
            _logger.LogInformation("Assistant circuit breaker is half-open; allowing a trial request.");
            return true;
        }
    }

    public void RecordSuccess()
    {
        lock (_gate)
        {
            _consecutiveFailures = 0;
            _openedAt = null;
        }
    }

    public void RecordFailure()
    {
        lock (_gate)
        {
            _consecutiveFailures++;

            if (_consecutiveFailures < _options.CircuitBreakerFailureThreshold) return;

            _openedAt = _time.GetUtcNow();
            _logger.LogWarning(
                "Assistant circuit breaker opened after {Failures} consecutive failures. "
                + "Calls will fail fast for {Cooldown}.",
                _consecutiveFailures, _options.CircuitBreakerCooldown);
        }
    }
}
