using Flow.Domain.Enums;
using Flow.Domain.Exceptions;

namespace Flow.Domain.Entities;

/// <summary>Append-only history of a strategic guideline. Never updated, never deleted.</summary>
public class StrategicGuidelineHistoryEntry
{
    public Guid Id { get; private set; }
    public Guid GuidelineId { get; private set; }
    public Guid ChangedBy { get; private set; }
    public string ChangedByName { get; private set; } = string.Empty;
    public DateTimeOffset ChangedAt { get; private set; }
    public GuidelineChangeType ChangeType { get; private set; }

    public string Title { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public GuidelineCategory Category { get; private set; }
    public string? Campaign { get; private set; }
    public DateTimeOffset ValidFrom { get; private set; }
    public DateTimeOffset? ValidUntil { get; private set; }

    private StrategicGuidelineHistoryEntry() { }

    public static StrategicGuidelineHistoryEntry Capture(
        StrategicGuideline guideline,
        GuidelineChangeType changeType,
        Guid changedBy,
        string changedByName)
    {
        ArgumentNullException.ThrowIfNull(guideline);
        if (changedBy == Guid.Empty)
            throw new DomainException("Guideline history requires a valid actor.");

        return new StrategicGuidelineHistoryEntry
        {
            Id = Guid.NewGuid(),
            GuidelineId = guideline.Id,
            ChangedBy = changedBy,
            ChangedByName = changedByName,
            ChangedAt = DateTimeOffset.UtcNow,
            ChangeType = changeType,
            Title = guideline.Title,
            Description = guideline.Description,
            Category = guideline.Category,
            Campaign = guideline.Campaign,
            ValidFrom = guideline.ValidFrom,
            ValidUntil = guideline.ValidUntil
        };
    }
}
