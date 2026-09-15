using Flow.Application.Common.Exceptions;
using Flow.Application.Common.Interfaces;
using Flow.Application.Common.Persistence;
using Flow.Application.Common.Services;
using Flow.Domain.Entities;
using Flow.Domain.Enums;
using MediatR;

namespace Flow.Application.Ideas.Commands.ApproveIdea;

public class ApproveIdeaCommandHandler : IRequestHandler<ApproveIdeaCommand>
{
    private const int IdeaApprovalPoints = 50;

    private readonly IIdeaRepository _ideas;
    private readonly IUserRepository _users;
    private readonly IPointLedgerRepository _pointLedger;
    private readonly IUnitOfWork _unitOfWork;
    private readonly AuditTrail _audit;
    private readonly NotificationPublisher _notifications;
    private readonly IFlowMetrics _metrics;

    public ApproveIdeaCommandHandler(
        IIdeaRepository ideas,
        IUserRepository users,
        IPointLedgerRepository pointLedger,
        IUnitOfWork unitOfWork,
        AuditTrail audit,
        NotificationPublisher notifications,
        IFlowMetrics metrics)
    {
        _ideas = ideas;
        _users = users;
        _pointLedger = pointLedger;
        _unitOfWork = unitOfWork;
        _audit = audit;
        _notifications = notifications;
        _metrics = metrics;
    }

    public async Task Handle(ApproveIdeaCommand request, CancellationToken cancellationToken)
    {
        var idea = await _ideas.GetByIdAsync(request.IdeaId, cancellationToken)
            ?? throw new NotFoundException("Idea", request.IdeaId);

        var submitter = await _users.GetByIdAsync(idea.SubmittedBy, cancellationToken)
            ?? throw new NotFoundException("User", idea.SubmittedBy);

        var previousStatus = idea.Status.ToString();
        idea.Approve(request.ManagerComment);

        var ledgerEntry = PointLedgerEntry.Create(
            userId: submitter.Id,
            points: IdeaApprovalPoints,
            reason: "Idea approved",
            referenceType: nameof(Idea),
            referenceId: idea.Id);

        await _unitOfWork.ExecuteAsync(async ct =>
        {
            await _ideas.UpdateAsync(idea, ct);
            await _users.IncrementPointsAsync(submitter.Id, IdeaApprovalPoints, ct);
            await _pointLedger.AppendAsync(ledgerEntry, ct);

            await _audit.RecordAsync(
                nameof(Idea), idea.Id, "Approved",
                oldValue: previousStatus, newValue: idea.Status.ToString(),
                reason: request.ManagerComment, cancellationToken: ct);

            await _notifications.PublishAsync(
                idea.SubmittedBy,
                NotificationType.IdeaApproved,
                "Sua ideia foi aprovada",
                $"\"{idea.Title}\" foi aprovada. Você ganhou {IdeaApprovalPoints} pontos.",
                $"flow://ideas/{idea.Id}",
                $"IdeaApproved:{idea.Id}:{idea.SubmittedBy}",
                ct);
        }, cancellationToken);

        _metrics.IdeaApproved();
    }
}
