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

        using var connection = new SqlConnection(_connectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandTimeout = 60;
        command.CommandText = $"""
            {TicketSelectSql}
            WHERE (@CanViewAll = 1 OR t.CreatedByUserAccountId = @UserId)
            ORDER BY t.UpdatedDate DESC, t.Id DESC;
            """;
        command.Parameters.AddWithValue("@CanViewAll", CanManageAllTickets(role));
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
        using var connection = new SqlConnection(_connectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandTimeout = 60;
        command.CommandText = $"""
            {TicketSelectSql}
            WHERE t.Id = @TicketId
              AND (@CanViewAll = 1 OR t.CreatedByUserAccountId = @UserId);
            """;
        command.Parameters.AddWithValue("@TicketId", ticketId);
        command.Parameters.AddWithValue("@CanViewAll", CanManageAllTickets(role));
        command.Parameters.AddWithValue("@UserId", userId);

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        return await Task.FromResult(ReadTicket(reader));
    }

    public async Task<TicketResponse> CreateTicketAsync(TicketCreateRequest request, int userId)
    {
        using var connection = new SqlConnection(_connectionString);
        connection.Open();

        var openStatusId = GetLookupId(connection, "TicketStatus", "StatusName", "Open");

        using var command = connection.CreateCommand();
        command.CommandTimeout = 60;
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

        return await GetTicketAsync(ticketId, userId, "Admin")
            ?? throw new InvalidOperationException("Created ticket could not be loaded.");
    }

    public async Task<TicketResponse?> UpdateTicketAsync(
        int ticketId,
        TicketUpdateRequest request,
        int userId,
        string role)
    {
        var existingTicket = await GetTicketAsync(ticketId, userId, role);
        if (existingTicket is null)
        {
            return null;
        }

        if (!CanEditTicket(existingTicket.CreatedByUserAccountId, userId, role))
        {
            return null;
        }

        using var connection = new SqlConnection(_connectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandTimeout = 60;
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

        return await GetTicketAsync(ticketId, userId, role);
    }

    public async Task<bool> DeleteTicketAsync(int ticketId, int userId, string role)
    {
        var existingTicket = await GetTicketAsync(ticketId, userId, role);
        if (existingTicket is null || !CanEditTicket(existingTicket.CreatedByUserAccountId, userId, role))
        {
            return false;
        }

        using var connection = new SqlConnection(_connectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandTimeout = 60;
        command.CommandText = "DELETE FROM Ticket WHERE Id = @TicketId;";
        command.Parameters.AddWithValue("@TicketId", ticketId);

        return await Task.FromResult(command.ExecuteNonQuery() > 0);
    }

    private IReadOnlyList<LookupItem> GetLookup(string tableName, string columnName)
    {
        var items = new List<LookupItem>();

        using var connection = new SqlConnection(_connectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandTimeout = 60;
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
        command.CommandTimeout = 60;
        command.CommandText = $"SELECT Id FROM {tableName} WHERE {columnName} = @Value;";
        command.Parameters.AddWithValue("@Value", value);
        return (int)command.ExecuteScalar()!;
    }

    private static bool CanManageAllTickets(string role)
    {
        return role is "Admin" or "Agent" or "Manager";
    }

    private static bool CanEditTicket(int createdByUserId, int currentUserId, string role)
    {
        return createdByUserId == currentUserId || CanManageAllTickets(role);
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
            reader.GetDateTime(12),
            reader.GetDateTime(13));
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
            ua.Id AS CreatedByUserAccountId,
            ua.FullName AS CreatedByName,
            t.CreatedDate,
            t.UpdatedDate
        FROM Ticket t
        INNER JOIN TicketCategory tc ON t.TicketCategoryId = tc.Id
        INNER JOIN TicketPriority tp ON t.TicketPriorityId = tp.Id
        INNER JOIN TicketStatus ts ON t.TicketStatusId = ts.Id
        INNER JOIN UserAccount ua ON t.CreatedByUserAccountId = ua.Id
        """;
}
