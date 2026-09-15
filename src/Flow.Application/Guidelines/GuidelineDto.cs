using Flow.Domain.Entities;

namespace Flow.Application.Guidelines;

public record GuidelineDto(
    Guid Id,
    string Title,
    string Description,
    string Category,
    string? Campaign,
    DateTimeOffset ValidFrom,
    DateTimeOffset? ValidUntil,
    bool IsCurrent,
    Guid CreatedBy,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    public static GuidelineDto From(StrategicGuideline g, DateTimeOffset at) => new(
        g.Id,
        g.Title,
        g.Description,
        g.Category.ToString(),
        g.Campaign,
        g.ValidFrom,
        g.ValidUntil,
        g.IsCurrentAt(at),
        g.CreatedBy,
        g.CreatedAt,
        g.UpdatedAt);
}

public record GuidelineHistoryDto(
    Guid Id,
    Guid GuidelineId,
    string ChangeType,
    Guid ChangedBy,
    string ChangedByName,
    DateTimeOffset ChangedAt,
    string Title,
    string Description,
    string Category,
    string? Campaign,
    DateTimeOffset ValidFrom,
    DateTimeOffset? ValidUntil)
{
    public static GuidelineHistoryDto From(StrategicGuidelineHistoryEntry e) => new(
        e.Id, e.GuidelineId, e.ChangeType.ToString(), e.ChangedBy, e.ChangedByName, e.ChangedAt,
        e.Title, e.Description, e.Category.ToString(), e.Campaign, e.ValidFrom, e.ValidUntil);
}
