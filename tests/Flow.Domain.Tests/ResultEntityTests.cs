using Flow.Domain.Entities;
using Flow.Domain.Exceptions;
using Flow.Domain.ValueObjects;
using FluentAssertions;

namespace Flow.Domain.Tests;

public class ResultEntityTests
{
    private static Result NewResult() => Result.Create(Guid.NewGuid(), Guid.NewGuid());

    [Fact]
    public void Create_StartsWithNeitherMeasurement()
    {
        var result = NewResult();

        result.Estimated.Should().BeNull();
        result.Actual.Should().BeNull();
    }

    [Fact]
    public void Create_WithoutProject_IsRejected()
    {
        var act = () => Result.Create(Guid.Empty, Guid.NewGuid());

        act.Should().Throw<DomainException>().WithMessage("*ProjectId*");
    }

    // ─── The two groups must stay independent ───────────────────────────────

    [Fact]
    public void SetEstimated_DoesNotTouchActual()
    {
        var result = NewResult();
        result.SetActual(10_000m, 5_000m, 8_000m);
        var actualBefore = result.Actual;

        result.SetEstimated(1m, 2m, 3m);

        result.Actual.Should().Be(actualBefore,
            because: "a planning figure must never overwrite a realised one");
    }

    [Fact]
    public void SetActual_DoesNotTouchEstimated()
    {
        var result = NewResult();
        result.SetEstimated(10_000m, 5_000m, 8_000m);
        var estimatedBefore = result.Estimated;

        result.SetActual(1m, 2m, 3m);

        result.Estimated.Should().Be(estimatedBefore);
    }

    // ─── ROI ────────────────────────────────────────────────────────────────

    [Fact]
    public void Roi_UsesTheDocumentedFormula()
    {
        var result = NewResult();

        // (100 + 50 - 50) / 50 * 100 = 200
        result.SetEstimated(100m, 50m, 50m);

        result.Estimated!.Roi.Should().Be(200m);
    }

    [Fact]
    public void Roi_IsNullWhenCostIsZero()
    {
        var result = NewResult();

        result.SetActual(1_000m, 0m, 0m);

        result.Actual!.Roi.Should().BeNull(because: "division by zero has no meaningful answer");
    }

    [Fact]
    public void Roi_IsNullWhenCostIsMissing()
    {
        var result = NewResult();

        result.SetActual(1_000m, 500m, null);

        result.Actual!.Roi.Should().BeNull();
    }

    [Fact]
    public void Roi_CanBeNegativeWhenTheProjectLostMoney()
    {
        var result = NewResult();

        // (0 + 20 - 100) / 100 * 100 = -80
        result.SetActual(0m, 20m, 100m);

        result.Actual!.Roi.Should().Be(-80m);
    }

    [Fact]
    public void Roi_KeepsDecimalPrecision()
    {
        var result = NewResult();

        result.SetActual(0m, 196_500m, 151_200m);

        result.Actual!.Roi.Should().BeApproximately(29.96031746031746031746031746m, 0.0000000001m);
    }

    [Theory]
    [InlineData(-1, 0, 0, "revenue")]
    [InlineData(0, -1, 0, "savings")]
    [InlineData(0, 0, -1, "cost")]
    public void Measurement_WithNegativeMoney_IsRejected(
        decimal revenue, decimal savings, decimal cost, string expected)
    {
        var act = () => ResultMeasurement.Record(revenue, savings, cost);

        act.Should().Throw<DomainException>().WithMessage($"*{expected}*");
    }

    // ─── Non-financial impact ───────────────────────────────────────────────

    [Fact]
    public void ImpactMetrics_AreStoredIndependentlyOfTheMoney()
    {
        var result = NewResult();

        result.SetImpactMetrics(11.5m, 640m, 18.5m);

        result.ProductivityGainPercent.Should().Be(11.5m);
        result.TimeSavedHours.Should().Be(640m);
        result.QualityGainPercent.Should().Be(18.5m);
        result.Estimated.Should().BeNull();
        result.Actual.Should().BeNull();
    }

    [Fact]
    public void ImpactMetrics_RejectNegativeHours()
    {
        var result = NewResult();

        var act = () => result.SetImpactMetrics(null, -1m, null);

        act.Should().Throw<DomainException>().WithMessage("*TimeSavedHours*");
    }

    [Theory]
    [InlineData(-101)]
    [InlineData(1001)]
    public void ImpactMetrics_RejectImplausiblePercentages(decimal percent)
    {
        var result = NewResult();

        var act = () => result.SetImpactMetrics(percent, null, null);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void ImpactMetrics_AllowARegression()
    {
        var result = NewResult();

        result.SetImpactMetrics(-12m, 0m, null);

        result.ProductivityGainPercent.Should().Be(-12m,
            because: "a project that made things worse still has to be reportable");
    }

    [Fact]
    public void Payback_RejectsNegativeMonths()
    {
        var result = NewResult();

        var act = () => result.SetNotes(-1, null);

        act.Should().Throw<DomainException>().WithMessage("*Payback*");
    }
}
