using Flow.Domain.Entities;

namespace Flow.Application.Common.Persistence;

public interface IGuidelineRepository
{
    Task<StrategicGuideline?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<StrategicGuideline>> QueryAsync(GuidelineFilter filter, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<StrategicGuideline>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddAsync(StrategicGuideline guideline, CancellationToken cancellationToken = default);
    Task UpdateAsync(StrategicGuideline guideline, CancellationToken cancellationToken = default);
    Task RemoveAsync(Guid id, CancellationToken cancellationToken = default);
}
