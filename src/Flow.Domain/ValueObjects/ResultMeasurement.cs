using Flow.Domain.Exceptions;

namespace Flow.Domain.ValueObjects;

/// <summary>
/// One independent measurement group of a project result. Estimated and actual are two
/// instances of this type and never overwrite one another, which is what keeps a planning
/// figure from silently becoming a realised one.
/// </summary>
public sealed record ResultMeasurement
{
    public decimal? Revenue { get; private set; }
    public decimal? Savings { get; private set; }
    public decimal? Cost { get; private set; }
    public decimal? Roi { get; private set; }
    public DateTimeOffset RecordedAt { get; private set; }

    // Parameterless constructor kept private for the persistence mapper only.
    private ResultMeasurement() { }

    private ResultMeasurement(
        decimal? revenue, decimal? savings, decimal? cost, decimal? roi, DateTimeOffset recordedAt)
    {
        Revenue = revenue;
        Savings = savings;
        Cost = cost;
        Roi = roi;
        RecordedAt = recordedAt;
    }

    public static ResultMeasurement Record(
        decimal? revenue, decimal? savings, decimal? cost, DateTimeOffset? recordedAt = null)
    {
        Require(revenue, nameof(revenue));
        Require(savings, nameof(savings));
        Require(cost, nameof(cost));

        return new ResultMeasurement(
            revenue, savings, cost,
            ComputeRoi(revenue, savings, cost),
            recordedAt ?? DateTimeOffset.UtcNow);
    }

    /// <summary>Rehydrates a persisted measurement without recomputing the stored ROI.</summary>
    public static ResultMeasurement Restore(
        decimal? revenue, decimal? savings, decimal? cost, decimal? roi, DateTimeOffset recordedAt) =>
        new(revenue, savings, cost, roi, recordedAt);

    /// <summary>ROI = (Revenue + Savings - Cost) / Cost * 100. Null when cost is null or zero.</summary>
    public static decimal? ComputeRoi(decimal? revenue, decimal? savings, decimal? cost)
    {
        if (cost is null || cost.Value == 0m) return null;
        return ((revenue ?? 0m) + (savings ?? 0m) - cost.Value) / cost.Value * 100m;
    }

    private static void Require(decimal? value, string name)
    {
        if (value is < 0m)
            throw new DomainException($"Result {name} cannot be negative.");
    }
}
