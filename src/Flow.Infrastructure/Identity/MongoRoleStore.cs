using Flow.Infrastructure.Persistence.Mongo;
using Microsoft.AspNetCore.Identity;
using MongoDB.Driver;

namespace Flow.Infrastructure.Identity;

public sealed class MongoRoleStore : IRoleStore<Role>
{
    private readonly IMongoCollection<Role> _roles;

    public MongoRoleStore(FlowMongoContext context) => _roles = context.Roles;

    public async Task<IdentityResult> CreateAsync(Role role, CancellationToken cancellationToken)
    {
        try
        {
            await _roles.InsertOneAsync(role, cancellationToken: cancellationToken);
            return IdentityResult.Success;
        }
        catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            return IdentityResult.Failed(new IdentityError
            {
                Code = "DuplicateRoleName",
                Description = $"Role '{role.Name}' already exists."
            });
        }
    }

    public async Task<IdentityResult> UpdateAsync(Role role, CancellationToken cancellationToken)
    {
        await _roles.ReplaceOneAsync(
            Builders<Role>.Filter.Eq(r => r.Id, role.Id), role, cancellationToken: cancellationToken);
        return IdentityResult.Success;
    }

    public async Task<IdentityResult> DeleteAsync(Role role, CancellationToken cancellationToken)
    {
        await _roles.DeleteOneAsync(Builders<Role>.Filter.Eq(r => r.Id, role.Id), cancellationToken);
        return IdentityResult.Success;
    }

    public Task<string> GetRoleIdAsync(Role role, CancellationToken cancellationToken) =>
        Task.FromResult(role.Id.ToString());

    public Task<string?> GetRoleNameAsync(Role role, CancellationToken cancellationToken) =>
        Task.FromResult<string?>(role.Name);

    public Task SetRoleNameAsync(Role role, string? roleName, CancellationToken cancellationToken)
    {
        role.Name = roleName ?? string.Empty;
        return Task.CompletedTask;
    }

    public Task<string?> GetNormalizedRoleNameAsync(Role role, CancellationToken cancellationToken) =>
        Task.FromResult<string?>(role.NormalizedName);

    public Task SetNormalizedRoleNameAsync(Role role, string? normalizedName, CancellationToken cancellationToken)
    {
        role.NormalizedName = normalizedName ?? string.Empty;
        return Task.CompletedTask;
    }

    public async Task<Role?> FindByIdAsync(string roleId, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(roleId, out var id)) return null;

        return await _roles
            .Find(Builders<Role>.Filter.Eq(r => r.Id, id))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<Role?> FindByNameAsync(string normalizedRoleName, CancellationToken cancellationToken) =>
        await _roles
            .Find(Builders<Role>.Filter.Eq(r => r.NormalizedName, normalizedRoleName))
            .FirstOrDefaultAsync(cancellationToken);

    public void Dispose() { /* the collection is owned by the singleton context */ }
}
