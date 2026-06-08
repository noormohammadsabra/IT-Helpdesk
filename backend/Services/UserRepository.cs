using HelpDesk.Api.Models;
using Microsoft.Data.SqlClient;

namespace HelpDesk.Api.Services;

public sealed class UserRepository
{
    private readonly string _connectionString;

    public UserRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("DefaultConnection is missing.");
    }

    public async Task<AppUser?> GetByEmailAsync(string email)
    {
        using var connection = new SqlConnection(_connectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandTimeout = 60;
        command.CommandText = """
            SELECT ua.Id, ua.FullName, ua.Email, ua.PasswordHash, r.RoleName, ua.Department, ua.IsActive
            FROM UserAccount ua
            INNER JOIN Role r ON ua.RoleId = r.Id
            WHERE ua.Email = @Email AND ua.IsActive = 1;
            """;
        command.Parameters.AddWithValue("@Email", email);

        using var reader = command.ExecuteReader();

        if (!reader.Read())
        {
            return null;
        }

        return await Task.FromResult(ReadUser(reader));
    }

    public async Task<IReadOnlyList<AppUser>> GetAllAsync()
    {
        var users = new List<AppUser>();

        using var connection = new SqlConnection(_connectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandTimeout = 60;
        command.CommandText = """
            SELECT ua.Id, ua.FullName, ua.Email, ua.PasswordHash, r.RoleName, ua.Department, ua.IsActive
            FROM UserAccount ua
            INNER JOIN Role r ON ua.RoleId = r.Id
            ORDER BY ua.Id;
            """;

        using var reader = command.ExecuteReader();

        while (reader.Read())
        {
            users.Add(ReadUser(reader));
        }

        return await Task.FromResult(users);
    }

    public async Task<AppUser> CreateAsync(RegisterRequest request, string passwordHash)
    {
        using var connection = new SqlConnection(_connectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandTimeout = 60;
        command.CommandText = """
            INSERT INTO UserAccount (FullName, Email, PasswordHash, RoleId, Department, IsActive)
            OUTPUT INSERTED.Id
            SELECT @FullName, @Email, @PasswordHash, Id, @Department, 1
            FROM Role
            WHERE RoleName = @RoleName;
            """;
        command.Parameters.AddWithValue("@FullName", request.FullName);
        command.Parameters.AddWithValue("@Email", request.Email);
        command.Parameters.AddWithValue("@PasswordHash", passwordHash);
        command.Parameters.AddWithValue("@RoleName", request.RoleName);
        command.Parameters.AddWithValue("@Department", request.Department);

        var createdId = (int?)command.ExecuteScalar();

        if (createdId is null)
        {
            throw new InvalidOperationException("The selected role does not exist.");
        }

        return await Task.FromResult(new AppUser(createdId.Value, request.FullName, request.Email, passwordHash, request.RoleName, request.Department, true));
    }

    private static AppUser ReadUser(SqlDataReader reader)
    {
        return new AppUser(
            reader.GetInt32(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetString(4),
            reader.GetString(5),
            reader.GetBoolean(6));
    }
}
