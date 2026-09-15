namespace Flow.Domain.Enums;

public enum NotificationType
{
    IdeaSubmitted,
    IdeaCommented,
    IdeaApproved,
    IdeaRejected,
    IdeaAwaitingReview,
    ProjectCreated,
    ProjectBlocked,
    ProjectUnblocked,
    ProjectCompleted,
    ProjectCancelled,
    ProjectDeadlineAtRisk,
    ProjectProgressUpdated,
    ResultRecorded
}
