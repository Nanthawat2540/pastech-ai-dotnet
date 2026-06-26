using Dapper;
using Microsoft.Data.SqlClient;

namespace PasTechAI.Infrastructure.Data;

public class DbInitializer(string connectionString)
{
    public async Task InitializeAsync()
    {
        using var conn = new SqlConnection(connectionString);
        await conn.ExecuteAsync("""
            IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='ChatMessage' AND xtype='U')
            CREATE TABLE ChatMessage (
                Id          INT IDENTITY PRIMARY KEY,
                SessionId   NVARCHAR(50)  NOT NULL,
                UserId      NVARCHAR(100) NOT NULL,
                Role        NVARCHAR(20)  NOT NULL,
                Content     NVARCHAR(MAX) NOT NULL,
                CreatedDate DATETIME DEFAULT GETDATE()
            );
            IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name='IX_ChatMessage_Session')
                CREATE INDEX IX_ChatMessage_Session ON ChatMessage(SessionId);
            IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name='IX_ChatMessage_User')
                CREATE INDEX IX_ChatMessage_User ON ChatMessage(UserId);

            IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='UserMemory' AND xtype='U')
            CREATE TABLE UserMemory (
                Id          INT IDENTITY PRIMARY KEY,
                UserId      NVARCHAR(100) NOT NULL,
                MemoryType  NVARCHAR(100) NOT NULL,
                MemoryValue NVARCHAR(1000) NOT NULL,
                UpdatedDate DATETIME DEFAULT GETDATE(),
                CONSTRAINT UQ_UserMemory UNIQUE (UserId, MemoryType)
            );

            IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='ConversationSummary' AND xtype='U')
            CREATE TABLE ConversationSummary (
                Id          INT IDENTITY PRIMARY KEY,
                SessionId   NVARCHAR(50)  NOT NULL,
                UserId      NVARCHAR(100) NOT NULL,
                Summary     NVARCHAR(MAX) NOT NULL,
                CreatedDate DATETIME DEFAULT GETDATE()
            );
            IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name='IX_Summary_User')
                CREATE INDEX IX_Summary_User ON ConversationSummary(UserId);

            IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Rooms' AND xtype='U')
            BEGIN
                CREATE TABLE Rooms (
                    Id          INT IDENTITY PRIMARY KEY,
                    Name        NVARCHAR(100) NOT NULL,
                    Location    NVARCHAR(200) NOT NULL,
                    Capacity    INT NOT NULL,
                    Description NVARCHAR(500) NULL,
                    IsActive    BIT NOT NULL DEFAULT 1
                );
                INSERT INTO Rooms (Name, Location, Capacity, Description) VALUES
                    (N'ห้องประชุม A', N'ชั้น 2', 10, N'ห้องประชุมขนาดกลาง'),
                    (N'ห้องประชุม B', N'ชั้น 2', 20, N'ห้องประชุมขนาดใหญ่'),
                    (N'ห้องประชุม C', N'ชั้น 3', 6, N'ห้องประชุมขนาดเล็ก VIP'),
                    (N'ห้องอบรม', N'ชั้น 1', 50, N'ห้องสัมมนา/อบรม');
            END;

            IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='RoomBookings' AND xtype='U')
                CREATE TABLE RoomBookings (
                    Id        INT IDENTITY PRIMARY KEY,
                    RoomId    INT NOT NULL,
                    RoomName  NVARCHAR(100) NOT NULL,
                    UserId    NVARCHAR(100) NOT NULL,
                    Title     NVARCHAR(200) NOT NULL,
                    StartTime DATETIME NOT NULL,
                    EndTime   DATETIME NOT NULL,
                    Status    NVARCHAR(20) NOT NULL DEFAULT 'confirmed',
                    CreatedAt DATETIME NOT NULL DEFAULT GETDATE()
                );
            IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name='IX_RoomBookings_Room')
                CREATE INDEX IX_RoomBookings_Room ON RoomBookings(RoomId, StartTime, EndTime);
            IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name='IX_RoomBookings_User')
                CREATE INDEX IX_RoomBookings_User ON RoomBookings(UserId, StartTime);
            """);
    }
}
