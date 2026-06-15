using System.Security.Claims;
using HelpDesk.Api.Models;
using HelpDesk.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HelpDesk.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly UserRepository _users;
    private readonly PasswordService _passwords;
    private readonly TokenService _tokens;

    public AuthController(UserRepository users, PasswordService passwords, TokenService tokens)
    {
        _users = users;
        _passwords = passwords;
        _tokens = tokens;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var user = await _users.GetByEmailAsync(request.Email);

        if (user is null || !_passwords.VerifyPassword(request.Password, user.PasswordHash))
        {
            return Unauthorized();
        }

        var token = _tokens.CreateToken(user);

        return Ok(new LoginResponse(
            token,
            user.FullName,
            user.Email,
            user.RoleName,
            user.Department));
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequest request)
    {
        var existingUser = await _users.GetByEmailAsync(request.Email);

        if (existingUser is not null)
        {
            return Conflict(new { message = "A user with this email already exists." });
        }

        var passwordHash = _passwords.HashPassword(request.Password);
        var user = await _users.CreateAsync(request, passwordHash);

        return Created($"/api/users/{user.Id}", new
        {
            user.Id,
            user.FullName,
            user.Email,
            user.RoleName,
            user.Department,
        });
    }

    [Authorize]
    [HttpGet("me")]
    public IActionResult Me()
    {
        return Ok(new
        {
            id = User.FindFirstValue(ClaimTypes.NameIdentifier),
            fullName = User.FindFirstValue(ClaimTypes.Name),
            email = User.FindFirstValue(ClaimTypes.Email),
            role = User.FindFirstValue(ClaimTypes.Role),
        });
    }
}
