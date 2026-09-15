namespace Flow.Infrastructure.Seeding;

/// <summary>
/// Configuration for the demonstration dataset.
///
/// The password has no default on purpose: a hardcoded fallback would eventually reach an
/// environment where it is a real credential.
/// </summary>
public sealed class DemoSeedOptions
{
    public const string SectionName = "DemoSeed";

    public string OperatorEmail { get; set; } = "operator@flow.demo";
    public string ManagerEmail { get; set; } = "manager@flow.demo";
    public string LeadershipEmail { get; set; } = "leadership@flow.demo";

    public string Password { get; set; } = string.Empty;
}
