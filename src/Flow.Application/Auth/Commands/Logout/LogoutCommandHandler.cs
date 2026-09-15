using Flow.Application.Common.Interfaces;
using Flow.Application.Common.Persistence;
using MediatR;
using DomainRefreshToken = Flow.Domain.Entities.RefreshToken;

namespace Flow.Application.Auth.Commands.Logout;

public class LogoutCommandHandler : IRequestHandler<LogoutCommand>
{
    private readonly IRefreshTokenRepository _refreshTokens;
    private readonly ICurrentUserService _currentUser;

    public LogoutCommandHandler(IRefreshTokenRepository refreshTokens, ICurrentUserService currentUser)
    {
        _refreshTokens = refreshTokens;
        _currentUser = currentUser;
    }

    public async Task Handle(LogoutCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId;
        if (userId is null) return;

        // Ownership and liveness are conditions on the write, not checks made beforehand:
        // logging out with someone else's token must not revoke it, and a token that is
        // already inactive needs no work. A no-op is a successful logout either way, so
        // the result is deliberately not inspected.
        await _refreshTokens.TryRevokeAsync(
            DomainRefreshToken.Hash(request.RefreshToken),
            userId.Value,
            DateTimeOffset.UtcNow,
            cancellationToken);
    }
}
