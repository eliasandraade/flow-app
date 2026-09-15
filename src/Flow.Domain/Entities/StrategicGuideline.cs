using Flow.Domain.Common;
using Flow.Domain.Enums;
using Flow.Domain.Exceptions;

namespace Flow.Domain.Entities;

public class StrategicGuideline : BaseEntity
{
    public string Title { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public GuidelineCategory Category { get; private set; }
    public string? Campaign { get; private set; }
    public DateTimeOffset ValidFrom { get; private set; }
    public DateTimeOffset? ValidUntil { get; private set; }
    public Guid CreatedBy { get; private set; }

    private StrategicGuideline() { }

    /// <summary>
    /// Validity is always derived from the period. There is deliberately no mutable
    /// IsActive flag: a stored boolean would drift out of sync with the dates the moment
    /// the clock passes ValidUntil.
    /// </summary>
    public bool IsCurrentAt(DateTimeOffset at) =>
        ValidFrom <= at && (ValidUntil is null || at <= ValidUntil.Value);

    public static StrategicGuideline Create(
        string title,
        string description,
        GuidelineCategory category,
        string? campaign,
        DateTimeOffset validFrom,
        DateTimeOffset? validUntil,
        Guid createdBy)
    {
        ValidateText(title, description);
        ValidatePeriod(validFrom, validUntil);
        if (createdBy == Guid.Empty)
            throw new DomainException("CreatedBy must be a valid user ID.");

        var now = DateTimeOffset.UtcNow;
        return new StrategicGuideline
        {
            Title = title.Trim(),
            Description = description.Trim(),
            Category = category,
            Campaign = Normalize(campaign),
            ValidFrom = validFrom,
            ValidUntil = validUntil,
            CreatedBy = createdBy,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    public void Update(
        string title,
        string description,
        GuidelineCategory category,
        string? campaign,
        DateTimeOffset validFrom,
        DateTimeOffset? validUntil)
    {
        ValidateText(title, description);
        ValidatePeriod(validFrom, validUntil);

        Title = title.Trim();
        Description = description.Trim();
        Category = category;
        Campaign = Normalize(campaign);
        ValidFrom = validFrom;
        ValidUntil = validUntil;
        SetUpdated();
    }

    /// <summary>
    /// Ends the guideline by closing its validity period instead of deleting it, so that
    /// ideas and projects already linked to it keep a resolvable reference.
    /// </summary>
    public void Close(DateTimeOffset closedAt)
    {
        if (closedAt < ValidFrom)
            throw new DomainException("A guideline cannot be closed before it starts.");
        if (ValidUntil is not null && ValidUntil.Value <= closedAt)
            throw new DomainException("This guideline is already closed.");

        ValidUntil = closedAt;
        SetUpdated();
    }

    private static void ValidateText(string title, string description)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new DomainException("Guideline title is required.");
        if (string.IsNullOrWhiteSpace(description))
            throw new DomainException("Guideline description is required.");
    }

    private static void ValidatePeriod(DateTimeOffset validFrom, DateTimeOffset? validUntil)
    {
        if (validUntil is not null && validUntil.Value <= validFrom)
            throw new DomainException("Guideline ValidUntil must be later than ValidFrom.");
    }

    private static string? Normalize(string? campaign) =>
        string.IsNullOrWhiteSpace(campaign) ? null : campaign.Trim();
}
