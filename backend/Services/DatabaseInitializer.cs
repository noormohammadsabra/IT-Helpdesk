using Microsoft.Data.SqlClient;

namespace HelpDesk.Api.Services;

public sealed class DatabaseInitializer
{
    private readonly IConfiguration _configuration;
    private readonly PasswordService _passwordService;

    public DatabaseInitializer(IConfiguration configuration, PasswordService passwordService)
    {
        _configuration = configuration;
        _passwordService = passwordService;
    }

    public async Task InitializeAsync()
    {
        var masterConnectionString = _configuration.GetConnectionString("MasterConnection")
            ?? throw new InvalidOperationException("MasterConnection is missing.");
        var defaultConnectionString = _configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("DefaultConnection is missing.");

        using (var masterConnection = new SqlConnection(masterConnectionString))
        {
            masterConnection.Open();
            using var createDatabaseCommand = masterConnection.CreateCommand();
            createDatabaseCommand.CommandTimeout = 300;
            createDatabaseCommand.CommandText = """
                IF DB_ID('ITHelpDesk') IS NULL
                BEGIN
                    CREATE DATABASE ITHelpDesk;
                END
                """;
            createDatabaseCommand.ExecuteNonQuery();
        }

        using var connection = new SqlConnection(defaultConnectionString);
        connection.Open();

        Execute(connection, """
            IF OBJECT_ID('Role', 'U') IS NULL
            BEGIN
                CREATE TABLE Role
                (
                    Id INT IDENTITY(1,1) PRIMARY KEY,
                    RoleName NVARCHAR(50) NOT NULL UNIQUE
                );
            END
            """);

        Execute(connection, """
            IF OBJECT_ID('UserAccount', 'U') IS NULL
            BEGIN
                CREATE TABLE UserAccount
                (
                    Id INT IDENTITY(1,1) PRIMARY KEY,
                    FullName NVARCHAR(100) NOT NULL,
                    Email NVARCHAR(150) NOT NULL UNIQUE,
                    PasswordHash NVARCHAR(MAX) NOT NULL,
                    RoleId INT NOT NULL,
                    Department NVARCHAR(100) NOT NULL,
                    IsActive BIT NOT NULL DEFAULT 1,
                    CreatedDate DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
                    CONSTRAINT FK_UserAccount_Role FOREIGN KEY (RoleId) REFERENCES Role(Id)
                );
            END
            """);

        foreach (var roleName in new[] { "Admin", "Agent", "Manager", "Employee" })
        {
            UpsertRole(connection, roleName);
        }

        SeedUser(connection, "Admin User", "admin@ids.com", "Admin", "IT Administration");
        SeedUser(connection, "Support Agent", "agent@ids.com", "Agent", "IT Support");
        SeedUser(connection, "Support Manager", "manager@ids.com", "Manager", "IT Management");
        SeedUser(connection, "Employee User", "employee@ids.com", "Employee", "Operations");

        await Task.CompletedTask;
    }

    private static void Execute(SqlConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandTimeout = 120;
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private static void UpsertRole(SqlConnection connection, string roleName)
    {
        using var command = connection.CreateCommand();
        command.CommandTimeout = 120;
        command.CommandText = """
            IF NOT EXISTS (SELECT 1 FROM Role WHERE RoleName = @RoleName)
            BEGIN
                INSERT INTO Role (RoleName) VALUES (@RoleName);
            END
            """;
        command.Parameters.AddWithValue("@RoleName", roleName);
        command.ExecuteNonQuery();
    }

    private void SeedUser(SqlConnection connection, string fullName, string email, string roleName, string department)
    {
        using var command = connection.CreateCommand();
        command.CommandTimeout = 120;
        command.CommandText = """
            IF NOT EXISTS (SELECT 1 FROM UserAccount WHERE Email = @Email)
            BEGIN
                INSERT INTO UserAccount (FullName, Email, PasswordHash, RoleId, Department, IsActive)
                SELECT @FullName, @Email, @PasswordHash, Id, @Department, 1
                FROM Role
                WHERE RoleName = @RoleName;
            END
            """;
        command.Parameters.AddWithValue("@FullName", fullName);
        command.Parameters.AddWithValue("@Email", email);
        command.Parameters.AddWithValue("@PasswordHash", _passwordService.HashPassword("Password123!"));
        command.Parameters.AddWithValue("@RoleName", roleName);
        command.Parameters.AddWithValue("@Department", department);
        command.ExecuteNonQuery();
    }
}
