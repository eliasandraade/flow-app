using Flow.Application.Common.Exceptions;
using Flow.Application.Common.Persistence;
using Flow.Application.Common.Services;
using Flow.Domain.Entities;
using Flow.Domain.Enums;
using MediatR;

namespace Flow.Application.Guidelines.Commands.CloseGuideline;

public class CloseGuidelineCommandHandler : IRequestHandler<CloseGuidelineCommand>
{
    private readonly IGuidelineRepository _guidelines;
    private readonly IGuidelineHistoryRepository _history;
    private readonly IUnitOfWork _unitOfWork;
    private readonly AuditTrail _audit;

    public CloseGuidelineCommandHandler(
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

    public async Task Handle(CloseGuidelineCommand request, CancellationToken cancellationToken)
    {
        var guideline = await _guidelines.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("Guideline", request.Id);

        var closedAt = DateTimeOffset.UtcNow;
        guideline.Close(closedAt);

        await _unitOfWork.ExecuteAsync(async ct =>
        {
            await _guidelines.UpdateAsync(guideline, ct);

            await _history.AppendAsync(
                StrategicGuidelineHistoryEntry.Capture(
                    guideline, GuidelineChangeType.Closed, _audit.ActorId, _audit.ActorName),
                ct);

            await _audit.RecordAsync(
                nameof(StrategicGuideline), guideline.Id, "Closed",
                newValue: closedAt.ToString("O"), cancellationToken: ct);
        }, cancellationToken);
    }
}
