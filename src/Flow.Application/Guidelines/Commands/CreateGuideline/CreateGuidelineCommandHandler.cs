using Flow.Application.Common.Persistence;
using Flow.Application.Common.Services;
using Flow.Domain.Entities;
using Flow.Domain.Enums;
using MediatR;

namespace Flow.Application.Guidelines.Commands.CreateGuideline;

public class CreateGuidelineCommandHandler : IRequestHandler<CreateGuidelineCommand, GuidelineDto>
{
    private readonly IGuidelineRepository _guidelines;
    private readonly IGuidelineHistoryRepository _history;
    private readonly IUnitOfWork _unitOfWork;
    private readonly AuditTrail _audit;

    public CreateGuidelineCommandHandler(
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

    public async Task<GuidelineDto> Handle(CreateGuidelineCommand request, CancellationToken cancellationToken)
    {
        var actorId = _audit.ActorId;

        var guideline = StrategicGuideline.Create(
            request.Title,
            request.Description,
            request.Category,
            request.Campaign,
            request.ValidFrom,
            request.ValidUntil,
            actorId);

        await _unitOfWork.ExecuteAsync(async ct =>
        {
            await _guidelines.AddAsync(guideline, ct);

            await _history.AppendAsync(
                StrategicGuidelineHistoryEntry.Capture(
                    guideline, GuidelineChangeType.Created, actorId, _audit.ActorName),
                ct);

            await _audit.RecordAsync(
                nameof(StrategicGuideline), guideline.Id, "Created",
                newValue: guideline.Title, cancellationToken: ct);
        }, cancellationToken);

        return GuidelineDto.From(guideline, DateTimeOffset.UtcNow);
    }
}
