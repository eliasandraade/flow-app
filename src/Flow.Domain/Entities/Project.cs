using Flow.Domain.Common;
using Flow.Domain.Enums;
using Flow.Domain.Exceptions;

namespace Flow.Domain.Entities;

public class Project : BaseEntity
{
    public const int MinProgress = 0;
    public const int MaxProgress = 100;

    public string Title { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public Guid? SourceIdeaId { get; private set; }

    /// <summary>Strategy applicable at creation time, recorded for historical traceability.</summary>
    public Guid? LinkedGuidelineId { get; private set; }

    public Guid OwnerId { get; private set; }
    public string OwnerName { get; private set; } = string.Empty;

    /// <summary>Governance state machine.</summary>
    public ProjectStatus Status { get; private set; }

    /// <summary>Execution phase. Orthogonal to Status: a Blocked project keeps its stage.</summary>
    public ProjectStage Stage { get; private set; }

    public int ProgressPercentage { get; private set; }
    public ProjectPriority Priority { get; private set; }
    public decimal? EstimatedCost { get; private set; }
    public decimal? ActualCost { get; private set; }
    public DateTimeOffset? StartDate { get; private set; }
    public DateTimeOffset? Deadline { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public string? BlockedReason { get; private set; }

    /// <summary>
    /// Materialised on entering Blocked. The previous implementation rebuilt this on every
    /// dashboard read by grouping over the ever-growing snapshot collection.
    /// </summary>
    public DateTimeOffset? BlockedSince { get; private set; }

    public string? CancelledReason { get; private set; }

    private Project() { }

    public static Project Create(
        string title,
        string description,
        Guid ownerId,
        string ownerName,
        ProjectPriority priority,
        Guid? sourceIdeaId = null,
        Guid? linkedGuidelineId = null,
        decimal? estimatedCost = null,
        DateTimeOffset? deadline = null)
    {
        if (string.IsNullOrWhiteSpace(title)) throw new DomainException("Project title is required.");
        if (string.IsNullOrWhiteSpace(description)) throw new DomainException("Project description is required.");
        if (ownerId == Guid.Empty) throw new DomainException("Project must have a valid owner.");
        if (estimatedCost is < 0m) throw new DomainException("Estimated cost cannot be negative.");

        var now = DateTimeOffset.UtcNow;
        return new Project
        {
            Title = title.Trim(),
            Description = description.Trim(),
            OwnerId = ownerId,
            OwnerName = ownerName,
            Status = ProjectStatus.Planned,
            Stage = ProjectStage.Discovery,
            ProgressPercentage = 0,
            Priority = priority,
            SourceIdeaId = sourceIdeaId,
            LinkedGuidelineId = linkedGuidelineId,
            EstimatedCost = estimatedCost,
            Deadline = deadline,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    public void Update(
        string title,
        string description,
        ProjectPriority priority,
        Guid ownerId,
        string ownerName,
        decimal? estimatedCost,
        decimal? actualCost,
        DateTimeOffset? deadline)
    {
        EnsureEditable();

        if (string.IsNullOrWhiteSpace(title)) throw new DomainException("Project title is required.");
        if (string.IsNullOrWhiteSpace(description)) throw new DomainException("Project description is required.");
        if (ownerId == Guid.Empty) throw new DomainException("Project must have a valid owner.");
        if (estimatedCost is < 0m) throw new DomainException("Estimated cost cannot be negative.");
        if (actualCost is < 0m) throw new DomainException("Actual cost cannot be negative.");

        Title = title.Trim();
        Description = description.Trim();
        Priority = priority;
        OwnerId = ownerId;
        OwnerName = ownerName;
        EstimatedCost = estimatedCost;
        ActualCost = actualCost;
        Deadline = deadline;
        SetUpdated();
    }

    public void Start()
    {
        if (Status != ProjectStatus.Planned)
            throw new DomainException("Only Planned projects can be started.");

        Status = ProjectStatus.InProgress;
        StartDate = DateTimeOffset.UtcNow;
        if (Stage == ProjectStage.Discovery) Stage = ProjectStage.Planning;
        SetUpdated();
    }

    public void Complete()
    {
        if (Status != ProjectStatus.InProgress)
            throw new DomainException("Only InProgress projects can be completed.");

        Status = ProjectStatus.Completed;
        CompletedAt = DateTimeOffset.UtcNow;
        // A completed project is by definition fully delivered.
        ProgressPercentage = MaxProgress;
        Stage = ProjectStage.Rollout;
        SetUpdated();
    }

    public void Cancel(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new DomainException("A reason is required when cancelling a project.");
        if (Status != ProjectStatus.InProgress && Status != ProjectStatus.Blocked)
            throw new DomainException("Only InProgress or Blocked projects can be cancelled.");

        Status = ProjectStatus.Cancelled;
        CancelledReason = reason;
        BlockedSince = null;
        SetUpdated();
    }

    public void Block(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new DomainException("A reason is required when blocking a project.");
        if (Status != ProjectStatus.Planned && Status != ProjectStatus.InProgress)
            throw new DomainException("Only Planned or InProgress projects can be blocked.");

        Status = ProjectStatus.Blocked;
        BlockedReason = reason;
        BlockedSince = DateTimeOffset.UtcNow;
        SetUpdated();
    }

    public void Unblock()
    {
        if (Status != ProjectStatus.Blocked)
            throw new DomainException("Only Blocked projects can be unblocked.");

        Status = ProjectStatus.InProgress;
        BlockedReason = null;
        BlockedSince = null;
        SetUpdated();
    }

    /// <summary>Dedicated progress operation. Progress is never a side effect of an edit.</summary>
    public void UpdateProgress(int progressPercentage)
    {
        EnsureEditable();
        if (progressPercentage < MinProgress || progressPercentage > MaxProgress)
            throw new DomainException($"Progress must be between {MinProgress} and {MaxProgress}.");
        if (Status == ProjectStatus.Planned && progressPercentage > 0)
            throw new DomainException("A Planned project cannot report progress before it starts.");
        if (progressPercentage == MaxProgress && Status != ProjectStatus.Completed)
            throw new DomainException(
                "Progress reaches 100 only by completing the project, so status and progress cannot disagree.");

        ProgressPercentage = progressPercentage;
        SetUpdated();
    }

    public void AdvanceStage(ProjectStage stage)
    {
        EnsureEditable();
        if (Status == ProjectStatus.Planned && stage > ProjectStage.Planning)
            throw new DomainException("A Planned project cannot move past the Planning stage.");

        Stage = stage;
        SetUpdated();
    }

    public bool IsOverdueAt(DateTimeOffset at) =>
        Deadline is not null
        && Status is ProjectStatus.InProgress or ProjectStatus.Blocked or ProjectStatus.Planned
        && Deadline.Value < at;

    /// <summary>At risk: deadline close and delivery clearly behind, or blocked with a deadline ahead.</summary>
    public bool IsAtRiskAt(DateTimeOffset at, int horizonDays = 14)
    {
        if (Deadline is null) return false;
        if (Status is ProjectStatus.Completed or ProjectStatus.Cancelled) return false;
        if (IsOverdueAt(at)) return false;

        var daysLeft = (Deadline.Value - at).TotalDays;
        if (daysLeft > horizonDays) return false;

        return Status == ProjectStatus.Blocked || ProgressPercentage < 75;
    }

    private void EnsureEditable()
    {
        if (Status is ProjectStatus.Completed or ProjectStatus.Cancelled)
            throw new DomainException("Completed or Cancelled projects cannot be edited.");
    }
}
