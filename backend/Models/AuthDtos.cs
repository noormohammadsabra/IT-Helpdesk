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

public sealed record LookupItem(int Id, string Name);

public sealed record TicketResponse(
    int Id,
    string TicketNumber,
    string Title,
    string Description,
    int CategoryId,
    string CategoryName,
    int PriorityId,
    string PriorityName,
    int StatusId,
    string StatusName,
    int CreatedByUserAccountId,
    string CreatedByName,
    DateTime CreatedDate,
    DateTime UpdatedDate);

public sealed record TicketCreateRequest(
    string Title,
    string Description,
    int CategoryId,
    int PriorityId);

public sealed record TicketUpdateRequest(
    string Title,
    string Description,
    int CategoryId,
    int PriorityId,
    int StatusId);
