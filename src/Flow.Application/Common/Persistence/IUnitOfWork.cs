namespace Flow.Application.Common.Persistence;

/// <summary>
/// Transactional boundary for a use case.
///
/// Everything written inside the delegate commits together or not at all: the aggregate
/// change, the audit entry, the project snapshot, the points ledger and the notification
/// outbox. Under EF Core that atomicity came free from a single SaveChanges; with explicit
/// document writes it has to be stated, and this is where it is stated.
///
/// The driver session handle deliberately never crosses into this layer, so application
/// code stays testable without a database.
/// </summary>
public interface IUnitOfWork
{
    Task ExecuteAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken = default);

    Task<TResult> ExecuteAsync<TResult>(
        Func<CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken = default);
}
