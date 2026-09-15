namespace Flow.Application.Auth;

/// <summary>Auth lifetimes, bound from configuration at the composition root.</summary>
public sealed class AuthSettings
{
    public int RefreshTokenDays { get; set; } = 7;
}
