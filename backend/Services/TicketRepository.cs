using HelpDesk.Api.Models;
using Microsoft.Data.SqlClient;

namespace HelpDesk.Api.Services;

public sealed class TicketRepository
{
    private readonly string _connectionString;

    public TicketRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("DefaultConnection is missing.");
    }

    public async Task<IReadOnlyList<LookupItem>> GetCategoriesAsync()
    {
        return await Task.FromResult(GetLookup("TicketCategory", "CategoryName"));
    }

    public async Task<IReadOnlyList<LookupItem>> GetPrioritiesAsync()
    {
        return await Task.FromResult(GetLookup("TicketPriority", "PriorityName"));
    }

    public async Task<IReadOnlyList<LookupItem>> GetStatusesAsync()
    {
        return await Task.FromResult(GetLookup("TicketStatus", "StatusName"));
    }

    public async Task<IReadOnlyList<TicketResponse>> GetTicketsAsync(int userId, string role)
    {
        var tickets = new List<TicketResponse>();

        using var connection = CreateConnection();
        using var command = connection.CreateCommand();
        command.CommandText = $"""
            {TicketSelectSql}
            WHERE (@CanViewAll = 1 OR t.CreatedByUserAccountId = @UserId OR t.AssignedToUserAccountId = @UserId)
            ORDER BY t.UpdatedDate DESC, t.Id DESC;
            """;
        command.Parameters.AddWithValue("@CanViewAll", CanViewAllTickets(role));
        command.Parameters.AddWithValue("@UserId", userId);

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            tickets.Add(ReadTicket(reader));
        }

        return await Task.FromResult(tickets);
    }

    public async Task<TicketResponse?> GetTicketAsync(int ticketId, int userId, string role)
    {
        using var connection = CreateConnection();
        return await Task.FromResult(GetTicket(connection, ticketId, userId, role));
    }

    public async Task<TicketResponse> CreateTicketAsync(TicketCreateRequest request, int userId)
    {
        using var connection = CreateConnection();
        var openStatusId = GetLookupId(connection, "TicketStatus", "StatusName", "Open");

        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Ticket
            (
                Title,
                Description,
                CreatedByUserAccountId,
                TicketCategoryId,
                TicketPriorityId,
                TicketStatusId
            )
            OUTPUT INSERTED.Id
            VALUES
            (
                @Title,
                @Description,
                @CreatedByUserAccountId,
                @TicketCategoryId,
                @TicketPriorityId,
                @TicketStatusId
            );
            """;
        command.Parameters.AddWithValue("@Title", request.Title.Trim());
        command.Parameters.AddWithValue("@Description", request.Description.Trim());
        command.Parameters.AddWithValue("@CreatedByUserAccountId", userId);
        command.Parameters.AddWithValue("@TicketCategoryId", request.CategoryId);
        command.Parameters.AddWithValue("@TicketPriorityId", request.PriorityId);
        command.Parameters.AddWithValue("@TicketStatusId", openStatusId);

        var ticketId = (int)command.ExecuteScalar()!;
        AddActivity(connection, ticketId, userId, "Ticket Created", $"Ticket was created with title '{request.Title.Trim()}'.");

        return await Task.FromResult(GetTicket(connection, ticketId, userId, "Admin")
            ?? throw new InvalidOperationException("Created ticket could not be loaded."));
    }

    public async Task<TicketResponse?> UpdateTicketAsync(
        int ticketId,
        TicketUpdateRequest request,
        int userId,
        string role)
    {
        using var connection = CreateConnection();
        var existingTicket = GetTicket(connection, ticketId, userId, role);

        if (existingTicket is null || !CanEditTicket(existingTicket, userId, role))
        {
            return null;
        }

        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE Ticket
            SET
                Title = @Title,
                Description = @Description,
                TicketCategoryId = @TicketCategoryId,
                TicketPriorityId = @TicketPriorityId,
                TicketStatusId = @TicketStatusId,
                UpdatedDate = SYSUTCDATETIME()
            WHERE Id = @TicketId;
            """;
        command.Parameters.AddWithValue("@TicketId", ticketId);
        command.Parameters.AddWithValue("@Title", request.Title.Trim());
        command.Parameters.AddWithValue("@Description", request.Description.Trim());
        command.Parameters.AddWithValue("@TicketCategoryId", request.CategoryId);
        command.Parameters.AddWithValue("@TicketPriorityId", request.PriorityId);
        command.Parameters.AddWithValue("@TicketStatusId", request.StatusId);
        command.ExecuteNonQuery();

        AddActivity(connection, ticketId, userId, "Ticket Updated", "Ticket title, description, category, priority, or status was updated.");
        return await Task.FromResult(GetTicket(connection, ticketId, userId, role));
    }

    public async Task<bool> DeleteTicketAsync(int ticketId, int userId, string role)
    {
        using var connection = CreateConnection();
        var existingTicket = GetTicket(connection, ticketId, userId, role);

        if (existingTicket is null || !CanEditTicket(existingTicket, userId, role))
        {
            return false;
        }

        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Ticket WHERE Id = @TicketId;";
        command.Parameters.AddWithValue("@TicketId", ticketId);
        return await Task.FromResult(command.ExecuteNonQuery() > 0);
    }

    public async Task<TicketResponse?> AssignTicketAsync(int ticketId, int agentUserId, int actorUserId, string role)
    {
        using var connection = CreateConnection();
        var existingTicket = GetTicket(connection, ticketId, actorUserId, role);

        if (existingTicket is null || !CanManageAllTickets(role))
        {
            return null;
        }

        var agentName = GetUserFullName(connection, agentUserId);
        if (agentName is null)
        {
            return null;
        }

        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE Ticket
            SET AssignedToUserAccountId = @AgentUserId,
                UpdatedDate = SYSUTCDATETIME()
            WHERE Id = @TicketId;
            """;
        command.Parameters.AddWithValue("@TicketId", ticketId);
        command.Parameters.AddWithValue("@AgentUserId", agentUserId);
        command.ExecuteNonQuery();

        AddActivity(connection, ticketId, actorUserId, "Ticket Assigned", $"Ticket assigned to {agentName}.");
        return await Task.FromResult(GetTicket(connection, ticketId, actorUserId, role));
    }

    public async Task<TicketResponse?> UpdateTicketStatusAsync(int ticketId, int statusId, int actorUserId, string role)
    {
        using var connection = CreateConnection();
        var existingTicket = GetTicket(connection, ticketId, actorUserId, role);

        if (existingTicket is null || !CanEditTicket(existingTicket, actorUserId, role))
        {
            return null;
        }

        var statusName = GetStatusName(connection, statusId);
        if (statusName is null)
        {
            return null;
        }

        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE Ticket
            SET TicketStatusId = @StatusId,
                UpdatedDate = SYSUTCDATETIME()
            WHERE Id = @TicketId;
            """;
        command.Parameters.AddWithValue("@TicketId", ticketId);
        command.Parameters.AddWithValue("@StatusId", statusId);
        command.ExecuteNonQuery();

        AddActivity(connection, ticketId, actorUserId, "Status Updated", $"Ticket status changed to {statusName}.");
        return await Task.FromResult(GetTicket(connection, ticketId, actorUserId, role));
    }

    public async Task<IReadOnlyList<TicketCommentResponse>?> GetCommentsAsync(int ticketId, int userId, string role)
    {
        using var connection = CreateConnection();
        var ticket = GetTicket(connection, ticketId, userId, role);

        if (ticket is null)
        {
            return null;
        }

        var comments = new List<TicketCommentResponse>();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                tc.Id,
                tc.TicketId,
                ua.Id,
                ua.FullName,
                r.RoleName,
                tc.CommentText,
                tc.IsInternal,
                tc.CreatedDate
            FROM TicketComment tc
            INNER JOIN UserAccount ua ON tc.UserAccountId = ua.Id
            INNER JOIN Role r ON ua.RoleId = r.Id
            WHERE tc.TicketId = @TicketId
              AND (@CanViewInternal = 1 OR tc.IsInternal = 0)
            ORDER BY tc.CreatedDate ASC, tc.Id ASC;
            """;
        command.Parameters.AddWithValue("@TicketId", ticketId);
        command.Parameters.AddWithValue("@CanViewInternal", CanManageAllTickets(role));

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            comments.Add(new TicketCommentResponse(
                reader.GetInt32(0),
                reader.GetInt32(1),
                reader.GetInt32(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetString(5),
                reader.GetBoolean(6),
                reader.GetDateTime(7)));
        }

        return await Task.FromResult(comments);
    }

    public async Task<TicketCommentResponse?> AddCommentAsync(
        int ticketId,
        TicketCommentRequest request,
        int userId,
        string role)
    {
        using var connection = CreateConnection();
        var ticket = GetTicket(connection, ticketId, userId, role);

        if (ticket is null)
        {
            return null;
        }

        var isInternal = request.IsInternal && CanManageAllTickets(role);

        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO TicketComment (TicketId, UserAccountId, CommentText, IsInternal)
            OUTPUT INSERTED.Id
            VALUES (@TicketId, @UserAccountId, @CommentText, @IsInternal);
            """;
        command.Parameters.AddWithValue("@TicketId", ticketId);
        command.Parameters.AddWithValue("@UserAccountId", userId);
        command.Parameters.AddWithValue("@CommentText", request.CommentText.Trim());
        command.Parameters.AddWithValue("@IsInternal", isInternal);
        var commentId = (int)command.ExecuteScalar()!;

        AddActivity(
            connection,
            ticketId,
            userId,
            isInternal ? "Internal Note Added" : "Comment Added",
            isInternal ? "An internal note was added." : "A visible comment was added.");

        return await Task.FromResult(GetComment(connection, commentId));
    }

    public async Task<IReadOnlyList<ActivityLogResponse>?> GetActivityAsync(int ticketId, int userId, string role)
    {
        using var connection = CreateConnection();
        var ticket = GetTicket(connection, ticketId, userId, role);

        if (ticket is null)
        {
            return null;
        }

        var activity = new List<ActivityLogResponse>();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                al.Id,
                al.TicketId,
                ua.Id,
                ua.FullName,
                al.ActionName,
                al.ActionDetails,
                al.CreatedDate
            FROM ActivityLog al
            INNER JOIN UserAccount ua ON al.UserAccountId = ua.Id
            WHERE al.TicketId = @TicketId
            ORDER BY al.CreatedDate ASC, al.Id ASC;
            """;
        command.Parameters.AddWithValue("@TicketId", ticketId);

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            activity.Add(new ActivityLogResponse(
                reader.GetInt32(0),
                reader.GetInt32(1),
                reader.GetInt32(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetString(5),
                reader.GetDateTime(6)));
        }

        return await Task.FromResult(activity);
    }

    public async Task<DashboardAnalyticsResponse> GetDashboardAnalyticsAsync(int userId, string role)
    {
        var tickets = await GetTicketsAsync(userId, role);
        var ticketList = tickets.ToList();

        var byStatus = ticketList
            .GroupBy(ticket => ticket.StatusName)
            .Select(group => new ChartPoint(group.Key, group.Count()))
            .OrderBy(point => point.Name)
            .ToList();

        var byCategory = ticketList
            .GroupBy(ticket => ticket.CategoryName)
            .Select(group => new ChartPoint(group.Key, group.Count()))
            .OrderByDescending(point => point.Value)
            .ToList();

        var byPriority = ticketList
            .GroupBy(ticket => ticket.PriorityName)
            .Select(group => new ChartPoint(group.Key, group.Count()))
            .OrderByDescending(point => point.Value)
            .ToList();

        var byAgent = ticketList
            .Where(ticket => !string.IsNullOrWhiteSpace(ticket.AssignedAgentName))
            .GroupBy(ticket => ticket.AssignedAgentName!)
            .Select(group => new ChartPoint(group.Key, group.Count()))
            .OrderByDescending(point => point.Value)
            .ToList();

        return new DashboardAnalyticsResponse(
            ticketList.Count,
            ticketList.Count(ticket => ticket.StatusName == "Open"),
            ticketList.Count(ticket => ticket.StatusName == "In Progress"),
            ticketList.Count(ticket => ticket.StatusName == "Resolved"),
            ticketList.Count(ticket => ticket.PriorityName == "Critical"),
            byStatus,
            byCategory,
            byPriority,
            byAgent);
    }

    public async Task<AttachmentResponse?> AddAttachmentAsync(
        int ticketId,
        int userId,
        string role,
        string fileName,
        string storedFileName,
        string filePath,
        string contentType,
        long fileSizeBytes)
    {
        using var connection = CreateConnection();
        var ticket = GetTicket(connection, ticketId, userId, role);

        if (ticket is null)
        {
            return null;
        }

        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO TicketAttachment
            (
                TicketId,
                UploadedByUserAccountId,
                FileName,
                StoredFileName,
                FilePath,
                ContentType,
                FileSizeBytes
            )
            OUTPUT INSERTED.Id
            VALUES
            (
                @TicketId,
                @UploadedByUserAccountId,
                @FileName,
                @StoredFileName,
                @FilePath,
                @ContentType,
                @FileSizeBytes
            );
            """;
        command.Parameters.AddWithValue("@TicketId", ticketId);
        command.Parameters.AddWithValue("@UploadedByUserAccountId", userId);
        command.Parameters.AddWithValue("@FileName", fileName);
        command.Parameters.AddWithValue("@StoredFileName", storedFileName);
        command.Parameters.AddWithValue("@FilePath", filePath);
        command.Parameters.AddWithValue("@ContentType", contentType);
        command.Parameters.AddWithValue("@FileSizeBytes", fileSizeBytes);

        var attachmentId = (int)command.ExecuteScalar()!;
        AddActivity(connection, ticketId, userId, "Attachment Uploaded", $"File '{fileName}' was uploaded.");

        return await Task.FromResult(GetAttachment(connection, attachmentId));
    }

    public async Task<IReadOnlyList<AttachmentResponse>?> GetAttachmentsAsync(int ticketId, int userId, string role)
    {
        using var connection = CreateConnection();
        var ticket = GetTicket(connection, ticketId, userId, role);

        if (ticket is null)
        {
            return null;
        }

        var attachments = new List<AttachmentResponse>();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                ta.Id,
                ta.TicketId,
                ta.FileName,
                ta.ContentType,
                ta.FileSizeBytes,
                ua.FullName,
                ta.UploadedDate
            FROM TicketAttachment ta
            INNER JOIN UserAccount ua ON ta.UploadedByUserAccountId = ua.Id
            WHERE ta.TicketId = @TicketId
            ORDER BY ta.UploadedDate DESC, ta.Id DESC;
            """;
        command.Parameters.AddWithValue("@TicketId", ticketId);

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            attachments.Add(ReadAttachment(reader));
        }

        return await Task.FromResult(attachments);
    }

    public async Task<(AttachmentResponse Attachment, string FilePath)?> GetAttachmentFileAsync(
        int ticketId,
        int attachmentId,
        int userId,
        string role)
    {
        using var connection = CreateConnection();
        var ticket = GetTicket(connection, ticketId, userId, role);

        if (ticket is null)
        {
            return null;
        }

        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                ta.Id,
                ta.TicketId,
                ta.FileName,
                ta.ContentType,
                ta.FileSizeBytes,
                ua.FullName,
                ta.UploadedDate,
                ta.FilePath
            FROM TicketAttachment ta
            INNER JOIN UserAccount ua ON ta.UploadedByUserAccountId = ua.Id
            WHERE ta.TicketId = @TicketId AND ta.Id = @AttachmentId;
            """;
        command.Parameters.AddWithValue("@TicketId", ticketId);
        command.Parameters.AddWithValue("@AttachmentId", attachmentId);

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        var attachment = new AttachmentResponse(
            reader.GetInt32(0),
            reader.GetInt32(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetInt64(4),
            reader.GetString(5),
            reader.GetDateTime(6));
        var filePath = reader.GetString(7);

        return await Task.FromResult((attachment, filePath));
    }

    private SqlConnection CreateConnection()
    {
        var connection = new SqlConnection(_connectionString);
        connection.Open();
        return connection;
    }

    private TicketResponse? GetTicket(SqlConnection connection, int ticketId, int userId, string role)
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"""
            {TicketSelectSql}
            WHERE t.Id = @TicketId
              AND (@CanViewAll = 1 OR t.CreatedByUserAccountId = @UserId OR t.AssignedToUserAccountId = @UserId);
            """;
        command.Parameters.AddWithValue("@TicketId", ticketId);
        command.Parameters.AddWithValue("@CanViewAll", CanViewAllTickets(role));
        command.Parameters.AddWithValue("@UserId", userId);

        using var reader = command.ExecuteReader();
        return reader.Read() ? ReadTicket(reader) : null;
    }

    private IReadOnlyList<LookupItem> GetLookup(string tableName, string columnName)
    {
        var items = new List<LookupItem>();

        using var connection = CreateConnection();
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT Id, {columnName} FROM {tableName} ORDER BY Id;";

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            items.Add(new LookupItem(reader.GetInt32(0), reader.GetString(1)));
        }

        return items;
    }

    private static int GetLookupId(SqlConnection connection, string tableName, string columnName, string value)
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT Id FROM {tableName} WHERE {columnName} = @Value;";
        command.Parameters.AddWithValue("@Value", value);
        return (int)command.ExecuteScalar()!;
    }

    private static string? GetUserFullName(SqlConnection connection, int userId)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT FullName FROM UserAccount WHERE Id = @UserId AND IsActive = 1;";
        command.Parameters.AddWithValue("@UserId", userId);
        return command.ExecuteScalar() as string;
    }

    private static string? GetStatusName(SqlConnection connection, int statusId)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT StatusName FROM TicketStatus WHERE Id = @StatusId;";
        command.Parameters.AddWithValue("@StatusId", statusId);
        return command.ExecuteScalar() as string;
    }

    private static void AddActivity(SqlConnection connection, int ticketId, int userId, string actionName, string actionDetails)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO ActivityLog (TicketId, UserAccountId, ActionName, ActionDetails)
            VALUES (@TicketId, @UserAccountId, @ActionName, @ActionDetails);
            """;
        command.Parameters.AddWithValue("@TicketId", ticketId);
        command.Parameters.AddWithValue("@UserAccountId", userId);
        command.Parameters.AddWithValue("@ActionName", actionName);
        command.Parameters.AddWithValue("@ActionDetails", actionDetails);
        command.ExecuteNonQuery();
    }

    private static TicketCommentResponse? GetComment(SqlConnection connection, int commentId)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                tc.Id,
                tc.TicketId,
                ua.Id,
                ua.FullName,
                r.RoleName,
                tc.CommentText,
                tc.IsInternal,
                tc.CreatedDate
            FROM TicketComment tc
            INNER JOIN UserAccount ua ON tc.UserAccountId = ua.Id
            INNER JOIN Role r ON ua.RoleId = r.Id
            WHERE tc.Id = @CommentId;
            """;
        command.Parameters.AddWithValue("@CommentId", commentId);

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        return new TicketCommentResponse(
            reader.GetInt32(0),
            reader.GetInt32(1),
            reader.GetInt32(2),
            reader.GetString(3),
            reader.GetString(4),
            reader.GetString(5),
            reader.GetBoolean(6),
            reader.GetDateTime(7));
    }

    private static AttachmentResponse? GetAttachment(SqlConnection connection, int attachmentId)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                ta.Id,
                ta.TicketId,
                ta.FileName,
                ta.ContentType,
                ta.FileSizeBytes,
                ua.FullName,
                ta.UploadedDate
            FROM TicketAttachment ta
            INNER JOIN UserAccount ua ON ta.UploadedByUserAccountId = ua.Id
            WHERE ta.Id = @AttachmentId;
            """;
        command.Parameters.AddWithValue("@AttachmentId", attachmentId);

        using var reader = command.ExecuteReader();
        return reader.Read() ? ReadAttachment(reader) : null;
    }

    private static AttachmentResponse ReadAttachment(SqlDataReader reader)
    {
        return new AttachmentResponse(
            reader.GetInt32(0),
            reader.GetInt32(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetInt64(4),
            reader.GetString(5),
            reader.GetDateTime(6));
    }

    private static bool CanViewAllTickets(string role)
    {
        return role is "Admin" or "Agent" or "Manager";
    }

    private static bool CanManageAllTickets(string role)
    {
        return role is "Admin" or "Agent" or "Manager";
    }

    private static bool CanEditTicket(TicketResponse ticket, int currentUserId, string role)
    {
        return ticket.CreatedByUserAccountId == currentUserId
            || ticket.AssignedToUserAccountId == currentUserId
            || CanManageAllTickets(role);
    }

    private static TicketResponse ReadTicket(SqlDataReader reader)
    {
        return new TicketResponse(
            reader.GetInt32(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetInt32(4),
            reader.GetString(5),
            reader.GetInt32(6),
            reader.GetString(7),
            reader.GetInt32(8),
            reader.GetString(9),
            reader.GetInt32(10),
            reader.GetString(11),
            reader.IsDBNull(12) ? null : reader.GetInt32(12),
            reader.IsDBNull(13) ? null : reader.GetString(13),
            reader.GetDateTime(14),
            reader.GetDateTime(15));
    }

    private const string TicketSelectSql = """
        SELECT
            t.Id,
            t.TicketNumber,
            t.Title,
            t.Description,
            tc.Id AS CategoryId,
            tc.CategoryName,
            tp.Id AS PriorityId,
            tp.PriorityName,
            ts.Id AS StatusId,
            ts.StatusName,
            creator.Id AS CreatedByUserAccountId,
            creator.FullName AS CreatedByName,
            assigned.Id AS AssignedToUserAccountId,
            assigned.FullName AS AssignedAgentName,
            t.CreatedDate,
            t.UpdatedDate
        FROM Ticket t
        INNER JOIN TicketCategory tc ON t.TicketCategoryId = tc.Id
        INNER JOIN TicketPriority tp ON t.TicketPriorityId = tp.Id
        INNER JOIN TicketStatus ts ON t.TicketStatusId = ts.Id
        INNER JOIN UserAccount creator ON t.CreatedByUserAccountId = creator.Id
        LEFT JOIN UserAccount assigned ON t.AssignedToUserAccountId = assigned.Id
        """;
}
