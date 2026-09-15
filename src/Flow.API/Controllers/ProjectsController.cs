using Flow.Application.Projects;
using Flow.Application.Projects.Commands.AdvanceProjectStage;
using Flow.Application.Projects.Commands.BlockProject;
using Flow.Application.Projects.Commands.CancelProject;
using Flow.Application.Projects.Commands.CompleteProject;
using Flow.Application.Projects.Commands.ConvertIdeaToProject;
using Flow.Application.Projects.Commands.CreateProject;
using Flow.Application.Projects.Commands.StartProject;
using Flow.Application.Projects.Commands.UnblockProject;
using Flow.Application.Projects.Commands.UpdateProject;
using Flow.Application.Projects.Commands.UpdateProjectProgress;
using Flow.Application.Projects.Queries.GetProjectById;
using Flow.Application.Projects.Queries.GetProjects;
using Flow.Application.Projects.Queries.GetProjectSnapshots;
using Flow.Application.Projects.Queries.GetProjectTimeline;
using Flow.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Flow.API.Controllers;

[ApiController]
[Route("api/v1")]
[Authorize]
[Produces("application/json")]
public class ProjectsController : ControllerBase
{
    private readonly IMediator _mediator;

    public ProjectsController(IMediator mediator) => _mediator = mediator;

    [HttpPost("projects")]
    [Authorize(Roles = "Manager")]
    [ProducesResponseType(typeof(ProjectSummaryDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ProjectSummaryDto>> Create(
        [FromBody] CreateProjectCommand command, CancellationToken ct)
    {
        var result = await _mediator.Send(command, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    /// <summary>
    /// Converts an approved idea into a project, carrying the strategic guideline across
    /// so the strategy-to-result chain stays intact.
    /// </summary>
    [HttpPost("ideas/{ideaId:guid}/convert")]
    [Authorize(Roles = "Manager")]
    [ProducesResponseType(typeof(ProjectSummaryDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ProjectSummaryDto>> ConvertFromIdea(
        Guid ideaId, [FromBody] ConvertIdeaRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new ConvertIdeaToProjectCommand(
            ideaId, request.Title, request.Description, request.Priority,
            request.OwnerId, request.EstimatedCost, request.Deadline, request.AssistantRunId), ct);

        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpGet("projects")]
    [ProducesResponseType(typeof(IReadOnlyList<ProjectSummaryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ProjectSummaryDto>>> GetAll(
        [FromQuery] Guid? ownerId,
        [FromQuery] ProjectStatus? status,
        [FromQuery] ProjectStage? stage,
        [FromQuery] Guid? linkedGuidelineId,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50,
        CancellationToken ct = default) =>
        Ok(await _mediator.Send(
            new GetProjectsQuery(ownerId, status, stage, linkedGuidelineId, skip, take), ct));

    [HttpGet("projects/{id:guid}")]
    [ProducesResponseType(typeof(ProjectDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProjectDetailDto>> GetById(Guid id, CancellationToken ct) =>
        Ok(await _mediator.Send(new GetProjectByIdQuery(id), ct));

    [HttpPut("projects/{id:guid}")]
    [Authorize(Roles = "Manager")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Update(
        Guid id, [FromBody] UpdateProjectRequest request, CancellationToken ct)
    {
        await _mediator.Send(new UpdateProjectCommand(
            id, request.Title, request.Description, request.Priority, request.OwnerId,
            request.EstimatedCost, request.ActualCost, request.Deadline), ct);
        return NoContent();
    }

    /// <summary>
    /// Updates delivery progress. A dedicated operation, not a side effect of editing:
    /// progress changes are auditable and produce their own snapshot.
    /// </summary>
    [HttpPatch("projects/{id:guid}/progress")]
    [Authorize(Roles = "Manager")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateProgress(
        Guid id, [FromBody] UpdateProgressRequest request, CancellationToken ct)
    {
        await _mediator.Send(new UpdateProjectProgressCommand(id, request.ProgressPercentage), ct);
        return NoContent();
    }

    /// <summary>Moves the execution stage. Orthogonal to the governance status.</summary>
    [HttpPatch("projects/{id:guid}/stage")]
    [Authorize(Roles = "Manager")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> AdvanceStage(
        Guid id, [FromBody] AdvanceStageRequest request, CancellationToken ct)
    {
        await _mediator.Send(new AdvanceProjectStageCommand(id, request.Stage), ct);
        return NoContent();
    }

    [HttpPost("projects/{id:guid}/start")]
    [Authorize(Roles = "Manager")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Start(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new StartProjectCommand(id), ct);
        return NoContent();
    }

    [HttpPost("projects/{id:guid}/complete")]
    [Authorize(Roles = "Manager")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Complete(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new CompleteProjectCommand(id), ct);
        return NoContent();
    }

    [HttpPost("projects/{id:guid}/cancel")]
    [Authorize(Roles = "Manager")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Cancel(
        Guid id, [FromBody] ReasonRequest request, CancellationToken ct)
    {
        await _mediator.Send(new CancelProjectCommand(id, request.Reason), ct);
        return NoContent();
    }

    [HttpPost("projects/{id:guid}/block")]
    [Authorize(Roles = "Manager")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Block(
        Guid id, [FromBody] ReasonRequest request, CancellationToken ct)
    {
        await _mediator.Send(new BlockProjectCommand(id, request.Reason), ct);
        return NoContent();
    }

    [HttpPost("projects/{id:guid}/unblock")]
    [Authorize(Roles = "Manager")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Unblock(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new UnblockProjectCommand(id), ct);
        return NoContent();
    }

    [HttpGet("projects/{id:guid}/timeline")]
    [ProducesResponseType(typeof(IReadOnlyList<TimelineEntryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TimelineEntryDto>>> GetTimeline(
        Guid id, CancellationToken ct) =>
        Ok(await _mediator.Send(new GetProjectTimelineQuery(id), ct));

    [HttpGet("projects/{id:guid}/snapshots")]
    [Authorize(Roles = "Manager,Leadership")]
    [ProducesResponseType(typeof(IReadOnlyList<ProjectSnapshotDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ProjectSnapshotDto>>> GetSnapshots(
        Guid id, CancellationToken ct) =>
        Ok(await _mediator.Send(new GetProjectSnapshotsQuery(id), ct));

    public record ConvertIdeaRequest(
        string Title, string Description, ProjectPriority Priority, Guid OwnerId,
        decimal? EstimatedCost, DateTimeOffset? Deadline, Guid? AssistantRunId);

    public record UpdateProjectRequest(
        string Title, string Description, ProjectPriority Priority, Guid OwnerId,
        decimal? EstimatedCost, decimal? ActualCost, DateTimeOffset? Deadline);

    public record UpdateProgressRequest(int ProgressPercentage);
    public record AdvanceStageRequest(ProjectStage Stage);
    public record ReasonRequest(string Reason);
}
