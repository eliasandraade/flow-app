using Flow.Domain.Entities;

namespace Flow.Application.Common.Interfaces;

public interface IJwtTokenService
{
    string GenerateAccessToken(User user, IList<string> roles);
    string GenerateRefreshToken();
    Guid? GetUserIdFromToken(string accessToken);
}
