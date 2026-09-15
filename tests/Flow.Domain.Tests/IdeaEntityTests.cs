using Flow.Domain.Entities;
using Flow.Domain.Enums;
using Flow.Domain.Exceptions;
using Flow.Domain.ValueObjects;
using FluentAssertions;

namespace Flow.Domain.Tests;

public class IdeaEntityTests
{
    private static Idea Draft(Guid? guidelineId = null) => Idea.Create(
        "Sensor de vibração", "Instalar sensores nos motores críticos",
        "Paradas não programadas geram retrabalho",
        Guid.NewGuid(), "Carla Souza", guidelineId);

    private static Idea UnderReview()
    {
        var idea = Draft();
        idea.Submit();
        return idea;
    }

    [Fact]
    public void Create_StartsAsDraftWithMediumPriorityAndNoScores()
    {
        var idea = Draft();

        idea.Status.Should().Be(IdeaStatus.Draft);
        idea.Priority.Should().Be(IdeaPriority.Medium);
        idea.Score.Should().BeNull();
        idea.FlowScore.Should().BeNull();
        idea.SubmittedByName.Should().Be("Carla Souza");
    }

    [Theory]
    [InlineData("", "desc", "problema", "title")]
    [InlineData("titulo", "", "problema", "description")]
    [InlineData("titulo", "desc", "", "problem")]
    public void Create_WithMissingText_IsRejected(
        string title, string description, string problem, string expected)
    {
        var act = () => Idea.Create(title, description, problem, Guid.NewGuid(), "Carla");

        act.Should().Throw<DomainException>().WithMessage($"*{expected}*");
    }

    [Fact]
    public void Update_OnlyWorksWhileDraft()
    {
        var idea = UnderReview();

        var act = () => idea.Update("Novo", "Desc", "Problema", null);

        act.Should().Throw<DomainException>().WithMessage("*Draft*");
    }

    [Fact]
    public void Submit_MovesDraftIntoReview()
    {
        var idea = Draft();

        idea.Submit();

        idea.Status.Should().Be(IdeaStatus.UnderReview);
    }

    [Fact]
    public void Submit_Twice_IsRejected()
    {
        var idea = UnderReview();

        var act = () => idea.Submit();

        act.Should().Throw<DomainException>().WithMessage("*Draft*");
    }

    [Fact]
    public void Approve_RequiresReviewState()
    {
        var act = () => Draft().Approve("ok");

        act.Should().Throw<DomainException>().WithMessage("*under review*");
    }

    [Fact]
    public void Reject_KeepsTheManagerReasoning()
    {
        var idea = UnderReview();

        idea.Reject("Fora da competência da equipe");

        idea.Status.Should().Be(IdeaStatus.Rejected);
        idea.ManagerComment.Should().Be("Fora da competência da equipe");
    }

    // ─── Priority, score and FlowScore are three separate concepts ───────────

    [Fact]
    public void Priority_Score_And_FlowScore_AreIndependent()
    {
        var idea = UnderReview();

        idea.SetPriority(IdeaPriority.High);
        idea.SetScore(88);
        idea.SetFlowScore(FlowScore.Compute(new FlowScoreComponents(9, 9, 6, 8, 8)));

        idea.Priority.Should().Be(IdeaPriority.High);
        idea.Score.Should().Be(88);
        idea.FlowScore!.Total.Should().Be(75);
        idea.Score.Should().NotBe(idea.FlowScore.Total,
            because: "the manual score is sovereign and does not have to agree with the calculation");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void SetScore_OutOfRange_IsRejected(int score)
    {
        var idea = UnderReview();

        var act = () => idea.SetScore(score);

        act.Should().Throw<DomainException>().WithMessage("*between 0 and 100*");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(100)]
    public void SetScore_AtTheBoundaries_IsAccepted(int score)
    {
        var idea = UnderReview();

        idea.SetScore(score);

        idea.Score.Should().Be(score);
    }

    [Fact]
    public void SetScore_AfterApproval_IsRejected()
    {
        var idea = UnderReview();
        idea.Approve();

        var act = () => idea.SetScore(50);

        act.Should().Throw<DomainException>().WithMessage("*already approved or rejected*");
    }

    [Fact]
    public void SetPriority_AfterRejection_IsRejected()
    {
        var idea = UnderReview();
        idea.Reject("motivo");

        var act = () => idea.SetPriority(IdeaPriority.High);

        act.Should().Throw<DomainException>().WithMessage("*already approved or rejected*");
    }

    // ─── Deletion ───────────────────────────────────────────────────────────

    [Fact]
    public void CanBeDeleted_OnlyWhileDraft()
    {
        Draft().CanBeDeleted().Should().BeTrue();
        UnderReview().CanBeDeleted().Should().BeFalse();
    }
}

public class IdeaCommentTests
{
    [Fact]
    public void Create_KeepsAuthorIdentityForTheReviewThread()
    {
        var ideaId = Guid.NewGuid();
        var authorId = Guid.NewGuid();

        var comment = IdeaComment.Create(ideaId, authorId, "Ana Ribeiro", "  Precisa de evidência.  ");

        comment.IdeaId.Should().Be(ideaId);
        comment.AuthorId.Should().Be(authorId);
        comment.AuthorName.Should().Be("Ana Ribeiro");
        comment.Body.Should().Be("Precisa de evidência.");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithEmptyBody_IsRejected(string body)
    {
        var act = () => IdeaComment.Create(Guid.NewGuid(), Guid.NewGuid(), "Ana", body);

        act.Should().Throw<DomainException>().WithMessage("*empty*");
    }
}
