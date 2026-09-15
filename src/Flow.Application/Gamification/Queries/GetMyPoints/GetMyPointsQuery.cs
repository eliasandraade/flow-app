using MediatR;

namespace Flow.Application.Gamification.Queries.GetMyPoints;

public record GetMyPointsQuery : IRequest<PointsSummaryDto>;
