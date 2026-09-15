using Flow.Application.Gamification;
using Flow.Application.Gamification.Queries.GetMyPoints;
using Flow.Application.Gamification.Queries.GetMyPointsLedger;
using Flow.Application.Gamification.Queries.GetUserPoints;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Flow.API.Controllers;

[ApiController]
[Route("api/v1/users")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly IMediator _mediator;

    public UsersController(IMediator mediator) => _mediator = mediator;

    [HttpGet("me/points")]
    [Authorize(Roles = "Operator")]
    public async Task<ActionResult<PointsSummaryDto>> GetMyPoints(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetMyPointsQuery(), ct);
        return Ok(result);
    }

    [HttpGet("me/points/ledger")]
    [Authorize(Roles = "Operator")]
    public async Task<ActionResult<IReadOnlyList<PointsLedgerEntryDto>>> GetMyPointsLedger(
        CancellationToken ct)
    {
        var result = await _mediator.Send(new GetMyPointsLedgerQuery(), ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}/points")]
    [Authorize(Roles = "Manager,Leadership")]
    public async Task<ActionResult<PointsSummaryDto>> GetUserPoints(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetUserPointsQuery(id), ct);
        return Ok(result);
    }
}
