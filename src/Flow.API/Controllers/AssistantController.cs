using Flow.Application.Assistant;
using Flow.Application.Assistant.Commands.CompareIdeasWithAssistant;
using Flow.Application.Assistant.Commands.DraftProjectFromIdea;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Flow.API.Controllers;

/// <summary>
/// The manager's copilot.
///
/// Every operation here advises and nothing more. No endpoint on this controller changes
/// domain state: a suggestion becomes real only when the manager runs the ordinary command
/// that creates or updates the entity, with the usual authorisation, validation and audit.
/// </summary>
[ApiController]
[Route("api/v1/assistant")]
[Authorize(Roles = "Manager,Leadership")]
[EnableRateLimiting(RateLimitPolicies.Ai)]
[Produces("application/json")]
public class AssistantController : ControllerBase
{
    private readonly IMediator _mediator;

    public AssistantController(IMediator mediator) => _mediator = mediator;

    /// <summary>
    /// Compares ideas under review: strategic fit, strengths, risks and trade-offs, with the
    /// evidence behind each conclusion.
    /// </summary>
    [HttpPost("compare-ideas")]
    [ProducesResponseType(typeof(AssistantComparisonDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<AssistantComparisonDto>> CompareIdeas(
        [FromBody] CompareIdeasRequest request, CancellationToken ct) =>
        Ok(await _mediator.Send(
            new CompareIdeasWithAssistantCommand(request.IdeaIds, request.Question), ct));

    /// <summary>
    /// Proposes a project for an approved idea. Returns an editable draft; it creates
    /// nothing. Pass the returned assistantRunId when converting the idea so the
    /// governance record shows the suggestion was acted on.
    /// </summary>
    [HttpPost("ideas/{ideaId:guid}/project-draft")]
    [ProducesResponseType(typeof(ProjectDraftDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<ProjectDraftDto>> DraftProject(
        Guid ideaId, CancellationToken ct) =>
        Ok(await _mediator.Send(new DraftProjectFromIdeaCommand(ideaId), ct));

    public record CompareIdeasRequest(IReadOnlyList<Guid> IdeaIds, string? Question);
}
