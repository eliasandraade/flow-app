using Flow.Domain.Entities;
using Flow.Domain.Enums;
using Flow.Domain.Exceptions;
using FluentAssertions;

namespace Flow.Domain.Tests;

public class StrategicGuidelineTests
{
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset End = new(2026, 12, 31, 23, 59, 59, TimeSpan.Zero);

    private static StrategicGuideline Guideline() =>
        StrategicGuideline.Create(
            "Reduzir retrabalho", "Descrição da diretriz",
            GuidelineCategory.OperationalEfficiency, "Onda Operacional 2026",
            Start, End, Guid.NewGuid());

    /// <summary>A guideline with no end date, which stays in force until it is closed.</summary>
    private static StrategicGuideline OpenEndedGuideline() =>
        StrategicGuideline.Create(
            "Reduzir retrabalho", "Descrição da diretriz",
            GuidelineCategory.OperationalEfficiency, "Onda Operacional 2026",
            Start, null, Guid.NewGuid());

    [Fact]
    public void Create_KeepsCategoryAndCampaign()
    {
        var guideline = Guideline();

        guideline.Category.Should().Be(GuidelineCategory.OperationalEfficiency);
        guideline.Campaign.Should().Be("Onda Operacional 2026");
    }

    [Fact]
    public void Create_TreatsAnEmptyCampaignAsAbsent()
    {
        var guideline = StrategicGuideline.Create(
            "Título", "Descrição", GuidelineCategory.Safety, "   ",
            Start, null, Guid.NewGuid());

        guideline.Campaign.Should().BeNull(
            because: "whitespace would otherwise become a campaign of its own in the dashboard grouping");
    }

    [Fact]
    public void Create_WithEndBeforeStart_IsRejected()
    {
        var act = () => StrategicGuideline.Create(
            "Título", "Descrição", GuidelineCategory.Quality, null,
            Start, Start.AddDays(-1), Guid.NewGuid());

        act.Should().Throw<DomainException>().WithMessage("*later than ValidFrom*");
    }

    // ─── Validity is derived, never stored as a flag ────────────────────────

    [Theory]
    [InlineData(2025, 12, 31, false)]  // before the period
    [InlineData(2026, 1, 1, true)]     // exactly at the start
    [InlineData(2026, 6, 15, true)]    // inside
    [InlineData(2026, 12, 31, true)]   // on the last day
    [InlineData(2027, 1, 2, false)]    // after
    public void IsCurrentAt_FollowsThePeriodBoundaries(int y, int m, int d, bool expected)
    {
        var guideline = Guideline();

        guideline.IsCurrentAt(new DateTimeOffset(y, m, d, 12, 0, 0, TimeSpan.Zero))
            .Should().Be(expected);
    }

    [Fact]
    public void IsCurrentAt_WithOpenEnd_StaysValidIndefinitely()
    {
        var guideline = OpenEndedGuideline();

        guideline.IsCurrentAt(Start.AddYears(20)).Should().BeTrue();
    }

    [Fact]
    public void Close_EndsValidityWithoutDeletingTheGuideline()
    {
        var guideline = OpenEndedGuideline();
        var closedAt = Start.AddMonths(6);

        guideline.Close(closedAt);

        guideline.ValidUntil.Should().Be(closedAt);
        guideline.IsCurrentAt(closedAt.AddDays(1)).Should().BeFalse();
        guideline.IsCurrentAt(closedAt.AddDays(-1)).Should().BeTrue(
            because: "closing must not rewrite the past");
    }

    [Fact]
    public void Close_Twice_IsRejected()
    {
        var guideline = OpenEndedGuideline();
        guideline.Close(Start.AddMonths(6));

        var act = () => guideline.Close(Start.AddMonths(7));

        act.Should().Throw<DomainException>().WithMessage("*already closed*");
    }

    [Fact]
    public void Close_BeforeItStarted_IsRejected()
    {
        var guideline = OpenEndedGuideline();

        var act = () => guideline.Close(Start.AddDays(-1));

        act.Should().Throw<DomainException>().WithMessage("*before it starts*");
    }

    [Fact]
    public void History_CapturesTheStateAtTheMomentOfChange()
    {
        var guideline = Guideline();
        var actorId = Guid.NewGuid();

        var entry = StrategicGuidelineHistoryEntry.Capture(
            guideline, GuidelineChangeType.Created, actorId, "Marcos Leal");

        entry.GuidelineId.Should().Be(guideline.Id);
        entry.ChangeType.Should().Be(GuidelineChangeType.Created);
        entry.ChangedBy.Should().Be(actorId);
        entry.ChangedByName.Should().Be("Marcos Leal");
        entry.Title.Should().Be(guideline.Title);
        entry.ValidFrom.Should().Be(guideline.ValidFrom);
    }

    [Fact]
    public void History_SurvivesLaterEdits()
    {
        var guideline = Guideline();
        var snapshot = StrategicGuidelineHistoryEntry.Capture(
            guideline, GuidelineChangeType.Created, Guid.NewGuid(), "Marcos");

        guideline.Update(
            "Título alterado", "Nova descrição", GuidelineCategory.Safety,
            null, Start, End);

        snapshot.Title.Should().Be("Reduzir retrabalho",
            because: "history entries are immutable records of a past state");
    }
}
