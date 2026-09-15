using Flow.Domain.Exceptions;
using Flow.Domain.ValueObjects;
using FluentAssertions;

namespace Flow.Domain.Tests;

/// <summary>
/// Locks down the FlowScore formula documented in docs/sprint-2/flowscore.md.
///
/// The worked examples in that document and the cases here are the same numbers on
/// purpose: if the formula changes, both must change together and the version must be
/// bumped.
/// </summary>
public class FlowScoreTests
{
    private static FlowScoreComponents Components(int a, int i, int f, int u, int c) => new(a, i, f, u, c);

    [Theory]
    // name                          A   I   F   U   C   expected
    [InlineData("maximum",           10, 10, 10, 10, 10, 100)]
    [InlineData("minimum",            0,  0,  0,  0,  0,   0)]
    [InlineData("neutral",            5,  5,  5,  5,  5,  40)]
    [InlineData("aligned but costly",10,  8,  2,  5,  3,  49)]
    [InlineData("easy and certain",   2,  6,  9,  4, 10,  51)]
    [InlineData("strategic no data", 10,  9,  6,  8,  0,  51)]
    public void Compute_MatchesDocumentedExamples(
        string caseName, int a, int i, int f, int u, int c, int expected)
    {
        var score = FlowScore.Compute(Components(a, i, f, u, c));

        score.Total.Should().Be(expected, because: $"the documented example '{caseName}' says so");
    }

    /// <summary>
    /// This case lands on exactly 50.5. The default banker's rounding would return 50,
    /// so the away-from-zero mode is a real behavioural decision, not a detail.
    /// </summary>
    [Fact]
    public void Compute_ExactMidpoint_RoundsAwayFromZero()
    {
        var score = FlowScore.Compute(Components(2, 6, 9, 4, 10));

        score.Total.Should().Be(51);
    }

    [Fact]
    public void Compute_AlwaysWithinZeroToOneHundred()
    {
        for (var a = 0; a <= 10; a++)
        for (var i = 0; i <= 10; i += 2)
        for (var f = 0; f <= 10; f += 2)
        for (var u = 0; u <= 10; u += 5)
        for (var c = 0; c <= 10; c += 5)
        {
            FlowScore.Compute(Components(a, i, f, u, c)).Total.Should().BeInRange(0, 100);
        }
    }

    [Theory]
    [InlineData("strategicAlignment")]
    [InlineData("impact")]
    [InlineData("feasibility")]
    [InlineData("urgency")]
    [InlineData("confidence")]
    public void Compute_IsMonotonicInEveryDimension(string dimension)
    {
        var previous = -1;

        for (var value = 0; value <= 10; value++)
        {
            var components = dimension switch
            {
                "strategicAlignment" => Components(value, 5, 5, 5, 5),
                "impact" => Components(5, value, 5, 5, 5),
                "feasibility" => Components(5, 5, value, 5, 5),
                "urgency" => Components(5, 5, 5, value, 5),
                _ => Components(5, 5, 5, 5, value)
            };

            var total = FlowScore.Compute(components).Total;

            total.Should().BeGreaterThanOrEqualTo(previous,
                because: $"raising {dimension} must never lower the score");
            previous = total;
        }
    }

    [Fact]
    public void Compute_IsDeterministic()
    {
        var components = Components(7, 6, 8, 5, 6);

        var first = FlowScore.Compute(components).Total;
        var second = FlowScore.Compute(components).Total;

        second.Should().Be(first);
    }

    [Fact]
    public void Compute_PersistsComponentsThatReproduceTheTotal()
    {
        var score = FlowScore.Compute(Components(9, 8, 6, 7, 8));

        var recomputed = FlowScore.Compute(score.Components);

        recomputed.Total.Should().Be(score.Total,
            because: "the stored breakdown has to explain the stored number");
    }

    [Fact]
    public void Compute_StampsTheFormulaVersion()
    {
        FlowScore.Compute(Components(5, 5, 5, 5, 5)).FormulaVersion
            .Should().Be(FlowScore.CurrentFormulaVersion);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(11)]
    [InlineData(int.MaxValue)]
    public void Components_OutOfRange_AreRejectedRatherThanClamped(int invalid)
    {
        var act = () => Components(invalid, 5, 5, 5, 5);

        act.Should().Throw<DomainException>().WithMessage("*between 0 and 10*");
    }

    [Fact]
    public void Confidence_DiscountsButNeverZeroes()
    {
        var certain = FlowScore.Compute(Components(10, 10, 10, 10, 10)).Total;
        var blind = FlowScore.Compute(Components(10, 10, 10, 10, 0)).Total;

        blind.Should().BeLessThan(certain);
        blind.Should().Be(60, because: "the multiplier floor is 0.6, so evidence-free ideas still rank");
    }

    [Fact]
    public void StrategicAlignment_IsDerivedFromGuidelineValidity()
    {
        FlowScore.DeriveStrategicAlignment(hasGuideline: true, guidelineIsCurrent: true).Should().Be(7);
        FlowScore.DeriveStrategicAlignment(hasGuideline: true, guidelineIsCurrent: false).Should().Be(3);
        FlowScore.DeriveStrategicAlignment(hasGuideline: false, guidelineIsCurrent: false).Should().Be(2);
    }

    [Fact]
    public void Restore_KeepsHistoricalTotalWithoutRecomputing()
    {
        // A score produced by an older formula version must survive a weight change.
        var restored = FlowScore.Restore(
            total: 42,
            components: Components(1, 1, 1, 1, 1),
            computedAt: DateTimeOffset.UtcNow.AddYears(-1),
            formulaVersion: 1);

        restored.Total.Should().Be(42);
    }

    [Fact]
    public void Restore_RejectsATotalOutsideTheScale()
    {
        var act = () => FlowScore.Restore(101, Components(5, 5, 5, 5, 5), DateTimeOffset.UtcNow, 1);

        act.Should().Throw<DomainException>();
    }
}
