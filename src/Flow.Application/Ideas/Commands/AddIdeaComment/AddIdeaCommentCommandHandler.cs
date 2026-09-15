using Flow.Application.Common.Exceptions;
using Flow.Application.Common.Persistence;
using Flow.Application.Common.Services;
using Flow.Domain.Entities;
using Flow.Domain.Enums;
using MediatR;

namespace Flow.Application.Ideas.Commands.AddIdeaComment;

public class AddIdeaCommentCommandHandler : IRequestHandler<AddIdeaCommentCommand, IdeaCommentDto>
{
    private readonly IIdeaRepository _ideas;
    private readonly IIdeaCommentRepository _comments;
    private readonly IUnitOfWork _unitOfWork;
    private readonly AuditTrail _audit;
    private readonly NotificationPublisher _notifications;

    public AddIdeaCommentCommandHandler(
        IIdeaRepository ideas,
        IIdeaCommentRepository comments,
        IUnitOfWork unitOfWork,
        AuditTrail audit,
        NotificationPublisher notifications)
    {
        _ideas = ideas;
        _comments = comments;
        _unitOfWork = unitOfWork;
        _audit = audit;
        _notifications = notifications;
    }

    public async Task<IdeaCommentDto> Handle(
        AddIdeaCommentCommand request, CancellationToken cancellationToken)
    {
        var idea = await _ideas.GetByIdAsync(request.IdeaId, cancellationToken)
            ?? throw new NotFoundException("Idea", request.IdeaId);

        var comment = IdeaComment.Create(
            idea.Id, _audit.ActorId, _audit.ActorName, request.Body);

        await _unitOfWork.ExecuteAsync(async ct =>
        {
            await _comments.AddAsync(comment, ct);

            await _audit.RecordAsync(
                nameof(Idea), idea.Id, "Commented", newValue: comment.Id.ToString(), cancellationToken: ct);

            // Commenting on your own idea should not notify you about yourself.
            if (idea.SubmittedBy != _audit.ActorId)
            {
                await _notifications.PublishAsync(
                    idea.SubmittedBy,
                    NotificationType.IdeaCommented,
                    "Novo comentário na sua ideia",
                    $"{_audit.ActorName} comentou em \"{idea.Title}\".",
                    $"flow://ideas/{idea.Id}",
                    $"IdeaCommented:{comment.Id}:{idea.SubmittedBy}",
                    ct);
            }
        }, cancellationToken);

        return IdeaCommentDto.From(comment);
    }
}
