namespace Flow.Application.Auth;

public record AuthResultDto(
    string AccessToken,
    string RefreshToken,
    Guid UserId,
    string Name,
    string Email,
    string Role
);
