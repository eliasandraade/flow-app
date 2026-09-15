using Flow.Domain.Common;
using Flow.Domain.Enums;
using Flow.Domain.Exceptions;
using Flow.Domain.ValueObjects;

namespace Flow.Domain.Entities;

public class Idea : BaseEntity
{
    public const int MinScore = 0;
    public const int MaxScore = 100;

    public string Title { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public string Problem { get; private set; } = string.Empty;
    public Guid SubmittedBy { get; private set; }
    public string SubmittedByName { get; private set; } = string.Empty;
    public IdeaStatus Status { get; private set; }
    public IdeaPriority Priority { get; private set; }

    /// <summary>Manager's direct 0..100 rating. Sovereign over any suggestion.</summary>
    public int? Score { get; private set; }

    /// <summary>Deterministic, explainable prioritisation. Independent of any AI provider.</summary>
    public FlowScore? FlowScore { get; private set; }

    public string? ManagerComment { get; private set; }
    public Guid? LinkedGuidelineId { get; private set; }

    private Idea() { }

    public static Idea Create(
        string title,
        string description,
        string problem,
        Guid submittedBy,
        string submittedByName,
        Guid? linkedGuidelineId = null)
    {
        ValidateText(title, description, problem);
        if (submittedBy == Guid.Empty)
            throw new DomainException("Idea must reference a valid submitter.");

        var now = DateTimeOffset.UtcNow;
        return new Idea
        {
            Title = title.Trim(),
            Description = description.Trim(),
            Problem = problem.Trim(),
            SubmittedBy = submittedBy,
            SubmittedByName = submittedByName,
            Status = IdeaStatus.Draft,
            Priority = IdeaPriority.Medium,
            LinkedGuidelineId = linkedGuidelineId,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    public void Update(string title, string description, string problem, Guid? linkedGuidelineId)
    {
        if (Status != IdeaStatus.Draft)
            throw new DomainException("Only Draft ideas can be edited.");

        ValidateText(title, description, problem);

        Title = title.Trim();
        Description = description.Trim();
        Problem = problem.Trim();
        LinkedGuidelineId = linkedGuidelineId;
        SetUpdated();
    }

    public void Submit()
    {
        if (Status != IdeaStatus.Draft)
            throw new DomainException("Only Draft ideas can be submitted.");

        Status = IdeaStatus.UnderReview;
        SetUpdated();
    }

    public void Approve(string? managerComment = null)
    {
        if (Status != IdeaStatus.UnderReview)
            throw new DomainException("Only ideas under review can be approved.");

        Status = IdeaStatus.Approved;
        ManagerComment = managerComment;
        SetUpdated();
    }

    public void Reject(string? managerComment = null)
    {
        if (Status != IdeaStatus.UnderReview)
            throw new DomainException("Only ideas under review can be rejected.");

        Status = IdeaStatus.Rejected;
        ManagerComment = managerComment;
        SetUpdated();
    }

    public void SetPriority(IdeaPriority priority)
    {
        EnsureDecisionPending("Priority");
        Priority = priority;
        SetUpdated();
    }

    /// <summary>
    /// Manager's manual score. Distinct from <see cref="Priority"/> (a qualitative label)
    /// and from <see cref="FlowScore"/> (a calculated recommendation). None overwrites another.
    /// </summary>
    public void SetScore(int score)
    {
        EnsureDecisionPending("Score");
        if (score < MinScore || score > MaxScore)
            throw new DomainException($"Idea score must be between {MinScore} and {MaxScore}.");

        Score = score;
        SetUpdated();
    }

    public void SetFlowScore(FlowScore flowScore)
    {
        ArgumentNullException.ThrowIfNull(flowScore);
        EnsureDecisionPending("FlowScore");

        FlowScore = flowScore;
        SetUpdated();
    }

    public bool CanBeDeleted() => Status == IdeaStatus.Draft;

    private void EnsureDecisionPending(string field)
    {
        if (Status is IdeaStatus.Approved or IdeaStatus.Rejected)
            throw new DomainException(
                $"{field} cannot be changed on ideas that are already approved or rejected.");
    }

    private static void ValidateText(string title, string description, string problem)
    {
        if (string.IsNullOrWhiteSpace(title)) throw new DomainException("Idea title is required.");
        if (string.IsNullOrWhiteSpace(description)) throw new DomainException("Idea description is required.");
        if (string.IsNullOrWhiteSpace(problem)) throw new DomainException("Idea problem statement is required.");
    }
}
