using Flow.Application.Common.Interfaces;
using Flow.Application.Common.Persistence;
using Flow.Domain.Entities;

namespace Flow.Application.Common.Services;

/// <summary>
/// Writes governance records with the actor and correlation id already resolved.
///
/// Every handler used to repeat the same six lines to build an AuditLog, which is exactly
/// the kind of duplication that lets one transition quietly ship without an audit entry.
/// </summary>
public sealed class AuditTrail
{
    private readonly IAuditLogRepository _auditLogs;
    private readonly ICurrentUserService _currentUser;
    private readonly ICorrelationIdAccessor _correlation;

    public AuditTrail(
        IAuditLogRepository auditLogs,
        ICurrentUserService currentUser,
        ICorrelationIdAccessor correlation)
    {
        _auditLogs = auditLogs;
        _currentUser = currentUser;
        _correlation = correlation;
    }

    public Guid ActorId => _currentUser.UserId
        ?? throw new InvalidOperationException("Authenticated user identity could not be resolved.");

    public string ActorName => _currentUser.UserName ?? string.Empty;

    public Task RecordAsync(
        string entityType,
        Guid entityId,
        string action,
        string? oldValue = null,
        string? newValue = null,
        string? reason = null,
        CancellationToken cancellationToken = default)
    {
        var entry = AuditLog.Create(
            entityType: entityType,
            entityId: entityId,
            action: action,
            actorId: ActorId,
            actorName: ActorName,
            oldValue: oldValue,
            newValue: newValue,
            reason: reason,
            correlationId: _correlation.CorrelationId);

        return _auditLogs.AppendAsync(entry, cancellationToken);
    }
}
