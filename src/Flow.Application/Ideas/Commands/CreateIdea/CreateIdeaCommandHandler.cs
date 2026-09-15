using Flow.Application.Common.Exceptions;
using Flow.Application.Common.Interfaces;
using Flow.Application.Common.Persistence;
using Flow.Application.Common.Services;
using Flow.Domain.Entities;
using MediatR;

namespace Flow.Application.Ideas.Commands.CreateIdea;

public class CreateIdeaCommandHandler : IRequestHandler<CreateIdeaCommand, IdeaSummaryDto>
{
    private readonly IIdeaRepository _ideas;
    private readonly IGuidelineRepository _guidelines;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;
    private readonly AuditTrail _audit;

    public CreateIdeaCommandHandler(
        IIdeaRepository ideas,
        IGuidelineRepository guidelines,
        ICurrentUserService currentUser,
        IUnitOfWork unitOfWork,
        AuditTrail audit)
    {
        _ideas = ideas;
        _guidelines = guidelines;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
        _audit = audit;
    }

    public async Task<IdeaSummaryDto> Handle(CreateIdeaCommand request, CancellationToken cancellationToken)
    {
        var actorId = _audit.ActorId;

        // A dangling guideline reference would silently break the strategy-to-result chain
        // the dashboard depends on, so the link is validated at the point of creation.
        if (request.LinkedGuidelineId is { } guidelineId
            && !await _guidelines.ExistsAsync(guidelineId, cancellationToken))
        {
            throw new NotFoundException("Guideline", guidelineId);
        }

        var idea = Idea.Create(
            request.Title,
            request.Description,
            request.Problem,
            actorId,
            _currentUser.UserName ?? string.Empty,
            request.LinkedGuidelineId);

        await _unitOfWork.ExecuteAsync(async ct =>
        {
            await _ideas.AddAsync(idea, ct);
            await _audit.RecordAsync(
                nameof(Idea), idea.Id, "Created",
                newValue: idea.Status.ToString(), cancellationToken: ct);
        }, cancellationToken);

        return IdeaSummaryDto.From(idea);
    }
}
