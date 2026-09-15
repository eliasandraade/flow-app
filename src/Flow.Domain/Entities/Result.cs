using Flow.Domain.Common;
using Flow.Domain.Exceptions;
using Flow.Domain.ValueObjects;

namespace Flow.Domain.Entities;

public class Result : BaseEntity
{
    private const decimal MinPercentGain = -100m;
    private const decimal MaxPercentGain = 1000m;

    public Guid ProjectId { get; private set; }

    /// <summary>Planning figures. Never touched by <see cref="SetActual"/>.</summary>
    public ResultMeasurement? Estimated { get; private set; }

    /// <summary>Post-completion measurement. Never touched by <see cref="SetEstimated"/>.</summary>
    public ResultMeasurement? Actual { get; private set; }

    public int? PaybackPeriodMonths { get; private set; }

    // Non-financial outcomes. A process improvement can be highly valuable while moving
    // revenue very little, so the platform would under-report its own impact without these.
    public decimal? ProductivityGainPercent { get; private set; }
    public decimal? TimeSavedHours { get; private set; }
    public decimal? QualityGainPercent { get; private set; }

    public string? Notes { get; private set; }
    public Guid RecordedBy { get; private set; }

    private Result() { }

    public static Result Create(Guid projectId, Guid recordedBy)
    {
        if (projectId == Guid.Empty)
            throw new DomainException("ProjectId is required.");
        if (recordedBy == Guid.Empty)
            throw new DomainException("RecordedBy is required.");

        return new Result
        {
            ProjectId = projectId,
            RecordedBy = recordedBy
        };
    }

    public void SetEstimated(decimal? revenue, decimal? savings, decimal? cost)
    {
        Estimated = ResultMeasurement.Record(revenue, savings, cost);
        SetUpdated();
    }

    public void SetActual(decimal? revenue, decimal? savings, decimal? cost)
    {
        Actual = ResultMeasurement.Record(revenue, savings, cost);
        SetUpdated();
    }

    public void SetImpactMetrics(
        decimal? productivityGainPercent,
        decimal? timeSavedHours,
        decimal? qualityGainPercent)
    {
        ValidatePercent(productivityGainPercent, nameof(productivityGainPercent));
        ValidatePercent(qualityGainPercent, nameof(qualityGainPercent));
        if (timeSavedHours is < 0m)
            throw new DomainException("TimeSavedHours cannot be negative.");

        ProductivityGainPercent = productivityGainPercent;
        TimeSavedHours = timeSavedHours;
        QualityGainPercent = qualityGainPercent;
        SetUpdated();
    }

    public void SetNotes(int? paybackPeriodMonths, string? notes)
    {
        if (paybackPeriodMonths is < 0)
            throw new DomainException("PaybackPeriodMonths cannot be negative.");

        PaybackPeriodMonths = paybackPeriodMonths;
        Notes = notes;
        SetUpdated();
    }

    private static void ValidatePercent(decimal? value, string name)
    {
        if (value is null) return;
        if (value.Value < MinPercentGain || value.Value > MaxPercentGain)
            throw new DomainException(
                $"{name} must be between {MinPercentGain} and {MaxPercentGain}.");
    }
}
