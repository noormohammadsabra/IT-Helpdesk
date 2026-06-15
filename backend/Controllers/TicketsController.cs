using HelpDesk.Api.Models;
using HelpDesk.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HelpDesk.Api.Controllers;

[ApiController]
[Authorize]
[Route("api")]
public sealed class TicketsController : ControllerBase
{
    private readonly TicketRepository _tickets;
    private readonly UserRepository _users;

    public TicketsController(TicketRepository tickets, UserRepository users)
    {
        _tickets = tickets;
        _users = users;
    }

    [HttpGet("ticket-categories")]
    public async Task<IActionResult> GetCategories()
    {
        return Ok(await _tickets.GetCategoriesAsync());
    }

    [HttpGet("ticket-priorities")]
    public async Task<IActionResult> GetPriorities()
    {
        return Ok(await _tickets.GetPrioritiesAsync());
    }

    [HttpGet("ticket-statuses")]
    public async Task<IActionResult> GetStatuses()
    {
        return Ok(await _tickets.GetStatusesAsync());
    }

    [HttpGet("tickets")]
    public async Task<IActionResult> GetTickets()
    {
        var userId = CurrentUser.GetUserId(User);
        var role = CurrentUser.GetRole(User);
        return Ok(await _tickets.GetTicketsAsync(userId, role));
    }

    [HttpPost("tickets")]
    public async Task<IActionResult> CreateTicket(TicketCreateRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Description))
        {
            return BadRequest(new { message = "Title and description are required." });
        }

        var userId = CurrentUser.GetUserId(User);
        var createdTicket = await _tickets.CreateTicketAsync(request, userId);

        return Created($"/api/tickets/{createdTicket.Id}", createdTicket);
    }

    [HttpPut("tickets/{id:int}")]
    public async Task<IActionResult> UpdateTicket(int id, TicketUpdateRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Description))
        {
            return BadRequest(new { message = "Title and description are required." });
        }

        var userId = CurrentUser.GetUserId(User);
        var role = CurrentUser.GetRole(User);
        var updatedTicket = await _tickets.UpdateTicketAsync(id, request, userId, role);

        return updatedTicket is null ? NotFound() : Ok(updatedTicket);
    }

    [HttpDelete("tickets/{id:int}")]
    public async Task<IActionResult> DeleteTicket(int id)
    {
        var userId = CurrentUser.GetUserId(User);
        var role = CurrentUser.GetRole(User);
        var deleted = await _tickets.DeleteTicketAsync(id, userId, role);

        return deleted ? NoContent() : NotFound();
    }

    [Authorize(Roles = "Admin,Agent,Manager")]
    [HttpPost("tickets/{id:int}/assign")]
    public async Task<IActionResult> AssignTicket(int id, AssignTicketRequest request)
    {
        var userId = CurrentUser.GetUserId(User);
        var role = CurrentUser.GetRole(User);
        var updatedTicket = await _tickets.AssignTicketAsync(id, request.AgentUserId, userId, role);

        return updatedTicket is null ? NotFound() : Ok(updatedTicket);
    }

    [HttpPost("tickets/{id:int}/status")]
    public async Task<IActionResult> UpdateStatus(int id, UpdateTicketStatusRequest request)
    {
        var userId = CurrentUser.GetUserId(User);
        var role = CurrentUser.GetRole(User);
        var updatedTicket = await _tickets.UpdateTicketStatusAsync(id, request.StatusId, userId, role);

        return updatedTicket is null ? NotFound() : Ok(updatedTicket);
    }

    [HttpGet("tickets/{id:int}/comments")]
    public async Task<IActionResult> GetComments(int id)
    {
        var userId = CurrentUser.GetUserId(User);
        var role = CurrentUser.GetRole(User);
        var comments = await _tickets.GetCommentsAsync(id, userId, role);

        return comments is null ? NotFound() : Ok(comments);
    }

    [HttpPost("tickets/{id:int}/comments")]
    public async Task<IActionResult> AddComment(int id, TicketCommentRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.CommentText))
        {
            return BadRequest(new { message = "Comment text is required." });
        }

        var userId = CurrentUser.GetUserId(User);
        var role = CurrentUser.GetRole(User);
        var comment = await _tickets.AddCommentAsync(id, request, userId, role);

        return comment is null ? NotFound() : Created($"/api/tickets/{id}/comments/{comment.Id}", comment);
    }

    [HttpGet("tickets/{id:int}/activity")]
    public async Task<IActionResult> GetActivity(int id)
    {
        var userId = CurrentUser.GetUserId(User);
        var role = CurrentUser.GetRole(User);
        var activity = await _tickets.GetActivityAsync(id, userId, role);

        return activity is null ? NotFound() : Ok(activity);
    }

    [Authorize(Roles = "Admin,Agent,Manager")]
    [HttpGet("agents")]
    public async Task<IActionResult> GetAgents()
    {
        var users = await _users.GetAllAsync();
        return Ok(users
            .Where(user => user.RoleName is "Agent" or "Admin")
            .Select(user => new
            {
                id = user.Id,
                fullName = user.FullName,
                email = user.Email,
                roleName = user.RoleName,
            }));
    }
}
