using Dapper;
using Microsoft.Data.SqlClient;
using PasTechAI.Domain.Entities;
using PasTechAI.Domain.Interfaces;

namespace PasTechAI.Infrastructure.Repositories;

public class SummaryRepository(string connectionString) : ISummaryRepository
{
    public async Task<List<ConversationSummary>> GetRecentByUserAsync(string userId, int limit = 2)
    {
        using var conn = new SqlConnection(connectionString);
        var rows = await conn.QueryAsync<ConversationSummary>(
            """
            SELECT TOP (@Limit) Id, SessionId, UserId, Summary, CreatedDate
            FROM ConversationSummary
            WHERE UserId = @UserId
            ORDER BY CreatedDate DESC
            """,
            new { UserId = userId, Limit = limit });
        return rows.ToList();
    }

    public async Task SaveAsync(string sessionId, string userId, string summary)
    {
        using var conn = new SqlConnection(connectionString);
        await conn.ExecuteAsync(
            "INSERT INTO ConversationSummary (SessionId, UserId, Summary) VALUES (@SessionId, @UserId, @Summary)",
            new { SessionId = sessionId, UserId = userId, Summary = summary });
    }
}
