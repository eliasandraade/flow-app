using Flow.Application.Common.Exceptions;
using Flow.Application.Common.Persistence;
using Flow.Application.Common.Services;
using Flow.Domain.Entities;
using MediatR;

namespace Flow.Application.Ideas.Commands.SetIdeaScore;

public class SetIdeaScoreCommandHandler : IRequestHandler<SetIdeaScoreCommand>
{
    private readonly IIdeaRepository _ideas;
    private readonly IUnitOfWork _unitOfWork;
    private readonly AuditTrail _audit;

    public SetIdeaScoreCommandHandler(
        IIdeaRepository ideas, IUnitOfWork unitOfWork, AuditTrail audit)
    {
        _ideas = ideas;
        _unitOfWork = unitOfWork;
        _audit = audit;
    }

    public async Task Handle(SetIdeaScoreCommand request, CancellationToken cancellationToken)
    {
        var idea = await _ideas.GetByIdAsync(request.IdeaId, cancellationToken)
            ?? throw new NotFoundException("Idea", request.IdeaId);

        var previous = idea.Score?.ToString();
        idea.SetScore(request.Score);

        await _unitOfWork.ExecuteAsync(async ct =>
        {
            await _ideas.UpdateAsync(idea, ct);
            await _audit.RecordAsync(
                nameof(Idea), idea.Id, "ScoreSet",
                oldValue: previous, newValue: idea.Score?.ToString(), cancellationToken: ct);
        }, cancellationToken);
    }
}
