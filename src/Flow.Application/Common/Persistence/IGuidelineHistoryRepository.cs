using Flow.Domain.Entities;

namespace Flow.Application.Common.Persistence;

/// <summary>Append-only history of strategic guidelines.</summary>
public interface IGuidelineHistoryRepository
{
    Task AppendAsync(StrategicGuidelineHistoryEntry entry, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<StrategicGuidelineHistoryEntry>> GetForGuidelineAsync(
        Guid guidelineId, CancellationToken cancellationToken = default);
}
