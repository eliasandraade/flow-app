using Flow.Domain.Entities;
using Flow.Infrastructure.Persistence.Mongo;
using Microsoft.AspNetCore.Identity;
using MongoDB.Driver;

namespace Flow.Infrastructure.Identity;

/// <summary>
/// MongoDB storage for ASP.NET Core Identity users.
///
/// Only the contracts the product actually exercises are implemented. Two-factor, lockout,
/// claims, phone numbers and external logins are not part of Flow, and stubbing six more
/// interfaces to throw NotImplementedException would be ceremony, not architecture.
///
/// A community package was considered and rejected: the maintained options lag both the
/// 3.x driver and Identity 8, and six well-documented interfaces are a smaller risk than a
/// stalled adapter sitting under authentication.
/// </summary>
public sealed class MongoUserStore :
    IUserStore<User>,
    IUserPasswordStore<User>,
    IUserEmailStore<User>,
    IUserRoleStore<User>,
    IUserSecurityStampStore<User>
{
    private readonly IMongoCollection<User> _users;
    private readonly IMongoCollection<Role> _roles;

    public MongoUserStore(FlowMongoContext context)
    {
        _users = context.Users;
        _roles = context.Roles;
    }

    // ---------- IUserStore ----------

    public Task<string> GetUserIdAsync(User user, CancellationToken cancellationToken) =>
        Task.FromResult(user.Id.ToString());

    public Task<string?> GetUserNameAsync(User user, CancellationToken cancellationToken) =>
        Task.FromResult(user.UserName);

    public Task SetUserNameAsync(User user, string? userName, CancellationToken cancellationToken)
    {
        user.UserName = userName;
        return Task.CompletedTask;
    }

    public Task<string?> GetNormalizedUserNameAsync(User user, CancellationToken cancellationToken) =>
        Task.FromResult(user.NormalizedUserName);

    public Task SetNormalizedUserNameAsync(User user, string? normalizedName, CancellationToken cancellationToken)
    {
        user.NormalizedUserName = normalizedName;
        return Task.CompletedTask;
    }

    public async Task<IdentityResult> CreateAsync(User user, CancellationToken cancellationToken)
    {
        try
        {
            await _users.InsertOneAsync(user, cancellationToken: cancellationToken);
            return IdentityResult.Success;
        }
        catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            // The unique index on normalizedEmail is the real guard against duplicate
            // accounts; a prior existence check would race with a concurrent signup.
            return IdentityResult.Failed(new IdentityError
            {
                Code = "DuplicateUserName",
                Description = $"A user with email '{user.Email}' already exists."
            });
        }
    }

    public async Task<IdentityResult> UpdateAsync(User user, CancellationToken cancellationToken)
    {
        var result = await _users.ReplaceOneAsync(
            Builders<User>.Filter.Eq(u => u.Id, user.Id), user, cancellationToken: cancellationToken);

        return result.MatchedCount == 0
            ? IdentityResult.Failed(new IdentityError { Code = "NotFound", Description = "User not found." })
            : IdentityResult.Success;
    }

    public async Task<IdentityResult> DeleteAsync(User user, CancellationToken cancellationToken)
    {
        await _users.DeleteOneAsync(
            Builders<User>.Filter.Eq(u => u.Id, user.Id), cancellationToken);
        return IdentityResult.Success;
    }

    public async Task<User?> FindByIdAsync(string userId, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(userId, out var id)) return null;

        return await _users
            .Find(Builders<User>.Filter.Eq(u => u.Id, id))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<User?> FindByNameAsync(string normalizedUserName, CancellationToken cancellationToken) =>
        await _users
            .Find(Builders<User>.Filter.Eq(u => u.NormalizedUserName, normalizedUserName))
            .FirstOrDefaultAsync(cancellationToken);

    // ---------- IUserPasswordStore ----------

    public Task SetPasswordHashAsync(User user, string? passwordHash, CancellationToken cancellationToken)
    {
        user.PasswordHash = passwordHash;
        return Task.CompletedTask;
    }

    public Task<string?> GetPasswordHashAsync(User user, CancellationToken cancellationToken) =>
        Task.FromResult(user.PasswordHash);

    public Task<bool> HasPasswordAsync(User user, CancellationToken cancellationToken) =>
        Task.FromResult(!string.IsNullOrEmpty(user.PasswordHash));

    // ---------- IUserEmailStore ----------

    public Task SetEmailAsync(User user, string? email, CancellationToken cancellationToken)
    {
        user.Email = email;
        return Task.CompletedTask;
    }

    public Task<string?> GetEmailAsync(User user, CancellationToken cancellationToken) =>
        Task.FromResult(user.Email);

    public Task<bool> GetEmailConfirmedAsync(User user, CancellationToken cancellationToken) =>
        Task.FromResult(user.EmailConfirmed);

    public Task SetEmailConfirmedAsync(User user, bool confirmed, CancellationToken cancellationToken)
    {
        user.EmailConfirmed = confirmed;
        return Task.CompletedTask;
    }

    public async Task<User?> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken) =>
        await _users
            .Find(Builders<User>.Filter.Eq(u => u.NormalizedEmail, normalizedEmail))
            .FirstOrDefaultAsync(cancellationToken);

    public Task<string?> GetNormalizedEmailAsync(User user, CancellationToken cancellationToken) =>
        Task.FromResult(user.NormalizedEmail);

    public Task SetNormalizedEmailAsync(User user, string? normalizedEmail, CancellationToken cancellationToken)
    {
        user.NormalizedEmail = normalizedEmail;
        return Task.CompletedTask;
    }

    // ---------- IUserRoleStore ----------

    // UserManager normalises the role name before it reaches the store, so everything
    // arriving here is upper-cased. What must be persisted is the role's canonical name:
    // the JWT role claim is built from this list, and ClaimsPrincipal.IsInRole compares
    // claim values with an ordinal, case-sensitive comparison. Storing "LEADERSHIP" would
    // make every [Authorize(Roles = "Leadership")] endpoint return 403.
    private async Task<Role?> ResolveRoleAsync(string roleName, CancellationToken cancellationToken) =>
        await _roles
            .Find(Builders<Role>.Filter.Eq(r => r.NormalizedName, roleName.ToUpperInvariant()))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task AddToRoleAsync(User user, string roleName, CancellationToken cancellationToken)
    {
        var role = await ResolveRoleAsync(roleName, cancellationToken)
            ?? throw new InvalidOperationException($"Role '{roleName}' does not exist.");

        user.AddRole(role.Name);

        await _users.UpdateOneAsync(
            Builders<User>.Filter.Eq(u => u.Id, user.Id),
            Builders<User>.Update.Set(u => u.Roles, user.Roles),
            cancellationToken: cancellationToken);
    }

    public async Task RemoveFromRoleAsync(User user, string roleName, CancellationToken cancellationToken)
    {
        var role = await ResolveRoleAsync(roleName, cancellationToken);

        user.RemoveRole(role?.Name ?? roleName);

        await _users.UpdateOneAsync(
            Builders<User>.Filter.Eq(u => u.Id, user.Id),
            Builders<User>.Update.Set(u => u.Roles, user.Roles),
            cancellationToken: cancellationToken);
    }

    public Task<IList<string>> GetRolesAsync(User user, CancellationToken cancellationToken) =>
        Task.FromResult<IList<string>>(user.Roles.ToList());

    /// <summary>Case-insensitive because the caller passes the normalised name.</summary>
    public Task<bool> IsInRoleAsync(User user, string roleName, CancellationToken cancellationToken) =>
        Task.FromResult(user.HasRole(roleName));

    public async Task<IList<User>> GetUsersInRoleAsync(string roleName, CancellationToken cancellationToken)
    {
        var role = await ResolveRoleAsync(roleName, cancellationToken);
        if (role is null) return [];

        return await _users
            .Find(Builders<User>.Filter.AnyEq(u => u.Roles, role.Name))
            .ToListAsync(cancellationToken);
    }

    // ---------- IUserSecurityStampStore ----------

    public Task SetSecurityStampAsync(User user, string stamp, CancellationToken cancellationToken)
    {
        user.SecurityStamp = stamp;
        return Task.CompletedTask;
    }

    public Task<string?> GetSecurityStampAsync(User user, CancellationToken cancellationToken) =>
        Task.FromResult(user.SecurityStamp);

    public void Dispose() { /* the collection is owned by the singleton context */ }
}
