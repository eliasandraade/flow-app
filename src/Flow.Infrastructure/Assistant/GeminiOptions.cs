namespace Flow.Infrastructure.Assistant;

/// <summary>
/// Gemini configuration. The API key is supplied by environment variable and stays on the
/// server: it is never shipped to the mobile client and never written to a log.
/// </summary>
public sealed class GeminiOptions
{
    public const string SectionName = "Gemini";

    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Model in use. Recorded on every assistant run for governance.</summary>
    public string Model { get; set; } = "gemini-3.8-flash";

    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Low by default: this is analysis of real figures, not creative writing.</summary>
    public float Temperature { get; set; } = 0.2f;

    public int MaxOutputTokens { get; set; } = 4096;

    public int CircuitBreakerFailureThreshold { get; set; } = 5;
    public TimeSpan CircuitBreakerCooldown { get; set; } = TimeSpan.FromMinutes(1);
}
