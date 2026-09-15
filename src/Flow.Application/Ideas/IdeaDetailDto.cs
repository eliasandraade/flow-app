using Flow.Domain.Entities;

namespace Flow.Application.Ideas;

public record FlowScoreDto(
    int Total,
    int StrategicAlignment,
    int Impact,
    int Feasibility,
    int Urgency,
    int Confidence,
    DateTimeOffset ComputedAt,
    int FormulaVersion)
{
    public static FlowScoreDto? From(Domain.ValueObjects.FlowScore? score) => score is null
        ? null
        : new FlowScoreDto(
            score.Total,
            score.Components.StrategicAlignment,
            score.Components.Impact,
            score.Components.Feasibility,
            score.Components.Urgency,
            score.Components.Confidence,
            score.ComputedAt,
            score.FormulaVersion);
}

public record IdeaDetailDto(
    Guid Id,
    string Title,
    string Description,
    string Problem,
    string Status,
    string Priority,
    int? Score,
    FlowScoreDto? FlowScore,
    Guid SubmittedBy,
    string SubmittedByName,
    string? ManagerComment,
    Guid? LinkedGuidelineId,
    string? LinkedGuidelineTitle,
    bool CanEdit,
    bool CanDelete,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    public static IdeaDetailDto From(Idea i, string? guidelineTitle, bool isOwner) => new(
        i.Id, i.Title, i.Description, i.Problem, i.Status.ToString(), i.Priority.ToString(),
        i.Score, FlowScoreDto.From(i.FlowScore), i.SubmittedBy, i.SubmittedByName,
        i.ManagerComment, i.LinkedGuidelineId, guidelineTitle,
        CanEdit: isOwner && i.CanBeDeleted(),
        CanDelete: isOwner && i.CanBeDeleted(),
        i.CreatedAt, i.UpdatedAt);
}
