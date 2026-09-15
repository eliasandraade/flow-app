namespace Flow.Application.Common.Interfaces;

/// <summary>
/// Exposes the current request correlation id so that audit entries can be tied back to
/// the distributed trace that produced them.
/// </summary>
public interface ICorrelationIdAccessor
{
    string? CorrelationId { get; }
}
