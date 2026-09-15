using Flow.Application.Common.Exceptions;
using Flow.Application.Common.Persistence;
using Flow.Application.Common.Services;
using Flow.Domain.Entities;
using MediatR;

namespace Flow.Application.Ideas.Commands.UpdateIdea;

public class UpdateIdeaCommandHandler : IRequestHandler<UpdateIdeaCommand>
{
    private readonly IIdeaRepository _ideas;
    private readonly IGuidelineRepository _guidelines;
    private readonly IUnitOfWork _unitOfWork;
    private readonly AuditTrail _audit;

    public UpdateIdeaCommandHandler(
        IIdeaRepository ideas,
        IGuidelineRepository guidelines,
        IUnitOfWork unitOfWork,
        AuditTrail audit)
    {
        _ideas = ideas;
        _guidelines = guidelines;
        _unitOfWork = unitOfWork;
        _audit = audit;
    }

    public async Task Handle(UpdateIdeaCommand request, CancellationToken cancellationToken)
    {
        var idea = await _ideas.GetByIdAsync(request.IdeaId, cancellationToken)
            ?? throw new NotFoundException("Idea", request.IdeaId);

        if (idea.SubmittedBy != _audit.ActorId)
            throw new ForbiddenException("Only the author can edit this idea.");

        if (request.LinkedGuidelineId is { } guidelineId
            && !await _guidelines.ExistsAsync(guidelineId, cancellationToken))
        {
            throw new NotFoundException("Guideline", guidelineId);
        }

        idea.Update(request.Title, request.Description, request.Problem, request.LinkedGuidelineId);

        await _unitOfWork.ExecuteAsync(async ct =>
        {
            await _ideas.UpdateAsync(idea, ct);
            await _audit.RecordAsync(
                nameof(Idea), idea.Id, "Updated", newValue: idea.Title, cancellationToken: ct);
        }, cancellationToken);
    }
}
