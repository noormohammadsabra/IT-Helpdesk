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
    private readonly NotificationService _notifications;
    private readonly IWebHostEnvironment _environment;

    public TicketsController(
        TicketRepository tickets,
        UserRepository users,
        NotificationService notifications,
        IWebHostEnvironment environment)
    {
        _tickets = tickets;
        _users = users;
        _notifications = notifications;
        _environment = environment;
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
        await _notifications.CreateForUserAsync(
            userId,
            createdTicket.Id,
            "Ticket created",
            $"{createdTicket.TicketNumber} was created successfully.");

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

        if (updatedTicket is null)
        {
            return NotFound();
        }

        await _notifications.CreateForUserAsync(
            request.AgentUserId,
            updatedTicket.Id,
            "Ticket assigned",
            $"{updatedTicket.TicketNumber} was assigned to you.");

        return Ok(updatedTicket);
    }

    [HttpPost("tickets/{id:int}/status")]
    public async Task<IActionResult> UpdateStatus(int id, UpdateTicketStatusRequest request)
    {
        var userId = CurrentUser.GetUserId(User);
        var role = CurrentUser.GetRole(User);
        var updatedTicket = await _tickets.UpdateTicketStatusAsync(id, request.StatusId, userId, role);

        if (updatedTicket is null)
        {
            return NotFound();
        }

        await _notifications.NotifyTicketParticipantsAsync(
            updatedTicket,
            userId,
            "Ticket status updated",
            $"{updatedTicket.TicketNumber} status changed to {updatedTicket.StatusName}.");

        return Ok(updatedTicket);
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

        if (comment is null)
        {
            return NotFound();
        }

        var ticket = await _tickets.GetTicketAsync(id, userId, role);
        if (ticket is not null)
        {
            await _notifications.NotifyTicketParticipantsAsync(
                ticket,
                userId,
                request.IsInternal ? "Internal note added" : "Ticket comment added",
                $"{ticket.TicketNumber} received a new {(request.IsInternal ? "internal note" : "comment")}.");
        }

        return Created($"/api/tickets/{id}/comments/{comment.Id}", comment);
    }

    [HttpGet("tickets/{id:int}/activity")]
    public async Task<IActionResult> GetActivity(int id)
    {
        var userId = CurrentUser.GetUserId(User);
        var role = CurrentUser.GetRole(User);
        var activity = await _tickets.GetActivityAsync(id, userId, role);

        return activity is null ? NotFound() : Ok(activity);
    }

    [HttpGet("tickets/{id:int}/attachments")]
    public async Task<IActionResult> GetAttachments(int id)
    {
        var userId = CurrentUser.GetUserId(User);
        var role = CurrentUser.GetRole(User);
        var attachments = await _tickets.GetAttachmentsAsync(id, userId, role);

        return attachments is null ? NotFound() : Ok(attachments);
    }

    [HttpPost("tickets/{id:int}/attachments")]
    [RequestSizeLimit(10_000_000)]
    public async Task<IActionResult> UploadAttachment(int id, [FromForm] IFormFile? file)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest(new { message = "File is required." });
        }

        var userId = CurrentUser.GetUserId(User);
        var role = CurrentUser.GetRole(User);
        var ticket = await _tickets.GetTicketAsync(id, userId, role);

        if (ticket is null)
        {
            return NotFound();
        }

        var uploadRoot = Path.Combine(_environment.ContentRootPath, "Uploads", "tickets", id.ToString());
        Directory.CreateDirectory(uploadRoot);

        var safeFileName = Path.GetFileName(file.FileName);
        var storedFileName = $"{Guid.NewGuid():N}{Path.GetExtension(safeFileName)}";
        var filePath = Path.Combine(uploadRoot, storedFileName);

        await using (var stream = System.IO.File.Create(filePath))
        {
            await file.CopyToAsync(stream);
        }

        var attachment = await _tickets.AddAttachmentAsync(
            id,
            userId,
            role,
            safeFileName,
            storedFileName,
            filePath,
            string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType,
            file.Length);

        if (attachment is null)
        {
            return NotFound();
        }

        await _notifications.NotifyTicketParticipantsAsync(
            ticket,
            userId,
            "Attachment uploaded",
            $"{ticket.TicketNumber} received a new attachment: {safeFileName}.");

        return Created($"/api/tickets/{id}/attachments/{attachment.Id}", attachment);
    }

    [HttpGet("tickets/{ticketId:int}/attachments/{attachmentId:int}/download")]
    public async Task<IActionResult> DownloadAttachment(int ticketId, int attachmentId)
    {
        var userId = CurrentUser.GetUserId(User);
        var role = CurrentUser.GetRole(User);
        var result = await _tickets.GetAttachmentFileAsync(ticketId, attachmentId, userId, role);

        if (result is null || !System.IO.File.Exists(result.Value.FilePath))
        {
            return NotFound();
        }

        return PhysicalFile(
            result.Value.FilePath,
            result.Value.Attachment.ContentType,
            result.Value.Attachment.FileName);
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
