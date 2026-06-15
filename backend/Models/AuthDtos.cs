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
    int? AssignedToUserAccountId,
    string? AssignedAgentName,
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

public sealed record AssignTicketRequest(int AgentUserId);

public sealed record UpdateTicketStatusRequest(int StatusId);

public sealed record TicketCommentRequest(string CommentText, bool IsInternal);

public sealed record TicketCommentResponse(
    int Id,
    int TicketId,
    int UserAccountId,
    string AuthorName,
    string AuthorRole,
    string CommentText,
    bool IsInternal,
    DateTime CreatedDate);

public sealed record ActivityLogResponse(
    int Id,
    int TicketId,
    int UserAccountId,
    string ActorName,
    string ActionName,
    string ActionDetails,
    DateTime CreatedDate);

public sealed record AttachmentResponse(
    int Id,
    int TicketId,
    string FileName,
    string ContentType,
    long FileSizeBytes,
    string UploadedByName,
    DateTime UploadedDate);

public sealed record NotificationResponse(
    int Id,
    int? TicketId,
    string Title,
    string Message,
    bool IsRead,
    DateTime CreatedDate);

public sealed record DashboardAnalyticsResponse(
    int TotalTickets,
    int OpenTickets,
    int InProgressTickets,
    int ResolvedTickets,
    int CriticalTickets,
    IReadOnlyList<ChartPoint> TicketsByStatus,
    IReadOnlyList<ChartPoint> TicketsByCategory,
    IReadOnlyList<ChartPoint> TicketsByPriority,
    IReadOnlyList<ChartPoint> TicketsByAgent);

public sealed record ChartPoint(string Name, int Value);
