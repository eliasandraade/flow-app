using Flow.Domain.Entities;
using Flow.Domain.Enums;
using Flow.Domain.Exceptions;
using FluentAssertions;

namespace Flow.Domain.Tests;

public class ProjectEntityTests
{
    private static Project Planned(DateTimeOffset? deadline = null) => Project.Create(
        "Piloto de manutenção preditiva", "Sensores na linha 3",
        Guid.NewGuid(), "Ana Ribeiro", ProjectPriority.Medium, deadline: deadline);

    private static Project InProgress(DateTimeOffset? deadline = null)
    {
        var project = Planned(deadline);
        project.Start();
        return project;
    }

    // ─── Creation ───────────────────────────────────────────────────────────

    [Fact]
    public void Create_StartsPlannedInDiscoveryWithNoProgress()
    {
        var ownerId = Guid.NewGuid();
        var project = Project.Create(
            "Projeto", "Descrição", ownerId, "Ana Ribeiro", ProjectPriority.High);

        project.Status.Should().Be(ProjectStatus.Planned);
        project.Stage.Should().Be(ProjectStage.Discovery);
        project.ProgressPercentage.Should().Be(0);
        project.OwnerId.Should().Be(ownerId);
        project.OwnerName.Should().Be("Ana Ribeiro");
        project.StartDate.Should().BeNull();
        project.BlockedSince.Should().BeNull();
    }

