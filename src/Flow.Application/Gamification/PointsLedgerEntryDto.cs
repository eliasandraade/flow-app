namespace Flow.Application.Gamification;

public record PointsLedgerEntryDto(
    Guid Id,
    int Points,
    string Reason,
    string ReferenceType,
    Guid ReferenceId,
    DateTimeOffset AwardedAt);
