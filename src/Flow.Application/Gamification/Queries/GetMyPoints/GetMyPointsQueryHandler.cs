using Flow.Application.Common.Exceptions;
using Flow.Application.Common.Interfaces;
using Flow.Application.Common.Persistence;
using MediatR;

namespace Flow.Application.Gamification.Queries.GetMyPoints;

public class GetMyPointsQueryHandler : IRequestHandler<GetMyPointsQuery, PointsSummaryDto>
{
    private readonly IUserRepository _users;
    private readonly ICurrentUserService _currentUser;

    public GetMyPointsQueryHandler(IUserRepository users, ICurrentUserService currentUser)
    {
        _users = users;
        _currentUser = currentUser;
    }

    public async Task<PointsSummaryDto> Handle(
        GetMyPointsQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId
            ?? throw new InvalidOperationException("Authenticated user identity could not be resolved.");

        var user = await _users.GetByIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException("User", userId);

        return new PointsSummaryDto(user.Id, user.Name, user.Points);
    }
}
