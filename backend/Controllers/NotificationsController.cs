using HelpDesk.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HelpDesk.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/notifications")]
public sealed class NotificationsController : ControllerBase
{
    private readonly NotificationService _notifications;

    public NotificationsController(NotificationService notifications)
    {
        _notifications = notifications;
    }

    [HttpGet]
    public async Task<IActionResult> GetNotifications()
    {
        var userId = CurrentUser.GetUserId(User);
        return Ok(await _notifications.GetForUserAsync(userId));
    }

    [HttpPost("{id:int}/read")]
    public async Task<IActionResult> MarkAsRead(int id)
    {
        var userId = CurrentUser.GetUserId(User);
        await _notifications.MarkAsReadAsync(id, userId);
        return NoContent();
    }
}
