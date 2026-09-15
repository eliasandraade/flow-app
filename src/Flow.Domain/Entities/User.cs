using Flow.Domain.Common;
using Flow.Domain.Enums;
using Microsoft.AspNetCore.Identity;

namespace Flow.Domain.Entities;

/// <summary>
/// Inherits <see cref="IdentityUser{TKey}"/> so that UserManager, the password hasher and
/// the security-stamp machinery keep working. That is a deliberate, contained concession:
/// the framework type is only used for identity fields, and the storage behind it is the
/// application's own Mongo store rather than Entity Framework.
/// </summary>
public class User : IdentityUser<Guid>, IAuditableEntity
{
    public string Name { get; private set; } = string.Empty;
    public UserRole Role { get; private set; }
    public int Points { get; private set; }

    /// <summary>
    /// Identity roles denormalised onto the user document. The only question ever asked of
    /// this data is "what roles does this user have", and it is asked on every login, so a
    /// join collection would add a round trip to the hottest path for no benefit.
    /// </summary>
    public List<string> Roles { get; private set; } = [];

    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private User() { }

    public static User Create(string name, string email, UserRole role)
    {
        var now = DateTimeOffset.UtcNow;
        return new User
        {
            Id = Guid.NewGuid(),
            Name = name,
            Email = email,
            UserName = email,
            NormalizedEmail = email.ToUpperInvariant(),
            NormalizedUserName = email.ToUpperInvariant(),
            Role = role,
            Points = 0,
            Roles = [],
            SecurityStamp = Guid.NewGuid().ToString("N"),
            ConcurrencyStamp = Guid.NewGuid().ToString("N"),
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    public void AddPoints(int points)
    {
        if (points <= 0) throw new ArgumentException("Points must be positive.", nameof(points));
        Points += points;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void AddRole(string roleName)
    {
        if (string.IsNullOrWhiteSpace(roleName)) return;
        if (Roles.Contains(roleName, StringComparer.OrdinalIgnoreCase)) return;

        Roles.Add(roleName);
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void RemoveRole(string roleName)
    {
        var existing = Roles.FirstOrDefault(r => string.Equals(r, roleName, StringComparison.OrdinalIgnoreCase));
        if (existing is null) return;

        Roles.Remove(existing);
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public bool HasRole(string roleName) =>
        Roles.Contains(roleName, StringComparer.OrdinalIgnoreCase);

    /// <summary>Elevates the business role. Used by controlled administrative paths and demo seeding.</summary>
    public void SetRole(UserRole role)
    {
        Role = role;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
