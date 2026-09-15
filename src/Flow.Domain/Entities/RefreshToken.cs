using System.Security.Cryptography;
using System.Text;
using Flow.Domain.Exceptions;

namespace Flow.Domain.Entities;

/// <summary>
/// A refresh token as stored by Flow. The raw token value is never persisted: only its
/// SHA-256 hash is, so a database dump cannot be replayed against the API.
/// </summary>
public class RefreshToken
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string TokenHash { get; private set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }

    /// <summary>Set when this token is rotated, forming an auditable rotation chain.</summary>
    public string? ReplacedByTokenHash { get; private set; }

    public bool IsRevoked => RevokedAt is not null;

    public bool IsActiveAt(DateTimeOffset at) => !IsRevoked && ExpiresAt > at;

    public bool IsActive => IsActiveAt(DateTimeOffset.UtcNow);

    private RefreshToken() { }

    public static RefreshToken Issue(Guid userId, string rawToken, DateTimeOffset expiresAt)
    {
        if (userId == Guid.Empty)
            throw new DomainException("A refresh token must belong to a valid user.");
        if (string.IsNullOrWhiteSpace(rawToken))
            throw new DomainException("A refresh token value is required.");

        return new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = Hash(rawToken),
            ExpiresAt = expiresAt,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    public void Revoke() => RevokedAt ??= DateTimeOffset.UtcNow;

    /// <summary>Revokes this token and records which token replaced it.</summary>
    public void RotateTo(RefreshToken replacement)
    {
        ArgumentNullException.ThrowIfNull(replacement);
        Revoke();
        ReplacedByTokenHash = replacement.TokenHash;
    }

    public static string Hash(string rawToken)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
