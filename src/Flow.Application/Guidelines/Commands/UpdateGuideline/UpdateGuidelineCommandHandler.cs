using Flow.Application.Common.Exceptions;
using Flow.Application.Common.Persistence;
using Flow.Application.Common.Services;
using Flow.Domain.Entities;
using Flow.Domain.Enums;
using MediatR;

namespace Flow.Application.Guidelines.Commands.UpdateGuideline;

public class UpdateGuidelineCommandHandler : IRequestHandler<UpdateGuidelineCommand>
{
    private readonly IGuidelineRepository _guidelines;
    private readonly IGuidelineHistoryRepository _history;
    private readonly IUnitOfWork _unitOfWork;
    private readonly AuditTrail _audit;

    public UpdateGuidelineCommandHandler(
        IGuidelineRepository guidelines,
        IGuidelineHistoryRepository history,
        IUnitOfWork unitOfWork,
        AuditTrail audit)
    {
        _guidelines = guidelines;
        _history = history;
        _unitOfWork = unitOfWork;
        _audit = audit;
    }

    public async Task Handle(UpdateGuidelineCommand request, CancellationToken cancellationToken)
    {
        var guideline = await _guidelines.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("Guideline", request.Id);

        var previousTitle = guideline.Title;

        guideline.Update(
            request.Title, request.Description, request.Category,
            request.Campaign, request.ValidFrom, request.ValidUntil);

        await _unitOfWork.ExecuteAsync(async ct =>
        {
            await _guidelines.UpdateAsync(guideline, ct);

            await _history.AppendAsync(
                StrategicGuidelineHistoryEntry.Capture(
                    guideline, GuidelineChangeType.Updated, _audit.ActorId, _audit.ActorName),
                ct);

            await _audit.RecordAsync(
                nameof(StrategicGuideline), guideline.Id, "Updated",
                oldValue: previousTitle, newValue: guideline.Title, cancellationToken: ct);
        }, cancellationToken);
    }
}
