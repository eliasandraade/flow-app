using Flow.Domain.Entities;

namespace Flow.Application.Common.Persistence;

public interface IResultRepository
{
    Task<Result?> GetByProjectIdAsync(Guid projectId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Result>> GetAllAsync(CancellationToken cancellationToken = default);
    Task UpsertAsync(Result result, CancellationToken cancellationToken = default);
}
