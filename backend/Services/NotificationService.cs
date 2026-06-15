using HelpDesk.Api.Hubs;
using HelpDesk.Api.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Data.SqlClient;

namespace HelpDesk.Api.Services;

public sealed class NotificationService
{
    private readonly string _connectionString;
    private readonly IHubContext<NotificationHub> _hubContext;

    public NotificationService(IConfiguration configuration, IHubContext<NotificationHub> hubContext)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("DefaultConnection is missing.");
        _hubContext = hubContext;
    }

    public async Task<IReadOnlyList<NotificationResponse>> GetForUserAsync(int userId)
    {
        var notifications = new List<NotificationResponse>();

        using var connection = CreateConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT TOP 30 Id, TicketId, Title, Message, IsRead, CreatedDate
            FROM Notification
            WHERE UserAccountId = @UserAccountId
            ORDER BY CreatedDate DESC, Id DESC;
            """;
        command.Parameters.AddWithValue("@UserAccountId", userId);

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            notifications.Add(new NotificationResponse(
                reader.GetInt32(0),
                reader.IsDBNull(1) ? null : reader.GetInt32(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetBoolean(4),
                reader.GetDateTime(5)));
        }

        return await Task.FromResult(notifications);
    }

    public async Task MarkAsReadAsync(int notificationId, int userId)
    {
        using var connection = CreateConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE Notification
            SET IsRead = 1
            WHERE Id = @Id AND UserAccountId = @UserAccountId;
            """;
        command.Parameters.AddWithValue("@Id", notificationId);
        command.Parameters.AddWithValue("@UserAccountId", userId);
        command.ExecuteNonQuery();

        await _hubContext.Clients.Group($"user-{userId}").SendAsync("NotificationRead", notificationId);
    }

    public async Task CreateForUserAsync(int userId, int? ticketId, string title, string message)
    {
        NotificationResponse notification;

        using (var connection = CreateConnection())
        {
            using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO Notification (UserAccountId, TicketId, Title, Message, IsRead)
                OUTPUT INSERTED.Id, INSERTED.TicketId, INSERTED.Title, INSERTED.Message, INSERTED.IsRead, INSERTED.CreatedDate
                VALUES (@UserAccountId, @TicketId, @Title, @Message, 0);
                """;
            command.Parameters.AddWithValue("@UserAccountId", userId);
            command.Parameters.AddWithValue("@TicketId", ticketId is null ? DBNull.Value : ticketId);
            command.Parameters.AddWithValue("@Title", title);
            command.Parameters.AddWithValue("@Message", message);

            using var reader = command.ExecuteReader();
            reader.Read();
            notification = new NotificationResponse(
                reader.GetInt32(0),
                reader.IsDBNull(1) ? null : reader.GetInt32(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetBoolean(4),
                reader.GetDateTime(5));
        }

        await _hubContext.Clients.Group($"user-{userId}").SendAsync("NotificationReceived", notification);
    }

    public async Task NotifyTicketParticipantsAsync(
        TicketResponse ticket,
        int actorUserId,
        string title,
        string message)
    {
        var recipientIds = new HashSet<int> { ticket.CreatedByUserAccountId };

        if (ticket.AssignedToUserAccountId is not null)
        {
            recipientIds.Add(ticket.AssignedToUserAccountId.Value);
        }

        recipientIds.Remove(actorUserId);

        foreach (var recipientId in recipientIds)
        {
            await CreateForUserAsync(recipientId, ticket.Id, title, message);
        }
    }

    private SqlConnection CreateConnection()
    {
        var connection = new SqlConnection(_connectionString);
        connection.Open();
        return connection;
    }
}
