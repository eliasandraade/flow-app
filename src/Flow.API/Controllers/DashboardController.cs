using Flow.Application.Assistant.Commands.GenerateExecutiveInsights;
using Flow.Application.Dashboard;
using Flow.Application.Dashboard.Queries.GetDashboardSummary;
using Flow.Application.Dashboard.Queries.GetProjectDashboard;
using Flow.Application.Dashboard.Queries.GetStrategyDashboard;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Flow.API.Controllers;

[ApiController]
[Route("api/v1/dashboard")]
[Authorize(Roles = "Manager,Leadership")]
[Produces("application/json")]
public class DashboardController : ControllerBase
{
    private readonly IMediator _mediator;

    public DashboardController(IMediator mediator) => _mediator = mediator;

    /// <summary>
    /// The full executive picture: idea funnel, project health, financial and non-financial
    /// outcomes, blockers, risk, rankings, strategy and campaign performance, and trends.
    /// Everything arrives ready to draw.
    /// </summary>
    [HttpGet("summary")]
    [ProducesResponseType(typeof(DashboardSummaryDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<DashboardSummaryDto>> GetSummary(CancellationToken ct) =>
        Ok(await _mediator.Send(new GetDashboardSummaryQuery(), ct));

    [HttpGet("projects/{id:guid}")]
    [ProducesResponseType(typeof(ProjectDashboardDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProjectDashboardDto>> GetProject(Guid id, CancellationToken ct) =>
        Ok(await _mediator.Send(new GetProjectDashboardQuery(id), ct));

    [HttpGet("strategies/{id:guid}")]
    [ProducesResponseType(typeof(StrategyDashboardDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StrategyDashboardDto>> GetStrategy(Guid id, CancellationToken ct) =>
        Ok(await _mediator.Send(new GetStrategyDashboardQuery(id), ct));

    /// <summary>
    /// Turns the dashboard into an executive narrative: summary, highlights, risks,
    /// opportunities and recommendations, each citing the figures it came from.
    ///
    /// The analysis is built strictly from the dashboard above, so it cannot report a
    /// number that is not on it. When the programme has too little data, it says so instead
    /// of manufacturing insight.
    /// </summary>
    [HttpPost("insights")]
    [Authorize(Roles = "Leadership")]
    [EnableRateLimiting(RateLimitPolicies.Ai)]
    [ProducesResponseType(typeof(ExecutiveInsightDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<ExecutiveInsightDto>> GenerateInsights(CancellationToken ct) =>
        Ok(await _mediator.Send(new GenerateExecutiveInsightsCommand(), ct));
}