    [Fact]
    public void Create_CarriesTheStrategicGuidelineForTraceability()
    {
        var guidelineId = Guid.NewGuid();
        var ideaId = Guid.NewGuid();

        var project = Project.Create(
            "Projeto", "Descrição", Guid.NewGuid(), "Ana", ProjectPriority.High,
            sourceIdeaId: ideaId, linkedGuidelineId: guidelineId);

        project.SourceIdeaId.Should().Be(ideaId);
        project.LinkedGuidelineId.Should().Be(guidelineId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithoutTitle_IsRejected(string title)
    {
        var act = () => Project.Create(title, "Desc", Guid.NewGuid(), "Ana", ProjectPriority.Low);

        act.Should().Throw<DomainException>().WithMessage("*title*");
    }

    [Fact]
    public void Create_WithNegativeCost_IsRejected()
    {
        var act = () => Project.Create(
            "Projeto", "Desc", Guid.NewGuid(), "Ana", ProjectPriority.Low, estimatedCost: -1m);

        act.Should().Throw<DomainException>().WithMessage("*negative*");
    }

    // ─── State machine ──────────────────────────────────────────────────────

    [Fact]
    public void Start_MovesOutOfDiscoveryAndStampsTheStartDate()
    {
        var project = Planned();
        var before = DateTimeOffset.UtcNow;

        project.Start();

        project.Status.Should().Be(ProjectStatus.InProgress);
        project.Stage.Should().Be(ProjectStage.Planning);
        project.StartDate.Should().NotBeNull().And.Subject.As<DateTimeOffset?>()!.Value
            .Should().BeOnOrAfter(before);
    }

    [Fact]
    public void Start_Twice_IsRejected()
    {
        var project = InProgress();

        var act = () => project.Start();

        act.Should().Throw<DomainException>().WithMessage("*Planned*");
    }

    [Fact]
    public void Complete_ForcesFullProgressAndRollout()
    {
        var project = InProgress();
        project.UpdateProgress(40);

        project.Complete();

        project.Status.Should().Be(ProjectStatus.Completed);
        project.CompletedAt.Should().NotBeNull();
        project.ProgressPercentage.Should().Be(100,
            because: "a completed project is by definition fully delivered");
        project.Stage.Should().Be(ProjectStage.Rollout);
    }

    [Fact]
    public void Complete_FromPlanned_IsRejected()
    {
        var act = () => Planned().Complete();

        act.Should().Throw<DomainException>().WithMessage("*InProgress*");
    }

    [Fact]
    public void Block_RecordsReasonAndTheMomentItStarted()
    {
        var project = InProgress();
        var before = DateTimeOffset.UtcNow;

        project.Block("Fornecedor atrasou a entrega");

        project.Status.Should().Be(ProjectStatus.Blocked);
        project.BlockedReason.Should().Be("Fornecedor atrasou a entrega");
        project.BlockedSince.Should().NotBeNull().And.Subject.As<DateTimeOffset?>()!.Value
            .Should().BeOnOrAfter(before);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Block_WithoutReason_IsRejected(string reason)
    {
        var project = InProgress();

        var act = () => project.Block(reason);

        act.Should().Throw<DomainException>().WithMessage("*reason*");
    }

    [Fact]
    public void Block_FromPlanned_IsAllowed()
    {
        var project = Planned();

        project.Block("Orçamento não aprovado");

        project.Status.Should().Be(ProjectStatus.Blocked);
    }

    [Fact]
    public void Unblock_ClearsTheBlockAndItsClock()
    {
        var project = InProgress();
        project.Block("Aguardando peça");

        project.Unblock();

        project.Status.Should().Be(ProjectStatus.InProgress);
        project.BlockedReason.Should().BeNull();
        project.BlockedSince.Should().BeNull();
    }

    [Fact]
    public void Unblock_WhenNotBlocked_IsRejected()
    {
        var act = () => InProgress().Unblock();

        act.Should().Throw<DomainException>().WithMessage("*Blocked*");
    }

    [Fact]
    public void Cancel_RequiresAReasonAndKeepsIt()
    {
        var project = InProgress();

        project.Cancel("Escopo devolvido ao backlog");

        project.Status.Should().Be(ProjectStatus.Cancelled);
        project.CancelledReason.Should().Be("Escopo devolvido ao backlog");
    }

    [Fact]
    public void Cancel_FromPlanned_IsRejected()
    {
        var act = () => Planned().Cancel("motivo");

        act.Should().Throw<DomainException>().WithMessage("*InProgress or Blocked*");
    }

    [Fact]
    public void CompletedProject_CannotBeEdited()
    {
        var project = InProgress();
        project.Complete();

        var act = () => project.Update(
            "Novo", "Desc", ProjectPriority.Low, Guid.NewGuid(), "Outro", null, null, null);

        act.Should().Throw<DomainException>().WithMessage("*cannot be edited*");
    }

    // ─── Progress ───────────────────────────────────────────────────────────

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void UpdateProgress_OutOfRange_IsRejected(int progress)
    {
        var project = InProgress();

        var act = () => project.UpdateProgress(progress);

        act.Should().Throw<DomainException>().WithMessage("*between 0 and 100*");
    }

    [Fact]
    public void UpdateProgress_To100_IsRejectedBecauseOnlyCompletionGetsThere()
    {
        var project = InProgress();

        var act = () => project.UpdateProgress(100);

        act.Should().Throw<DomainException>().WithMessage("*cannot disagree*");
    }

    [Fact]
    public void UpdateProgress_OnPlannedProject_IsRejected()
    {
        var act = () => Planned().UpdateProgress(10);

        act.Should().Throw<DomainException>().WithMessage("*before it starts*");
    }

    [Fact]
    public void UpdateProgress_OnRunningProject_IsAccepted()
    {
        var project = InProgress();

        project.UpdateProgress(55);

        project.ProgressPercentage.Should().Be(55);
    }

    // ─── Stage ──────────────────────────────────────────────────────────────

    [Fact]
    public void Stage_IsIndependentOfStatus()
    {
        var project = InProgress();
        project.AdvanceStage(ProjectStage.Execution);

        project.Block("Fornecedor");

        project.Status.Should().Be(ProjectStatus.Blocked);
        project.Stage.Should().Be(ProjectStage.Execution,
            because: "being blocked does not undo the execution phase already reached");
    }

    [Fact]
    public void AdvanceStage_BeyondPlanning_IsRejectedWhileStillPlanned()
    {
        var act = () => Planned().AdvanceStage(ProjectStage.Execution);

        act.Should().Throw<DomainException>().WithMessage("*past the Planning stage*");
    }

    // ─── Deadline health ────────────────────────────────────────────────────

    [Fact]
    public void IsOverdue_WhenDeadlinePassedAndWorkStillOpen()
    {
        var project = InProgress(deadline: DateTimeOffset.UtcNow.AddDays(-1));

        project.IsOverdueAt(DateTimeOffset.UtcNow).Should().BeTrue();
    }

    [Fact]
    public void IsOverdue_IsFalseOnceCompleted()
    {
        var project = InProgress(deadline: DateTimeOffset.UtcNow.AddDays(-1));
        project.Complete();

        project.IsOverdueAt(DateTimeOffset.UtcNow).Should().BeFalse();
    }

    [Fact]
    public void IsAtRisk_WhenDeadlineIsCloseAndDeliveryIsBehind()
    {
        var project = InProgress(deadline: DateTimeOffset.UtcNow.AddDays(5));
        project.UpdateProgress(30);

        project.IsAtRiskAt(DateTimeOffset.UtcNow).Should().BeTrue();
    }

    [Fact]
    public void IsAtRisk_IsFalseWhenDeliveryIsNearlyDone()
    {
        var project = InProgress(deadline: DateTimeOffset.UtcNow.AddDays(5));
        project.UpdateProgress(90);

        project.IsAtRiskAt(DateTimeOffset.UtcNow).Should().BeFalse();
    }

    [Fact]
    public void IsAtRisk_WhenBlockedWithADeadlineAhead()
    {
        var project = InProgress(deadline: DateTimeOffset.UtcNow.AddDays(5));
        project.UpdateProgress(95);
        project.Block("Fornecedor");

        project.IsAtRiskAt(DateTimeOffset.UtcNow).Should().BeTrue(
            because: "a stuck project is at risk regardless of how far it got");
    }

    [Fact]
    public void IsAtRisk_IsFalseWithoutADeadline()
    {
        var project = InProgress();

        project.IsAtRiskAt(DateTimeOffset.UtcNow).Should().BeFalse();
    }
}
