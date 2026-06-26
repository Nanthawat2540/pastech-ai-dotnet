using PasTechAI.Domain.Entities;

namespace PasTechAI.Domain.Interfaces;

public interface ISummaryRepository
{
    Task<List<ConversationSummary>> GetRecentByUserAsync(string userId, int limit = 2);
    Task SaveAsync(string sessionId, string userId, string summary);
}
