using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Flow.API.Services;

/// <summary>
/// Reads the Flow user id out of a principal.
///
/// It has to try two claim types, and that is not defensive coding. The token is minted
/// with a "sub" claim, but the JWT bearer handler maps inbound claims by default, so by the
/// time the principal reaches the pipeline "sub" has become
/// <see cref="ClaimTypes.NameIdentifier"/>. Code that looks only for "sub" therefore finds
/// nothing — silently, on an authenticated request.
///
/// One place, so that the rate limiter and the current-user service cannot disagree about
/// who is calling.
/// </summary>
public static class UserIdentity
{
    public static string? IdOf(ClaimsPrincipal? principal)
    {
        var claim = principal?.FindFirst(JwtRegisteredClaimNames.Sub)
            ?? principal?.FindFirst(ClaimTypes.NameIdentifier);

        return claim?.Value;
    }

    public static Guid? GuidOf(ClaimsPrincipal? principal) =>
        Guid.TryParse(IdOf(principal), out var id) ? id : null;
}
