using Flow.Application.Guidelines;
using Flow.Application.Guidelines.Commands.CloseGuideline;
using Flow.Application.Guidelines.Commands.CreateGuideline;
using Flow.Application.Guidelines.Commands.DeleteGuideline;
using Flow.Application.Guidelines.Commands.UpdateGuideline;
using Flow.Application.Guidelines.Queries.GetCurrentGuidelines;
using Flow.Application.Guidelines.Queries.GetGuidelineById;
using Flow.Application.Guidelines.Queries.GetGuidelineHistory;
using Flow.Application.Guidelines.Queries.GetGuidelines;
using Flow.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Flow.API.Controllers;

[ApiController]
[Route("api/v1/guidelines")]
[Authorize]
[Produces("application/json")]
public class GuidelinesController : ControllerBase
{
    private readonly IMediator _mediator;

    public GuidelinesController(IMediator mediator) => _mediator = mediator;

    /// <summary>Lists strategic guidelines, optionally filtered by category, campaign or validity.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<GuidelineDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<GuidelineDto>>> GetAll(
        [FromQuery] GuidelineCategory? category,
        [FromQuery] string? campaign,
        [FromQuery] bool currentOnly = false,
        CancellationToken ct = default) =>
        Ok(await _mediator.Send(new GetGuidelinesQuery(category, campaign, currentOnly), ct));

    /// <summary>
    /// The strategy currently in force. Validity is derived from the period, so this can
    /// never disagree with the stored dates.
    /// </summary>
    [HttpGet("current")]
    [ProducesResponseType(typeof(IReadOnlyList<GuidelineDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<GuidelineDto>>> GetCurrent(CancellationToken ct) =>
        Ok(await _mediator.Send(new GetCurrentGuidelinesQuery(), ct));

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(GuidelineDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GuidelineDto>> GetById(Guid id, CancellationToken ct) =>
        Ok(await _mediator.Send(new GetGuidelineByIdQuery(id), ct));

    /// <summary>Append-only change history for a guideline.</summary>
    [HttpGet("{id:guid}/history")]
    [ProducesResponseType(typeof(IReadOnlyList<GuidelineHistoryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<GuidelineHistoryDto>>> GetHistory(
        Guid id, CancellationToken ct) =>
        Ok(await _mediator.Send(new GetGuidelineHistoryQuery(id), ct));

    [HttpPost]
    [Authorize(Roles = "Leadership")]
    [ProducesResponseType(typeof(GuidelineDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<GuidelineDto>> Create(
        [FromBody] CreateGuidelineCommand command, CancellationToken ct)
    {
        var result = await _mediator.Send(command, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Leadership")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Update(
        Guid id, [FromBody] UpdateGuidelineRequest request, CancellationToken ct)
    {
        await _mediator.Send(new UpdateGuidelineCommand(
            id, request.Title, request.Description, request.Category,
            request.Campaign, request.ValidFrom, request.ValidUntil), ct);
        return NoContent();
    }

    /// <summary>
    /// Ends a guideline by closing its validity period. Preferred over deletion because
    /// ideas and projects already linked to it keep a resolvable reference.
    /// </summary>
    [HttpPost("{id:guid}/close")]
    [Authorize(Roles = "Leadership")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Close(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new CloseGuidelineCommand(id), ct);
        return NoContent();
    }

    /// <summary>
    /// Permanently removes a guideline. Refused with 409 when ideas or projects reference
    /// it; close it instead.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Leadership")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new DeleteGuidelineCommand(id), ct);
        return NoContent();
    }

    public record UpdateGuidelineRequest(
        string Title,
        string Description,
        GuidelineCategory Category,
        string? Campaign,
        DateTimeOffset ValidFrom,
        DateTimeOffset? ValidUntil);
}
