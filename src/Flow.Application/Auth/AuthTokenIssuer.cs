using Flow.Application.Common.Interfaces;
using Flow.Application.Common.Persistence;
using Flow.Domain.Entities;
using Microsoft.Extensions.Options;

namespace Flow.Application.Auth;

/// <summary>
/// Single place where an authenticated session is minted. Login, registration and refresh
/// all go through here so the refresh-token lifetime and storage rules cannot drift apart.
/// </summary>
public sealed class AuthTokenIssuer
{
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IRefreshTokenRepository _refreshTokens;
    private readonly AuthSettings _settings;

    public AuthTokenIssuer(
        IJwtTokenService jwtTokenService,
        IRefreshTokenRepository refreshTokens,
        IOptions<AuthSettings> settings)
    {
        _jwtTokenService = jwtTokenService;
        _refreshTokens = refreshTokens;
        _settings = settings.Value;
    }

    public async Task<(AuthResultDto Result, RefreshToken Issued)> IssueAsync(
        User user, IList<string> roles, CancellationToken cancellationToken)
    {
        var accessToken = _jwtTokenService.GenerateAccessToken(user, roles);

        // Only the hash reaches storage; the raw value exists just long enough to be
        // returned to the caller.
        var rawRefreshToken = _jwtTokenService.GenerateRefreshToken();
        var refreshToken = RefreshToken.Issue(
            user.Id,
            rawRefreshToken,
            DateTimeOffset.UtcNow.AddDays(_settings.RefreshTokenDays));

        await _refreshTokens.AddAsync(refreshToken, cancellationToken);

        var result = new AuthResultDto(
            AccessToken: accessToken,
            RefreshToken: rawRefreshToken,
            UserId: user.Id,
            Name: user.Name,
            Email: user.Email!,
            Role: user.Role.ToString());

        return (result, refreshToken);
    }
}
