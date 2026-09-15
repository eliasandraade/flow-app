using Flow.Domain.Exceptions;

namespace Flow.Domain.ValueObjects;

/// <summary>
/// Transparent prioritisation score for an idea, in the range 0..100.
///
/// The formula combines three established frameworks: weighted scoring for the additive
/// dimensions (with strategic alignment carrying the largest weight, which is the product
/// thesis), WSJF's separation of time-criticality from business value, and RICE's use of
/// confidence as a multiplier that discounts an estimate rather than inflating it.
///
/// Full rationale, worked examples and guaranteed properties: docs/sprint-2/flowscore.md
/// </summary>
public sealed record FlowScore
{
    /// <summary>Bump when the weights or the shape of the formula change, so historical scores stay interpretable.</summary>
    public const int CurrentFormulaVersion = 1;

    private const double WeightStrategicAlignment = 0.35;
    private const double WeightImpact = 0.25;
    private const double WeightFeasibility = 0.25;
    private const double WeightUrgency = 0.15;

    /// <summary>Confidence never zeroes a score: it scales it within [0.6, 1.0].</summary>
    private const double ConfidenceFloor = 0.6;
    private const double ConfidenceRange = 0.4;

    public int Total { get; private set; }
    public FlowScoreComponents Components { get; private set; } = null!;
    public DateTimeOffset ComputedAt { get; private set; }
    public int FormulaVersion { get; private set; }

    // Parameterless constructor kept private for the persistence mapper only.
    private FlowScore() { }

    private FlowScore(int total, FlowScoreComponents components, DateTimeOffset computedAt, int formulaVersion)
    {
        Total = total;
        Components = components;
        ComputedAt = computedAt;
        FormulaVersion = formulaVersion;
    }

    public static FlowScore Compute(FlowScoreComponents components, DateTimeOffset? computedAt = null)
    {
        ArgumentNullException.ThrowIfNull(components);

        var total = CalculateTotal(components);

        return new FlowScore(
            total,
            components,
            computedAt ?? DateTimeOffset.UtcNow,
            CurrentFormulaVersion);
    }

    /// <summary>Rehydrates a persisted score without recomputing it, preserving historical values.</summary>
    public static FlowScore Restore(
        int total,
        FlowScoreComponents components,
        DateTimeOffset computedAt,
        int formulaVersion)
    {
        ArgumentNullException.ThrowIfNull(components);
        if (total is < 0 or > 100)
            throw new DomainException("FlowScore total must be between 0 and 100.");
        if (formulaVersion < 1)
            throw new DomainException("FlowScore formula version must be positive.");

        return new FlowScore(total, components, computedAt, formulaVersion);
    }

    private static int CalculateTotal(FlowScoreComponents c)
    {
        var weightedBase =
            WeightStrategicAlignment * c.StrategicAlignment +
            WeightImpact * c.Impact +
            WeightFeasibility * c.Feasibility +
            WeightUrgency * c.Urgency;

        var confidenceFactor = ConfidenceFloor + ConfidenceRange * (c.Confidence / 10.0);

        // AwayFromZero is load-bearing: the default banker's rounding turns an exact 50.5
        // into 50. See the worked examples in docs/sprint-2/flowscore.md.
        var total = (int)Math.Round(10.0 * weightedBase * confidenceFactor, MidpointRounding.AwayFromZero);

        return Math.Clamp(total, 0, 100);
    }

    /// <summary>
    /// Initial strategic-alignment reading derived from the linked guideline. It is a
    /// starting point for the manager, never the final word.
    /// </summary>
    public static int DeriveStrategicAlignment(bool hasGuideline, bool guidelineIsCurrent) =>
        (hasGuideline, guidelineIsCurrent) switch
        {
            (true, true) => 7,
            (true, false) => 3,
            _ => 2
        };
}
