namespace PasTechAI.Domain.Entities;

public class UserMemory
{
    public int Id { get; set; }
    public string UserId { get; set; } = "";
    public string MemoryType { get; set; } = "";
    public string MemoryValue { get; set; } = "";
    public DateTime UpdatedDate { get; set; }
}
