namespace Flow.Application.Gamification;

public record PointsSummaryDto(Guid UserId, string UserName, int Points);
