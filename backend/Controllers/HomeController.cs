using Microsoft.AspNetCore.Mvc;

namespace HelpDesk.Api.Controllers;

[ApiController]
[Route("")]
public sealed class HomeController : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        return Ok(new
        {
            application = "IT Help Desk API",
            status = "Running",
            week = "Ticket Workflow, Comments, and History",
            architecture = "ASP.NET Core MVC Controllers",
        });
    }
}
