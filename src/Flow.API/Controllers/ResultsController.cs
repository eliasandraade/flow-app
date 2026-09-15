using Flow.Application.Results;
using Flow.Application.Results.Commands.RecordResult;
using Flow.Application.Results.Queries.GetResult;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Flow.API.Controllers;

[ApiController]
[Route("api/v1/projects/{projectId:guid}/result")]
[Authorize]
[Produces("application/json")]
public class ResultsController : ControllerBase
{
    private readonly IMediator _mediator;

    public ResultsController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [ProducesResponseType(typeof(ResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ResultDto>> Get(Guid projectId, CancellationToken ct) =>
        Ok(await _mediator.Send(new GetResultQuery(projectId), ct));

    /// <summary>
    /// Records estimated and realised outcomes. The two groups are written through
    /// separate operations and never overwrite one another.
    /// </summary>
    [HttpPut]
    [Authorize(Roles = "Manager")]
    [ProducesResponseType(typeof(ResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ResultDto>> Upsert(
        Guid projectId, [FromBody] RecordResultRequest request, CancellationToken ct) =>
        Ok(await _mediator.Send(new RecordResultCommand(
            projectId,
            request.EstimatedRevenue, request.EstimatedSavings, request.EstimatedCost,
            request.ActualRevenue, request.ActualSavings, request.ActualCost,
            request.PaybackPeriodMonths,
            request.ProductivityGainPercent, request.TimeSavedHours, request.QualityGainPercent,
            request.Notes), ct));

    public record RecordResultRequest(
        decimal? EstimatedRevenue,
        decimal? EstimatedSavings,
        decimal? EstimatedCost,
        decimal? ActualRevenue,
        decimal? ActualSavings,
        decimal? ActualCost,
        int? PaybackPeriodMonths,
        decimal? ProductivityGainPercent,
        decimal? TimeSavedHours,
        decimal? QualityGainPercent,
        string? Notes);
}
