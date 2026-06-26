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
            """);
    }
}
