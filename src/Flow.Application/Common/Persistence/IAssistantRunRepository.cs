using Flow.Domain.Entities;

namespace Flow.Application.Common.Persistence;

public interface IAssistantRunRepository
{
    Task AddAsync(AssistantRun run, CancellationToken cancellationToken = default);
    Task<AssistantRun?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task UpdateAsync(AssistantRun run, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AssistantRun>> GetForUserAsync(Guid userId, int take, CancellationToken cancellationToken = default);
}
