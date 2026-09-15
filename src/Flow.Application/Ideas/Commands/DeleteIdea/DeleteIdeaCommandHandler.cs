using Flow.Application.Common.Exceptions;
using Flow.Application.Common.Persistence;
using Flow.Application.Common.Services;
using Flow.Domain.Entities;
using Flow.Domain.Exceptions;
using MediatR;

namespace Flow.Application.Ideas.Commands.DeleteIdea;

public class DeleteIdeaCommandHandler : IRequestHandler<DeleteIdeaCommand>
{
    private readonly IIdeaRepository _ideas;
    private readonly IIdeaCommentRepository _comments;
    private readonly IUnitOfWork _unitOfWork;
    private readonly AuditTrail _audit;

    public DeleteIdeaCommandHandler(
        IIdeaRepository ideas,
        IIdeaCommentRepository comments,
        IUnitOfWork unitOfWork,
        AuditTrail audit)
    {
        _ideas = ideas;
        _comments = comments;
        _unitOfWork = unitOfWork;
        _audit = audit;
    }

    public async Task Handle(DeleteIdeaCommand request, CancellationToken cancellationToken)
    {
        var idea = await _ideas.GetByIdAsync(request.IdeaId, cancellationToken)
            ?? throw new NotFoundException("Idea", request.IdeaId);

        if (idea.SubmittedBy != _audit.ActorId)
            throw new ForbiddenException("Only the author can delete this idea.");

        if (!idea.CanBeDeleted())
            throw new DomainException("Only Draft ideas can be deleted.");

        await _unitOfWork.ExecuteAsync(async ct =>
        {
            await _comments.RemoveForIdeaAsync(idea.Id, ct);
            await _ideas.RemoveAsync(idea.Id, ct);

            // The idea document goes away, but the fact that it existed and was deleted
            // stays in the append-only audit trail.
            await _audit.RecordAsync(
                nameof(Idea), idea.Id, "Deleted", oldValue: idea.Title, cancellationToken: ct);
        }, cancellationToken);
    }
}
