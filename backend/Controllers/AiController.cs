using HelpDesk.Api.Models;
using HelpDesk.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HelpDesk.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/ai")]
public sealed class AiController : ControllerBase
{
    private readonly AiAssistantService _assistant;

    public AiController(AiAssistantService assistant)
    {
        _assistant = assistant;
    }

    [HttpPost("ticket-analysis")]
    public IActionResult AnalyzeTicket(AiTicketAnalysisRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Description))
        {
            return BadRequest(new { message = "Title and description are required." });
        }

        return Ok(_assistant.AnalyzeTicket(request));
    }

    [HttpPost("chat")]
    public IActionResult Chat(AiChatRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
        {
            return BadRequest(new { message = "Message is required." });
        }

        return Ok(_assistant.Chat(request));
    }
}
