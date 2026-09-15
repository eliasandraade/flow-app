using Flow.Application.Common.Exceptions;
using Flow.Application.Common.Interfaces;
using Flow.Application.Common.Persistence;
using Flow.Application.Common.Services;
using Flow.Domain.Entities;
using Flow.Domain.Enums;
using MediatR;

namespace Flow.Application.Ideas.Commands.RejectIdea;

public class RejectIdeaCommandHandler : IRequestHandler<RejectIdeaCommand>
{
    private readonly IIdeaRepository _ideas;
    private readonly IUnitOfWork _unitOfWork;
    private readonly AuditTrail _audit;
    private readonly NotificationPublisher _notifications;
    private readonly IFlowMetrics _metrics;

    public RejectIdeaCommandHandler(
        IIdeaRepository ideas,
        IUnitOfWork unitOfWork,
        AuditTrail audit,
        NotificationPublisher notifications,
        IFlowMetrics metrics)
    {
        _ideas = ideas;
        _unitOfWork = unitOfWork;
        _audit = audit;
        _notifications = notifications;
        _metrics = metrics;
    }

    public async Task Handle(RejectIdeaCommand request, CancellationToken cancellationToken)
    {
        var idea = await _ideas.GetByIdAsync(request.IdeaId, cancellationToken)
            ?? throw new NotFoundException("Idea", request.IdeaId);

        var previousStatus = idea.Status.ToString();
        idea.Reject(request.ManagerComment);

        await _unitOfWork.ExecuteAsync(async ct =>
        {
            await _ideas.UpdateAsync(idea, ct);

            // Rejection carries mandatory context: an unexplained "no" is the fastest way
            // to make people stop submitting ideas.
            await _audit.RecordAsync(
                nameof(Idea), idea.Id, "Rejected",
                oldValue: previousStatus, newValue: idea.Status.ToString(),
                reason: request.ManagerComment, cancellationToken: ct);

            await _notifications.PublishAsync(
                idea.SubmittedBy,
                NotificationType.IdeaRejected,
                "Sua ideia não foi aprovada",
                $"\"{idea.Title}\": {request.ManagerComment}",
                $"flow://ideas/{idea.Id}",
                $"IdeaRejected:{idea.Id}:{idea.SubmittedBy}",
                ct);
        }, cancellationToken);

        _metrics.IdeaRejected();
    }
}
