IF DB_ID('ITHelpDesk') IS NULL
BEGIN
    CREATE DATABASE ITHelpDesk;
END
GO

USE ITHelpDesk;
GO

IF OBJECT_ID('Role', 'U') IS NULL
BEGIN
    CREATE TABLE Role
    (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        RoleName NVARCHAR(50) NOT NULL UNIQUE
    );
END
GO

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
GO

INSERT INTO Role (RoleName)
SELECT RoleName
FROM (VALUES ('Admin'), ('Agent'), ('Manager'), ('Employee')) AS Roles(RoleName)
WHERE NOT EXISTS
(
    SELECT 1
    FROM Role
    WHERE Role.RoleName = Roles.RoleName
);
GO

IF OBJECT_ID('TicketCategory', 'U') IS NULL
BEGIN
    CREATE TABLE TicketCategory
    (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        CategoryName NVARCHAR(100) NOT NULL UNIQUE
    );
END
GO

IF OBJECT_ID('TicketPriority', 'U') IS NULL
BEGIN
    CREATE TABLE TicketPriority
    (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        PriorityName NVARCHAR(50) NOT NULL UNIQUE
    );
END
GO

IF OBJECT_ID('TicketStatus', 'U') IS NULL
BEGIN
    CREATE TABLE TicketStatus
    (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        StatusName NVARCHAR(50) NOT NULL UNIQUE
    );
END
GO

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
GO

IF COL_LENGTH('Ticket', 'AssignedToUserAccountId') IS NULL
BEGIN
    ALTER TABLE Ticket ADD AssignedToUserAccountId INT NULL;
    ALTER TABLE Ticket ADD CONSTRAINT FK_Ticket_AssignedUserAccount
        FOREIGN KEY (AssignedToUserAccountId) REFERENCES UserAccount(Id);
END
GO

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
GO

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
GO

IF OBJECT_ID('TicketAttachment', 'U') IS NULL
BEGIN
    CREATE TABLE TicketAttachment
    (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        TicketId INT NOT NULL,
        UploadedByUserAccountId INT NOT NULL,
        FileName NVARCHAR(255) NOT NULL,
        StoredFileName NVARCHAR(255) NOT NULL,
        FilePath NVARCHAR(500) NOT NULL,
        ContentType NVARCHAR(150) NOT NULL,
        FileSizeBytes BIGINT NOT NULL,
        UploadedDate DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_TicketAttachment_Ticket FOREIGN KEY (TicketId) REFERENCES Ticket(Id) ON DELETE CASCADE,
        CONSTRAINT FK_TicketAttachment_UserAccount FOREIGN KEY (UploadedByUserAccountId) REFERENCES UserAccount(Id)
    );
END
GO

IF OBJECT_ID('Notification', 'U') IS NULL
BEGIN
    CREATE TABLE Notification
    (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        UserAccountId INT NOT NULL,
        TicketId INT NULL,
        Title NVARCHAR(200) NOT NULL,
        Message NVARCHAR(MAX) NOT NULL,
        IsRead BIT NOT NULL DEFAULT 0,
        CreatedDate DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_Notification_UserAccount FOREIGN KEY (UserAccountId) REFERENCES UserAccount(Id),
        CONSTRAINT FK_Notification_Ticket FOREIGN KEY (TicketId) REFERENCES Ticket(Id) ON DELETE CASCADE
    );
END
GO

INSERT INTO TicketCategory (CategoryName)
SELECT CategoryName
FROM (VALUES ('Hardware'), ('Software'), ('Network'), ('Email'), ('Access Request'), ('Other')) AS Categories(CategoryName)
WHERE NOT EXISTS
(
    SELECT 1
    FROM TicketCategory
    WHERE TicketCategory.CategoryName = Categories.CategoryName
);
GO

INSERT INTO TicketPriority (PriorityName)
SELECT PriorityName
FROM (VALUES ('Low'), ('Medium'), ('High'), ('Critical')) AS Priorities(PriorityName)
WHERE NOT EXISTS
(
    SELECT 1
    FROM TicketPriority
    WHERE TicketPriority.PriorityName = Priorities.PriorityName
);
GO

INSERT INTO TicketStatus (StatusName)
SELECT StatusName
FROM (VALUES ('Open'), ('In Progress'), ('Pending'), ('Resolved'), ('Closed')) AS Statuses(StatusName)
WHERE NOT EXISTS
(
    SELECT 1
    FROM TicketStatus
    WHERE TicketStatus.StatusName = Statuses.StatusName
);
GO
