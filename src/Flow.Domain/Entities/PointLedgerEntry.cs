using Flow.Domain.Exceptions;

namespace Flow.Domain.Entities;

public class PointLedgerEntry
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public int Points { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public string ReferenceType { get; private set; } = string.Empty;
    public Guid ReferenceId { get; private set; }
    public DateTimeOffset AwardedAt { get; private set; }

    private PointLedgerEntry() { }

    public static PointLedgerEntry Create(
        Guid userId,
        int points,
        string reason,
        string referenceType,
        Guid referenceId)
    {
        if (userId == Guid.Empty)
            throw new DomainException("UserId must be a valid user ID.");
        if (points <= 0)
            throw new DomainException("Points awarded must be a positive value.");
        if (string.IsNullOrWhiteSpace(reason))
            throw new DomainException("Reason is required.");
        if (string.IsNullOrWhiteSpace(referenceType))
            throw new DomainException("ReferenceType is required.");
        if (referenceId == Guid.Empty)
            throw new DomainException("ReferenceId must be a valid ID.");

        return new PointLedgerEntry
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Points = points,
            Reason = reason,
            ReferenceType = referenceType,
            ReferenceId = referenceId,
            AwardedAt = DateTimeOffset.UtcNow
        };
    }
}
