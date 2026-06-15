using HelpDesk.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HelpDesk.Api.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin")]
public sealed class AdminController : ControllerBase
{
    private readonly UserRepository _users;

    public AdminController(UserRepository users)
    {
        _users = users;
    }

    [HttpGet("users")]
    public async Task<IActionResult> GetUsers()
    {
        var allUsers = await _users.GetAllAsync();

        return Ok(allUsers.Select(user => new
        {
            user.Id,
            user.FullName,
            user.Email,
            user.RoleName,
            user.Department,
            user.IsActive,
        }));
    }
}
