using Dapper;
using Microsoft.Data.SqlClient;
using PasTechAI.Domain.Entities;
using PasTechAI.Domain.Interfaces;

namespace PasTechAI.Infrastructure.Repositories;

public class MemoryRepository(string connectionString) : IMemoryRepository
{
    public async Task<List<UserMemory>> GetByUserAsync(string userId)
    {
        using var conn = new SqlConnection(connectionString);
        var rows = await conn.QueryAsync<UserMemory>(
            "SELECT Id, UserId, MemoryType, MemoryValue, UpdatedDate FROM UserMemory WHERE UserId = @UserId ORDER BY UpdatedDate DESC",
            new { UserId = userId });
        return rows.ToList();
    }

    public async Task UpsertAsync(string userId, string memoryType, string memoryValue)
    {
        using var conn = new SqlConnection(connectionString);
        await conn.ExecuteAsync(
            """
            MERGE UserMemory AS t
            USING (SELECT @UserId AS UserId, @MemoryType AS MemoryType) AS s
                ON t.UserId = s.UserId AND t.MemoryType = s.MemoryType
            WHEN MATCHED THEN
                UPDATE SET MemoryValue = @MemoryValue, UpdatedDate = GETDATE()
            WHEN NOT MATCHED THEN
                INSERT (UserId, MemoryType, MemoryValue) VALUES (@UserId, @MemoryType, @MemoryValue);
            """,
            new { UserId = userId, MemoryType = memoryType, MemoryValue = memoryValue });
    }
}
