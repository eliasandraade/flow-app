using Flow.Application.Common.Exceptions;
using Flow.Application.Common.Interfaces;
using Flow.Application.Common.Persistence;
using Flow.Application.Common.Services;
using Flow.Domain.Entities;
using Flow.Domain.Enums;
using MediatR;

namespace Flow.Application.Ideas.Commands.SubmitIdea;

public class SubmitIdeaCommandHandler : IRequestHandler<SubmitIdeaCommand>
{
    private readonly IIdeaRepository _ideas;
    private readonly IUserRepository _users;
    private readonly IUnitOfWork _unitOfWork;
    private readonly AuditTrail _audit;
    private readonly NotificationPublisher _notifications;
    private readonly IFlowMetrics _metrics;

    public SubmitIdeaCommandHandler(
        IIdeaRepository ideas,
        IUserRepository users,
        IUnitOfWork unitOfWork,
        AuditTrail audit,
        NotificationPublisher notifications,
        IFlowMetrics metrics)
    {
        _ideas = ideas;
        _users = users;
        _unitOfWork = unitOfWork;
        _audit = audit;
        _notifications = notifications;
        _metrics = metrics;
    }

    public async Task Handle(SubmitIdeaCommand request, CancellationToken cancellationToken)
    {
        var idea = await _ideas.GetByIdAsync(request.IdeaId, cancellationToken)
            ?? throw new NotFoundException("Idea", request.IdeaId);

        if (idea.SubmittedBy != _audit.ActorId)
            throw new ForbiddenException("Only the author can submit this idea.");

        var managers = await _users.GetByRoleAsync(UserRole.Manager, cancellationToken);

        var previousStatus = idea.Status.ToString();
        idea.Submit();

        await _unitOfWork.ExecuteAsync(async ct =>
        {
            await _ideas.UpdateAsync(idea, ct);

            await _audit.RecordAsync(
                nameof(Idea), idea.Id, "Submitted",
                oldValue: previousStatus, newValue: idea.Status.ToString(), cancellationToken: ct);

            await _notifications.PublishAsync(
                idea.SubmittedBy,
                NotificationType.IdeaSubmitted,
                "Ideia enviada para análise",
                $"A ideia \"{idea.Title}\" foi enviada e aguarda avaliação.",
                $"flow://ideas/{idea.Id}",
                $"IdeaSubmitted:{idea.Id}:{idea.SubmittedBy}",
                ct);

            await _notifications.PublishManyAsync(
                managers.Select(m => m.Id),
                NotificationType.IdeaAwaitingReview,
                "Nova ideia aguardando análise",
                $"\"{idea.Title}\" foi enviada por {idea.SubmittedByName}.",
                $"flow://manager/ideas/{idea.Id}",
                managerId => $"IdeaAwaitingReview:{idea.Id}:{managerId}",
                ct);
        }, cancellationToken);

        _metrics.IdeaSubmitted();
    }
}
