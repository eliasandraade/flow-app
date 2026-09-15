using Flow.Application.Common.Exceptions;
using Flow.Application.Common.Persistence;
using MediatR;

namespace Flow.Application.Gamification.Queries.GetUserPoints;

public class GetUserPointsQueryHandler : IRequestHandler<GetUserPointsQuery, PointsSummaryDto>
{
    private readonly IUserRepository _users;

    public GetUserPointsQueryHandler(IUserRepository users) => _users = users;

    public async Task<PointsSummaryDto> Handle(
        GetUserPointsQuery request, CancellationToken cancellationToken)
    {
        var user = await _users.GetByIdAsync(request.UserId, cancellationToken)
            ?? throw new NotFoundException("User", request.UserId);

        return new PointsSummaryDto(user.Id, user.Name, user.Points);
    }
}
