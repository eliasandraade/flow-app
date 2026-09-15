using Flow.Application.Notifications;
using Flow.Application.Notifications.Commands.MarkAllNotificationsRead;
using Flow.Application.Notifications.Commands.MarkNotificationRead;
using Flow.Application.Notifications.Queries.GetMyNotifications;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Flow.API.Controllers;

[ApiController]
[Route("api/v1/notifications")]
[Authorize]
[Produces("application/json")]
public class NotificationsController : ControllerBase
{
    private readonly IMediator _mediator;

    public NotificationsController(IMediator mediator) => _mediator = mediator;

    /// <summary>
    /// The notification centre held inside Flow. Independent of any push provider, so it
    /// keeps working when the provider does not.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(NotificationPageDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<NotificationPageDto>> GetMine(
        [FromQuery] bool unreadOnly = false,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50,
        CancellationToken ct = default) =>
        Ok(await _mediator.Send(new GetMyNotificationsQuery(unreadOnly, skip, take), ct));

    [HttpPost("{id:guid}/read")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> MarkRead(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new MarkNotificationReadCommand(id), ct);
        return NoContent();
    }

    [HttpPost("read-all")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> MarkAllRead(CancellationToken ct)
    {
        await _mediator.Send(new MarkAllNotificationsReadCommand(), ct);
        return NoContent();
    }
}
