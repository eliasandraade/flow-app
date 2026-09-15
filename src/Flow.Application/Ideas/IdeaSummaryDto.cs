using Flow.Domain.Entities;

namespace Flow.Application.Ideas;

public record IdeaSummaryDto(
    Guid Id,
    string Title,
    string Problem,
    string Status,
    string Priority,
    int? Score,
    int? FlowScore,
    Guid SubmittedBy,
    string SubmittedByName,
    Guid? LinkedGuidelineId,
    DateTimeOffset CreatedAt)
{
    public static IdeaSummaryDto From(Idea i) => new(
        i.Id, i.Title, i.Problem, i.Status.ToString(), i.Priority.ToString(),
        i.Score, i.FlowScore?.Total, i.SubmittedBy, i.SubmittedByName,
        i.LinkedGuidelineId, i.CreatedAt);
}
