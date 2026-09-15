using Flow.Application.Common.Exceptions;
using Flow.Application.Common.Persistence;
using Flow.Application.Common.Services;
using Flow.Domain.Entities;
using Flow.Domain.ValueObjects;
using MediatR;

namespace Flow.Application.Ideas.Commands.SetIdeaFlowScore;

public class SetIdeaFlowScoreCommandHandler : IRequestHandler<SetIdeaFlowScoreCommand, FlowScoreDto>
{
    private readonly IIdeaRepository _ideas;
    private readonly IGuidelineRepository _guidelines;
    private readonly IUnitOfWork _unitOfWork;
    private readonly AuditTrail _audit;

    public SetIdeaFlowScoreCommandHandler(
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

    public async Task<FlowScoreDto> Handle(
        SetIdeaFlowScoreCommand request, CancellationToken cancellationToken)
    {
        var idea = await _ideas.GetByIdAsync(request.IdeaId, cancellationToken)
            ?? throw new NotFoundException("Idea", request.IdeaId);

        var strategicAlignment = request.StrategicAlignment
            ?? await DeriveAlignmentAsync(idea, cancellationToken);

        var components = new FlowScoreComponents(
            strategicAlignment,
            request.Impact,
            request.Feasibility,
            request.Urgency,
            request.Confidence);

        // The total is always computed by the domain from the components, so no caller can
        // hand in an arbitrary number and call it a FlowScore.
        var flowScore = FlowScore.Compute(components);

        var previous = idea.FlowScore?.Total.ToString();
        idea.SetFlowScore(flowScore);

        await _unitOfWork.ExecuteAsync(async ct =>
        {
            await _ideas.UpdateAsync(idea, ct);
            await _audit.RecordAsync(
                nameof(Idea), idea.Id, "FlowScoreSet",
                oldValue: previous, newValue: flowScore.Total.ToString(), cancellationToken: ct);
        }, cancellationToken);

        return FlowScoreDto.From(flowScore)!;
    }

    /// <summary>
    /// When the manager does not state an alignment, derive a starting point from whether
    /// the idea is attached to a guideline that is actually in force right now.
    /// </summary>
    private async Task<int> DeriveAlignmentAsync(Idea idea, CancellationToken cancellationToken)
    {
        if (idea.LinkedGuidelineId is not { } guidelineId)
            return FlowScore.DeriveStrategicAlignment(hasGuideline: false, guidelineIsCurrent: false);

        var guideline = await _guidelines.GetByIdAsync(guidelineId, cancellationToken);
        if (guideline is null)
            return FlowScore.DeriveStrategicAlignment(hasGuideline: false, guidelineIsCurrent: false);

        return FlowScore.DeriveStrategicAlignment(
            hasGuideline: true,
            guidelineIsCurrent: guideline.IsCurrentAt(DateTimeOffset.UtcNow));
    }
}
