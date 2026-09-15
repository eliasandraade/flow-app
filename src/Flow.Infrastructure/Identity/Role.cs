namespace Flow.Infrastructure.Identity;

/// <summary>
/// Identity role document. Deliberately minimal: Flow has exactly three fixed roles and
/// no per-role claims, so anything beyond identity and name would be unused structure.
/// </summary>
public sealed class Role
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string NormalizedName { get; set; } = string.Empty;
    public string ConcurrencyStamp { get; set; } = Guid.NewGuid().ToString("N");

    public static Role Create(string name) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        NormalizedName = name.ToUpperInvariant()
    };
}
