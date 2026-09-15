using Flow.Application.Auth;
using Flow.Application.Auth.Commands.Login;
using Flow.Application.Auth.Commands.Logout;
using Flow.Application.Auth.Commands.RefreshToken;
using Flow.Application.Auth.Commands.Register;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Flow.API.Controllers;

[ApiController]
[Route("api/v1/auth")]
// Credential stuffing is cheap for an attacker and expensive for us, so the whole
// authentication surface is rate limited per source address.
[EnableRateLimiting(RateLimitPolicies.Auth)]
[Produces("application/json")]
public class AuthController : ControllerBase
{
    private readonly IMediator _mediator;

    public AuthController(IMediator mediator) => _mediator = mediator;

    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResultDto>> Register(
        [FromBody] RegisterCommand command, CancellationToken ct)
    {
        var result = await _mediator.Send(command, ct);
        return Ok(result);
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResultDto>> Login(
        [FromBody] LoginCommand command, CancellationToken ct)
    {
        var result = await _mediator.Send(command, ct);
        return Ok(result);
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResultDto>> Refresh(
        [FromBody] RefreshTokenCommand command, CancellationToken ct)
    {
        var result = await _mediator.Send(command, ct);
        return Ok(result);
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout([FromBody] LogoutCommand command, CancellationToken ct)
    {
        await _mediator.Send(command, ct);
        return NoContent();
    }
}
