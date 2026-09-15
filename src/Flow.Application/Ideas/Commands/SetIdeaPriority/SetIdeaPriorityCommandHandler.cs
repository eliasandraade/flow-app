using Flow.Application.Common.Exceptions;
using Flow.Application.Common.Persistence;
using Flow.Application.Common.Services;
using Flow.Domain.Entities;
using MediatR;

namespace Flow.Application.Ideas.Commands.SetIdeaPriority;

public class SetIdeaPriorityCommandHandler : IRequestHandler<SetIdeaPriorityCommand>
{
    private readonly IIdeaRepository _ideas;
    private readonly IUnitOfWork _unitOfWork;
    private readonly AuditTrail _audit;

    public SetIdeaPriorityCommandHandler(
        IIdeaRepository ideas, IUnitOfWork unitOfWork, AuditTrail audit)
    {
        _ideas = ideas;
        _unitOfWork = unitOfWork;
        _audit = audit;
    }

    public async Task Handle(SetIdeaPriorityCommand request, CancellationToken cancellationToken)
    {
        var idea = await _ideas.GetByIdAsync(request.IdeaId, cancellationToken)
            ?? throw new NotFoundException("Idea", request.IdeaId);

        var previous = idea.Priority.ToString();
        idea.SetPriority(request.Priority);

        await _unitOfWork.ExecuteAsync(async ct =>
        {
            await _ideas.UpdateAsync(idea, ct);
            await _audit.RecordAsync(
                nameof(Idea), idea.Id, "PrioritySet",
                oldValue: previous, newValue: idea.Priority.ToString(), cancellationToken: ct);
        }, cancellationToken);
    }
}
