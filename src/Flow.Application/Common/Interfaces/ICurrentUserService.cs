using Flow.Domain.Enums;

namespace Flow.Application.Common.Interfaces;

public interface ICurrentUserService
{
    Guid? UserId { get; }
    string? UserName { get; }
    bool IsAuthenticated { get; }

    /// <summary>Role resolved from the token claims, used for resource-level authorization.</summary>
    UserRole? Role { get; }

    bool IsInRole(UserRole role);
}
