using Flow.Domain.Entities;

namespace Flow.Application.Common.Persistence;

public interface IIdeaCommentRepository
{
    Task AddAsync(IdeaComment comment, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<IdeaComment>> GetForIdeaAsync(Guid ideaId, CancellationToken cancellationToken = default);
    Task RemoveForIdeaAsync(Guid ideaId, CancellationToken cancellationToken = default);
}
