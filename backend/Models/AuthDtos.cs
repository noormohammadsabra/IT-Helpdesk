namespace HelpDesk.Api.Models;

public sealed record LoginRequest(string Email, string Password);

public sealed record LoginResponse(
    string Token,
    string FullName,
    string Email,
    string Role,
    string Department);

public sealed record RegisterRequest(
    string FullName,
    string Email,
    string Password,
    string RoleName,
    string Department);

public sealed record AppUser(
    int Id,
    string FullName,
    string Email,
    string PasswordHash,
    string RoleName,
    string Department,
    bool IsActive);

public sealed record JwtSettings(
    string Issuer,
    string Audience,
    string SecretKey,
    int ExpirationMinutes);
