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
