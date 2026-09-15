using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Flow.Application.Common.Interfaces;
using Flow.Domain.Enums;

namespace Flow.API.Services;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private ClaimsPrincipal? Principal => _httpContextAccessor.HttpContext?.User;

    public Guid? UserId => UserIdentity.GuidOf(Principal);

    public string? UserName =>
        Principal?.FindFirst(JwtRegisteredClaimNames.Name)?.Value
        ?? Principal?.Identity?.Name;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated ?? false;

    /// <summary>
    /// Resolved from the role claims so that handlers can apply resource-level rules
    /// without a database round trip on every request.
    /// </summary>
    public UserRole? Role
    {
        get
        {
            foreach (var role in Enum.GetValues<UserRole>())
            {
                if (Principal?.IsInRole(role.ToString()) == true) return role;
            }

            return null;
        }
    }

    public bool IsInRole(UserRole role) => Principal?.IsInRole(role.ToString()) ?? false;
}
