namespace Flow.Infrastructure.Notifications;

/// <summary>
/// OneSignal credentials. Supplied by environment variables only — the REST API key is a
/// server-side secret and must never be committed or shipped inside the mobile app.
/// </summary>
public sealed class OneSignalOptions
{
    public const string SectionName = "OneSignal";

    public string AppId { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "https://api.onesignal.com/";
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(10);
}
