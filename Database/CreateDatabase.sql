-- ============================================================
-- مجلس المخامرة الشرقي - سكريبت إنشاء قاعدة البيانات
-- Server: .\SQLEXPRESS
-- ============================================================

USE master;
GO

IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = 'MajlisDB')
    CREATE DATABASE MajlisDB;
GO

USE MajlisDB;
GO

-- ── Users ────────────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Users' AND xtype='U')
CREATE TABLE Users (
    Id            INT IDENTITY(1,1) PRIMARY KEY,
    FullName      NVARCHAR(100)  NOT NULL,
    PhoneNumber   NVARCHAR(20)   NOT NULL,
    Email         NVARCHAR(150)  NOT NULL,
    PasswordHash  NVARCHAR(MAX)  NOT NULL,
    Role          NVARCHAR(20)   NOT NULL DEFAULT 'User',
    IsActive      BIT            NOT NULL DEFAULT 1,
    CreatedAt     DATETIME2      NOT NULL DEFAULT GETUTCDATE()
);
GO

CREATE UNIQUE INDEX IX_Users_Email        ON Users(Email);
CREATE UNIQUE INDEX IX_Users_PhoneNumber  ON Users(PhoneNumber);
GO

-- ── Bookings ─────────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Bookings' AND xtype='U')
CREATE TABLE Bookings (
    Id          INT IDENTITY(1,1) PRIMARY KEY,
    UserId      INT            NOT NULL REFERENCES Users(Id),
    GuestName   NVARCHAR(100)  NOT NULL,
    PhoneNumber NVARCHAR(20)   NOT NULL,
    StartDate   DATETIME2      NOT NULL,
    EndDate     DATETIME2      NOT NULL,
    -- 1=Wedding  2=Condolence  3=General
    Type        INT            NOT NULL,
    -- 1=Pending  2=Confirmed  3=Cancelled  4=Completed
    Status      INT            NOT NULL DEFAULT 1,
    Notes       NVARCHAR(500),
    Cost        DECIMAL(10,2)  NOT NULL DEFAULT 0,
    AdminNote   NVARCHAR(500),
    CreatedAt   DATETIME2      NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt   DATETIME2
);
GO

CREATE INDEX IX_Bookings_StartDate_EndDate_Status ON Bookings(StartDate, EndDate, Status);
GO

-- ── Payments ─────────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Payments' AND xtype='U')
CREATE TABLE Payments (
    Id              INT IDENTITY(1,1) PRIMARY KEY,
    BookingId       INT            NOT NULL UNIQUE REFERENCES Bookings(Id) ON DELETE CASCADE,
    TotalAmount     DECIMAL(10,2)  NOT NULL,
    PaidAmount      DECIMAL(10,2)  NOT NULL DEFAULT 0,
    -- 1=Unpaid  2=Partial  3=Paid
    Status          INT            NOT NULL DEFAULT 1,
    CreatedAt       DATETIME2      NOT NULL DEFAULT GETUTCDATE(),
    LastPaymentDate DATETIME2,
    Notes           NVARCHAR(MAX)
);
GO

-- ── PaymentTransactions ───────────────────────────────────────────────────────
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='PaymentTransactions' AND xtype='U')
CREATE TABLE PaymentTransactions (
    Id        INT IDENTITY(1,1) PRIMARY KEY,
    PaymentId INT            NOT NULL REFERENCES Payments(Id) ON DELETE CASCADE,
    Amount    DECIMAL(10,2)  NOT NULL,
    PaidAt    DATETIME2      NOT NULL DEFAULT GETUTCDATE(),
    Note      NVARCHAR(MAX)
);
GO

-- ── Complaints ────────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Complaints' AND xtype='U')
CREATE TABLE Complaints (
    Id            INT IDENTITY(1,1) PRIMARY KEY,
    UserId        INT            REFERENCES Users(Id) ON DELETE SET NULL, -- null = مجهول
    Title         NVARCHAR(200)  NOT NULL,
    Content       NVARCHAR(2000) NOT NULL,
    IsAnonymous   BIT            NOT NULL DEFAULT 0,
    -- 1=New  2=UnderReview  3=Resolved
    Status        INT            NOT NULL DEFAULT 1,
    AdminResponse NVARCHAR(MAX),
    CreatedAt     DATETIME2      NOT NULL DEFAULT GETUTCDATE(),
    RespondedAt   DATETIME2
);
GO

-- ── Members ───────────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Members' AND xtype='U')
CREATE TABLE Members (
    Id                  INT IDENTITY(1,1) PRIMARY KEY,
    FullName            NVARCHAR(100)  NOT NULL,
    PhoneNumber         NVARCHAR(20)   NOT NULL,
    NationalId          NVARCHAR(20),
    Address             NVARCHAR(MAX),
    -- 1=Active  2=Inactive  3=Suspended
    Status              INT            NOT NULL DEFAULT 1,
    JoinDate            DATETIME2      NOT NULL DEFAULT GETUTCDATE(),
    MonthlySubscription DECIMAL(10,2)  NOT NULL DEFAULT 0
);
GO

CREATE UNIQUE INDEX IX_Members_PhoneNumber ON Members(PhoneNumber);
GO

-- ── MemberPayments ────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='MemberPayments' AND xtype='U')
CREATE TABLE MemberPayments (
    Id       INT IDENTITY(1,1) PRIMARY KEY,
    MemberId INT           NOT NULL REFERENCES Members(Id) ON DELETE CASCADE,
    Year     INT           NOT NULL,
    Month    INT           NOT NULL CHECK (Month BETWEEN 1 AND 12),
    Amount   DECIMAL(10,2) NOT NULL,
    IsPaid   BIT           NOT NULL DEFAULT 0,
    PaidAt   DATETIME2,
    Note     NVARCHAR(MAX),
    CONSTRAINT UQ_MemberPayment UNIQUE (MemberId, Year, Month)
);
GO

-- ── __EFMigrationsHistory (مطلوب لـ EF Core) ─────────────────────────────────
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='__EFMigrationsHistory' AND xtype='U')
CREATE TABLE __EFMigrationsHistory (
    MigrationId    NVARCHAR(150) NOT NULL PRIMARY KEY,
    ProductVersion NVARCHAR(32)  NOT NULL
);
GO

INSERT INTO __EFMigrationsHistory VALUES ('20240101000000_InitialCreate', '8.0.0');
GO

PRINT 'تم إنشاء قاعدة البيانات بنجاح';
GO
