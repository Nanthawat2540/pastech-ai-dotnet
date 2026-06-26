using PasTechAI.Domain.Entities;

namespace PasTechAI.Domain.Interfaces;

public interface IChatRepository
{
    Task SaveAsync(string sessionId, string userId, string role, string content);
    Task<List<ChatMessage>> GetRecentAsync(string sessionId, int limit = 15);
    Task<int> CountAsync(string sessionId);
    Task<List<ChatMessage>> GetBySessionAsync(string sessionId);
}
