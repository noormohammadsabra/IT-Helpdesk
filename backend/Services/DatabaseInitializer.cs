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

        Execute(connection, """
            IF OBJECT_ID('TicketCategory', 'U') IS NULL
            BEGIN
                CREATE TABLE TicketCategory
                (
                    Id INT IDENTITY(1,1) PRIMARY KEY,
                    CategoryName NVARCHAR(100) NOT NULL UNIQUE
                );
            END
            """);

        Execute(connection, """
            IF OBJECT_ID('TicketPriority', 'U') IS NULL
            BEGIN
                CREATE TABLE TicketPriority
                (
                    Id INT IDENTITY(1,1) PRIMARY KEY,
                    PriorityName NVARCHAR(50) NOT NULL UNIQUE
                );
            END
            """);

        Execute(connection, """
            IF OBJECT_ID('TicketStatus', 'U') IS NULL
            BEGIN
                CREATE TABLE TicketStatus
                (
                    Id INT IDENTITY(1,1) PRIMARY KEY,
                    StatusName NVARCHAR(50) NOT NULL UNIQUE
                );
            END
            """);

        Execute(connection, """
            IF OBJECT_ID('Ticket', 'U') IS NULL
            BEGIN
                CREATE TABLE Ticket
                (
                    Id INT IDENTITY(1,1) PRIMARY KEY,
                    TicketNumber AS ('HD-' + RIGHT('0000' + CONVERT(VARCHAR(10), Id), 4)) PERSISTED,
                    Title NVARCHAR(200) NOT NULL,
                    Description NVARCHAR(MAX) NOT NULL,
                    CreatedByUserAccountId INT NOT NULL,
                    AssignedToUserAccountId INT NULL,
                    TicketCategoryId INT NOT NULL,
                    TicketPriorityId INT NOT NULL,
                    TicketStatusId INT NOT NULL,
                    CreatedDate DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
                    UpdatedDate DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
                    CONSTRAINT FK_Ticket_UserAccount FOREIGN KEY (CreatedByUserAccountId) REFERENCES UserAccount(Id),
                    CONSTRAINT FK_Ticket_AssignedUserAccount FOREIGN KEY (AssignedToUserAccountId) REFERENCES UserAccount(Id),
                    CONSTRAINT FK_Ticket_TicketCategory FOREIGN KEY (TicketCategoryId) REFERENCES TicketCategory(Id),
                    CONSTRAINT FK_Ticket_TicketPriority FOREIGN KEY (TicketPriorityId) REFERENCES TicketPriority(Id),
                    CONSTRAINT FK_Ticket_TicketStatus FOREIGN KEY (TicketStatusId) REFERENCES TicketStatus(Id)
                );
            END
            """);

        Execute(connection, """
            IF COL_LENGTH('Ticket', 'AssignedToUserAccountId') IS NULL
            BEGIN
                ALTER TABLE Ticket ADD AssignedToUserAccountId INT NULL;
                ALTER TABLE Ticket ADD CONSTRAINT FK_Ticket_AssignedUserAccount
                    FOREIGN KEY (AssignedToUserAccountId) REFERENCES UserAccount(Id);
            END
            """);

        Execute(connection, """
            IF OBJECT_ID('TicketComment', 'U') IS NULL
            BEGIN
                CREATE TABLE TicketComment
                (
                    Id INT IDENTITY(1,1) PRIMARY KEY,
                    TicketId INT NOT NULL,
                    UserAccountId INT NOT NULL,
                    CommentText NVARCHAR(MAX) NOT NULL,
                    IsInternal BIT NOT NULL DEFAULT 0,
                    CreatedDate DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
                    CONSTRAINT FK_TicketComment_Ticket FOREIGN KEY (TicketId) REFERENCES Ticket(Id) ON DELETE CASCADE,
                    CONSTRAINT FK_TicketComment_UserAccount FOREIGN KEY (UserAccountId) REFERENCES UserAccount(Id)
                );
            END
            """);

        Execute(connection, """
            IF OBJECT_ID('ActivityLog', 'U') IS NULL
            BEGIN
                CREATE TABLE ActivityLog
                (
                    Id INT IDENTITY(1,1) PRIMARY KEY,
                    UserAccountId INT NOT NULL,
                    TicketId INT NOT NULL,
                    ActionName NVARCHAR(200) NOT NULL,
                    ActionDetails NVARCHAR(MAX) NOT NULL,
                    CreatedDate DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
                    CONSTRAINT FK_ActivityLog_UserAccount FOREIGN KEY (UserAccountId) REFERENCES UserAccount(Id),
                    CONSTRAINT FK_ActivityLog_Ticket FOREIGN KEY (TicketId) REFERENCES Ticket(Id) ON DELETE CASCADE
                );
            END
            """);

        foreach (var roleName in new[] { "Admin", "Agent", "Manager", "Employee" })
        {
            UpsertRole(connection, roleName);
        }

        foreach (var categoryName in new[] { "Hardware", "Software", "Network", "Email", "Access Request", "Other" })
        {
            UpsertLookup(connection, "TicketCategory", "CategoryName", categoryName);
        }

        foreach (var priorityName in new[] { "Low", "Medium", "High", "Critical" })
        {
            UpsertLookup(connection, "TicketPriority", "PriorityName", priorityName);
        }

        foreach (var statusName in new[] { "Open", "In Progress", "Pending", "Resolved", "Closed" })
        {
            UpsertLookup(connection, "TicketStatus", "StatusName", statusName);
        }

        SeedUser(connection, "Admin User", "admin@ids.com", "Admin", "IT Administration");
        SeedUser(connection, "Support Agent", "agent@ids.com", "Agent", "IT Support");
        SeedUser(connection, "Support Manager", "manager@ids.com", "Manager", "IT Management");
        SeedUser(connection, "Employee User", "employee@ids.com", "Employee", "Operations");
        SeedTicket(connection, "Email access issue", "User cannot send or receive company emails.", "Email", "High", "Open", "employee@ids.com");
        SeedTicket(connection, "Printer not responding", "The office printer is not responding from employee laptops.", "Hardware", "Medium", "In Progress", "employee@ids.com");

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

    private static void UpsertLookup(SqlConnection connection, string tableName, string columnName, string value)
    {
        using var command = connection.CreateCommand();
        command.CommandTimeout = 120;
        command.CommandText = $"""
            IF NOT EXISTS (SELECT 1 FROM {tableName} WHERE {columnName} = @Value)
            BEGIN
                INSERT INTO {tableName} ({columnName}) VALUES (@Value);
            END
            """;
        command.Parameters.AddWithValue("@Value", value);
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

    private static void SeedTicket(
        SqlConnection connection,
        string title,
        string description,
        string categoryName,
        string priorityName,
        string statusName,
        string creatorEmail)
    {
        using var command = connection.CreateCommand();
        command.CommandTimeout = 120;
        command.CommandText = """
            IF NOT EXISTS (SELECT 1 FROM Ticket WHERE Title = @Title)
            BEGIN
                INSERT INTO Ticket
                (
                    Title,
                    Description,
                    CreatedByUserAccountId,
                    TicketCategoryId,
                    TicketPriorityId,
                    TicketStatusId
                )
                SELECT
                    @Title,
                    @Description,
                    ua.Id,
                    tc.Id,
                    tp.Id,
                    ts.Id
                FROM UserAccount ua
                CROSS JOIN TicketCategory tc
                CROSS JOIN TicketPriority tp
                CROSS JOIN TicketStatus ts
                WHERE ua.Email = @CreatorEmail
                  AND tc.CategoryName = @CategoryName
                  AND tp.PriorityName = @PriorityName
                  AND ts.StatusName = @StatusName;
            END
            """;
        command.Parameters.AddWithValue("@Title", title);
        command.Parameters.AddWithValue("@Description", description);
        command.Parameters.AddWithValue("@CreatorEmail", creatorEmail);
        command.Parameters.AddWithValue("@CategoryName", categoryName);
        command.Parameters.AddWithValue("@PriorityName", priorityName);
        command.Parameters.AddWithValue("@StatusName", statusName);
        command.ExecuteNonQuery();
    }
}
