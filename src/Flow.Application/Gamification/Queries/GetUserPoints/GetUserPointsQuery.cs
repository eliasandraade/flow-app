using MediatR;

namespace Flow.Application.Gamification.Queries.GetUserPoints;

public record GetUserPointsQuery(Guid UserId) : IRequest<PointsSummaryDto>;
