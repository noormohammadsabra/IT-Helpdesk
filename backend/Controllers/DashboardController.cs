using HelpDesk.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HelpDesk.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/dashboard")]
public sealed class DashboardController : ControllerBase
{
    private readonly TicketRepository _tickets;

    public DashboardController(TicketRepository tickets)
    {
        _tickets = tickets;
    }

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var userId = CurrentUser.GetUserId(User);
        var role = CurrentUser.GetRole(User);
        var tickets = await _tickets.GetTicketsAsync(userId, role);

        return Ok(new
        {
            openTickets = tickets.Count(ticket => ticket.StatusName == "Open"),
            inProgressTickets = tickets.Count(ticket => ticket.StatusName == "In Progress"),
            resolvedTickets = tickets.Count(ticket => ticket.StatusName == "Resolved"),
            recentTickets = tickets.Take(5),
        });
    }
}
