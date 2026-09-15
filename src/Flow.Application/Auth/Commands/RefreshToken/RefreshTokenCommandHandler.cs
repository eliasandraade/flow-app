using Flow.Application.Common.Exceptions;
using Flow.Application.Common.Interfaces;
using Flow.Application.Common.Persistence;
using Flow.Domain.Entities;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using DomainRefreshToken = Flow.Domain.Entities.RefreshToken;

namespace Flow.Application.Auth.Commands.RefreshToken;

public class RefreshTokenCommandHandler : IRequestHandler<RefreshTokenCommand, AuthResultDto>
{
    private readonly UserManager<User> _userManager;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IRefreshTokenRepository _refreshTokens;
    private readonly IUnitOfWork _unitOfWork;
    private readonly AuthTokenIssuer _tokenIssuer;
    private readonly ILogger<RefreshTokenCommandHandler> _logger;

    public RefreshTokenCommandHandler(
        UserManager<User> userManager,
        IJwtTokenService jwtTokenService,
        IRefreshTokenRepository refreshTokens,
        IUnitOfWork unitOfWork,
        AuthTokenIssuer tokenIssuer,
        ILogger<RefreshTokenCommandHandler> logger)
    {
        _userManager = userManager;
        _jwtTokenService = jwtTokenService;
        _refreshTokens = refreshTokens;
        _unitOfWork = unitOfWork;
        _tokenIssuer = tokenIssuer;
        _logger = logger;
    }

    public async Task<AuthResultDto> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        var userId = _jwtTokenService.GetUserIdFromToken(request.AccessToken)
            ?? throw new UnauthorizedException("Invalid access token.");

        var tokenHash = DomainRefreshToken.Hash(request.RefreshToken);

        var storedToken = await _refreshTokens.GetByHashAsync(tokenHash, cancellationToken)
            ?? throw new UnauthorizedException("Refresh token not found.");

        if (storedToken.UserId != userId)
            throw new UnauthorizedException("Refresh token does not belong to this user.");

        if (!storedToken.IsActive)
            await RejectAsReuseAsync(userId, cancellationToken);

        var user = await _userManager.FindByIdAsync(userId.ToString())
            ?? throw new NotFoundException(nameof(User), userId);

        var roles = await _userManager.GetRolesAsync(user);

        try
        {
            return await _unitOfWork.ExecuteAsync(async ct =>
            {
                var (result, issued) = await _tokenIssuer.IssueAsync(user, roles, ct);

                // The check above was a courtesy, not the guarantee: between reading the
                // token and getting here another request may have consumed it. Consumption
                // is therefore conditional on the token still being unconsumed, decided by
                // the database in one operation. Losing means the replacement just issued
                // is rolled back with the transaction, so no orphan chain survives.
                var consumed = await _refreshTokens.TryConsumeAsync(
                    tokenHash, userId, issued.TokenHash, DateTimeOffset.UtcNow, ct);

                if (!consumed) throw new RefreshTokenAlreadyConsumedException();

                return result;
            }, cancellationToken);
        }
        catch (RefreshTokenAlreadyConsumedException)
        {
            // Runs outside the transaction, which has already been rolled back.
            await RejectAsReuseAsync(userId, cancellationToken);
            throw;
        }
    }

    /// <summary>
    /// A token presented after it was consumed is the classic theft signature, and a
    /// request that loses the rotation race is indistinguishable from it. Both invalidate
    /// the whole chain, which is the conservative reading of the OAuth 2.0 security
    /// guidance and the behaviour the API already documents.
    /// </summary>
    private async Task RejectAsReuseAsync(Guid userId, CancellationToken cancellationToken)
    {
        _logger.LogWarning(
            "Refresh token reuse detected for user {UserId}. Revoking all active tokens.", userId);

        await _refreshTokens.RevokeAllForUserAsync(userId, cancellationToken);
        throw new UnauthorizedException("Refresh token is expired or revoked.");
    }

    /// <summary>
    /// Signals, from inside the transaction, that this request lost the rotation race.
    /// Throwing is what rolls the transaction back, so the replacement token that was just
    /// issued never reaches the database.
    /// </summary>
    private sealed class RefreshTokenAlreadyConsumedException : Exception;
}
