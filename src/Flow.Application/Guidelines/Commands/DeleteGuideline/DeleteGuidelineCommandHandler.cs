using Flow.Application.Common.Exceptions;
using Flow.Application.Common.Persistence;
using Flow.Application.Common.Services;
using Flow.Domain.Entities;
using MediatR;

namespace Flow.Application.Guidelines.Commands.DeleteGuideline;

public class DeleteGuidelineCommandHandler : IRequestHandler<DeleteGuidelineCommand>
{
    private readonly IGuidelineRepository _guidelines;
    private readonly IIdeaRepository _ideas;
    private readonly IProjectRepository _projects;
    private readonly IUnitOfWork _unitOfWork;
    private readonly AuditTrail _audit;

    public DeleteGuidelineCommandHandler(
        IGuidelineRepository guidelines,
        IIdeaRepository ideas,
        IProjectRepository projects,
        IUnitOfWork unitOfWork,
        AuditTrail audit)
    {
        _guidelines = guidelines;
        _ideas = ideas;
        _projects = projects;
        _unitOfWork = unitOfWork;
        _audit = audit;
    }

    public async Task Handle(DeleteGuidelineCommand request, CancellationToken cancellationToken)
    {
        var guideline = await _guidelines.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("Guideline", request.Id);

        // Deleting a guideline that ideas or projects already point at would leave dangling
        // references and erase why those items were prioritised. Closing preserves the link.
        var linkedIdeas = await _ideas.QueryAsync(
            new IdeaFilter { LinkedGuidelineId = request.Id, Take = 1 }, cancellationToken);
        var linkedProjects = await _projects.QueryAsync(
            new ProjectFilter { LinkedGuidelineId = request.Id, Take = 1 }, cancellationToken);

        if (linkedIdeas.Count > 0 || linkedProjects.Count > 0)
            throw new ConflictException(
                "This guideline is referenced by ideas or projects and cannot be deleted. Close it instead to end its validity while preserving traceability.");

        await _unitOfWork.ExecuteAsync(async ct =>
        {
            await _guidelines.RemoveAsync(guideline.Id, ct);
            await _audit.RecordAsync(
                nameof(StrategicGuideline), guideline.Id, "Deleted",
                oldValue: guideline.Title, cancellationToken: ct);
        }, cancellationToken);
    }
}
