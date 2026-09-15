using Flow.Domain.Exceptions;

namespace Flow.Domain.ValueObjects;

/// <summary>
/// The five dimensions that feed the FlowScore. Each one is an integer 0..10 where
/// "higher is better" — including <see cref="Feasibility"/>, which is deliberately the
/// inverse of effort so that every additive dimension points the same way.
/// </summary>
public sealed record FlowScoreComponents
{
    public const int MinComponent = 0;
    public const int MaxComponent = 10;

    public int StrategicAlignment { get; private set; }
    public int Impact { get; private set; }
    public int Feasibility { get; private set; }
    public int Urgency { get; private set; }
    public int Confidence { get; private set; }

    // Parameterless constructor kept private for the persistence mapper only.
    private FlowScoreComponents() { }

    public FlowScoreComponents(
        int strategicAlignment,
        int impact,
        int feasibility,
        int urgency,
        int confidence)
    {
        StrategicAlignment = Require(strategicAlignment, nameof(strategicAlignment));
        Impact = Require(impact, nameof(impact));
        Feasibility = Require(feasibility, nameof(feasibility));
        Urgency = Require(urgency, nameof(urgency));
        Confidence = Require(confidence, nameof(confidence));
    }

    private static int Require(int value, string name)
    {
        if (value < MinComponent || value > MaxComponent)
            throw new DomainException(
                $"FlowScore component '{name}' must be between {MinComponent} and {MaxComponent}.");
        return value;
    }
}
