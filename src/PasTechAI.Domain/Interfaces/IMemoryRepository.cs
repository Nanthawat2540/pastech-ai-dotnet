using PasTechAI.Domain.Entities;

namespace PasTechAI.Domain.Interfaces;

public interface IMemoryRepository
{
    Task<List<UserMemory>> GetByUserAsync(string userId);
    Task UpsertAsync(string userId, string memoryType, string memoryValue);
}
