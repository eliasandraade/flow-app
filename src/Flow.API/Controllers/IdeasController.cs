using Flow.Application.Ideas;
using Flow.Application.Ideas.Commands.AddIdeaComment;
using Flow.Application.Ideas.Commands.ApproveIdea;
using Flow.Application.Ideas.Commands.CreateIdea;
using Flow.Application.Ideas.Commands.DeleteIdea;
using Flow.Application.Ideas.Commands.RejectIdea;
using Flow.Application.Ideas.Commands.SetIdeaFlowScore;
using Flow.Application.Ideas.Commands.SetIdeaPriority;
using Flow.Application.Ideas.Commands.SetIdeaScore;
using Flow.Application.Ideas.Commands.SubmitIdea;
using Flow.Application.Ideas.Commands.UpdateIdea;
using Flow.Application.Ideas.Queries.CompareIdeas;
using Flow.Application.Ideas.Queries.GetIdeaById;
using Flow.Application.Ideas.Queries.GetIdeaComments;
using Flow.Application.Ideas.Queries.GetIdeas;
using Flow.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Flow.API.Controllers;

[ApiController]
[Route("api/v1/ideas")]
[Authorize]
[Produces("application/json")]
public class IdeasController : ControllerBase
{
    private readonly IMediator _mediator;

    public IdeasController(IMediator mediator) => _mediator = mediator;

    /// <summary>Creates a new idea in Draft.</summary>
    [HttpPost]
    [Authorize(Roles = "Operator")]
    [ProducesResponseType(typeof(IdeaSummaryDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<IdeaSummaryDto>> Create(
        [FromBody] CreateIdeaCommand command, CancellationToken ct)
    {
        var result = await _mediator.Send(command, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    /// <summary>
    /// Lists ideas. Operators always see only their own, regardless of the filters sent.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<IdeaSummaryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<IdeaSummaryDto>>> GetAll(
        [FromQuery] Guid? submittedById,
        [FromQuery] IdeaStatus? status,
        [FromQuery] IdeaPriority? priority,
        [FromQuery] Guid? linkedGuidelineId,
        [FromQuery] int? minScore,
        [FromQuery] string? sortBy,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50,
        CancellationToken ct = default) =>
        Ok(await _mediator.Send(
            new GetIdeasQuery(submittedById, status, priority, linkedGuidelineId, minScore, sortBy, skip, take), ct));

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(IdeaDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IdeaDetailDto>> GetById(Guid id, CancellationToken ct) =>
        Ok(await _mediator.Send(new GetIdeaByIdQuery(id), ct));

    /// <summary>Compares two to five ideas side by side, purely from stored data.</summary>
    [HttpPost("compare")]
    [Authorize(Roles = "Manager,Leadership")]
    [ProducesResponseType(typeof(IdeaComparisonDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<IdeaComparisonDto>> Compare(
        [FromBody] CompareIdeasRequest request, CancellationToken ct) =>
        Ok(await _mediator.Send(new CompareIdeasQuery(request.IdeaIds), ct));

    /// <summary>Edits a Draft idea. Only the author can do this.</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Operator")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(
        Guid id, [FromBody] UpdateIdeaRequest request, CancellationToken ct)
    {
        await _mediator.Send(new UpdateIdeaCommand(
            id, request.Title, request.Description, request.Problem, request.LinkedGuidelineId), ct);
        return NoContent();
    }

    /// <summary>Deletes a Draft idea. Only the author can do this.</summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Operator")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new DeleteIdeaCommand(id), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/submit")]
    [Authorize(Roles = "Operator")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Submit(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new SubmitIdeaCommand(id), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/approve")]
    [Authorize(Roles = "Manager")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Approve(
        Guid id, [FromBody] ApproveIdeaRequest request, CancellationToken ct)
    {
        await _mediator.Send(new ApproveIdeaCommand(id, request.ManagerComment), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/reject")]
    [Authorize(Roles = "Manager")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Reject(
        Guid id, [FromBody] RejectIdeaRequest request, CancellationToken ct)
    {
        await _mediator.Send(new RejectIdeaCommand(id, request.ManagerComment), ct);
        return NoContent();
    }

    /// <summary>Sets the qualitative priority label. Distinct from score and FlowScore.</summary>
    [HttpPatch("{id:guid}/priority")]
    [Authorize(Roles = "Manager")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> SetPriority(
        Guid id, [FromBody] SetPriorityRequest request, CancellationToken ct)
    {
        await _mediator.Send(new SetIdeaPriorityCommand(id, request.Priority), ct);
        return NoContent();
    }

    /// <summary>Sets the manager's own 0..100 score, which always overrides any suggestion.</summary>
    [HttpPatch("{id:guid}/score")]
    [Authorize(Roles = "Manager")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> SetScore(
        Guid id, [FromBody] SetScoreRequest request, CancellationToken ct)
    {
        await _mediator.Send(new SetIdeaScoreCommand(id, request.Score), ct);
        return NoContent();
    }

    /// <summary>
    /// Records the FlowScore dimensions. The total is recomputed by the domain from these
    /// components, so a caller cannot inject an arbitrary score.
    /// </summary>
    [HttpPut("{id:guid}/flow-score")]
    [Authorize(Roles = "Manager")]
    [ProducesResponseType(typeof(FlowScoreDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<FlowScoreDto>> SetFlowScore(
        Guid id, [FromBody] SetFlowScoreRequest request, CancellationToken ct) =>
        Ok(await _mediator.Send(new SetIdeaFlowScoreCommand(
            id, request.StrategicAlignment, request.Impact,
            request.Feasibility, request.Urgency, request.Confidence), ct));

    [HttpGet("{id:guid}/comments")]
    [ProducesResponseType(typeof(IReadOnlyList<IdeaCommentDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<IdeaCommentDto>>> GetComments(
        Guid id, CancellationToken ct) =>
        Ok(await _mediator.Send(new GetIdeaCommentsQuery(id), ct));

    [HttpPost("{id:guid}/comments")]
    [Authorize(Roles = "Manager,Leadership")]
    [ProducesResponseType(typeof(IdeaCommentDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<IdeaCommentDto>> AddComment(
        Guid id, [FromBody] AddCommentRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new AddIdeaCommentCommand(id, request.Body), ct);
        return CreatedAtAction(nameof(GetComments), new { id }, result);
    }

    public record UpdateIdeaRequest(string Title, string Description, string Problem, Guid? LinkedGuidelineId);
    public record ApproveIdeaRequest(string? ManagerComment);
    public record RejectIdeaRequest(string ManagerComment);
    public record SetPriorityRequest(IdeaPriority Priority);
    public record SetScoreRequest(int Score);
    public record SetFlowScoreRequest(
        int? StrategicAlignment, int Impact, int Feasibility, int Urgency, int Confidence);
    public record AddCommentRequest(string Body);
    public record CompareIdeasRequest(IReadOnlyList<Guid> IdeaIds);
}
