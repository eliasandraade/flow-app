namespace Flow.API;

public static class RateLimitPolicies
{
    /// <summary>Login, registration and refresh: cheap to abuse, expensive to leave open.</summary>
    public const string Auth = "auth";

    /// <summary>Model-backed endpoints, which cost real money per call.</summary>
    public const string Ai = "ai";
}

/// <summary>
/// Rate limits, configurable so an integration suite can raise them without the
/// production defaults being loosened to accommodate tests.
/// </summary>
public sealed class RateLimitOptions
{
    public const string SectionName = "RateLimiting";

    public bool Enabled { get; set; } = true;

    public int AuthPermitPerMinute { get; set; } = 10;
    public int AiPermitPerFiveMinutes { get; set; } = 20;
    public int GlobalPermitPerMinute { get; set; } = 300;
}
