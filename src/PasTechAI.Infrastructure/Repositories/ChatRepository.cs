using Dapper;
using Microsoft.Data.SqlClient;
using PasTechAI.Domain.Entities;
using PasTechAI.Domain.Interfaces;

namespace PasTechAI.Infrastructure.Repositories;

public class ChatRepository(string connectionString) : IChatRepository
{
    public async Task SaveAsync(string sessionId, string userId, string role, string content)
    {
        using var conn = new SqlConnection(connectionString);
        await conn.ExecuteAsync(
            "INSERT INTO ChatMessage (SessionId, UserId, Role, Content) VALUES (@SessionId, @UserId, @Role, @Content)",
            new { SessionId = sessionId, UserId = userId, Role = role, Content = content });
    }

    public async Task<List<ChatMessage>> GetRecentAsync(string sessionId, int limit = 15)
    {
        using var conn = new SqlConnection(connectionString);
        var rows = await conn.QueryAsync<ChatMessage>(
            """
            SELECT TOP (@Limit) Id, SessionId, UserId, Role, Content, CreatedDate
            FROM ChatMessage
            WHERE SessionId = @SessionId
            ORDER BY Id DESC
            """,
            new { SessionId = sessionId, Limit = limit });
        return rows.Reverse().ToList();
    }

    public async Task<int> CountAsync(string sessionId)
    {
        using var conn = new SqlConnection(connectionString);
        return await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM ChatMessage WHERE SessionId = @SessionId",
            new { SessionId = sessionId });
    }

    public async Task<List<ChatMessage>> GetBySessionAsync(string sessionId)
    {
        using var conn = new SqlConnection(connectionString);
        var rows = await conn.QueryAsync<ChatMessage>(
            "SELECT Id, SessionId, UserId, Role, Content, CreatedDate FROM ChatMessage WHERE SessionId = @SessionId ORDER BY Id",
            new { SessionId = sessionId });
        return rows.ToList();
    }
}
