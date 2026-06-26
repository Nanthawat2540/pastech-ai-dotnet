namespace PasTechAI.Domain.Entities;

public class ChatMessage
{
    public int Id { get; set; }
    public string SessionId { get; set; } = "";
    public string UserId { get; set; } = "";
    public string Role { get; set; } = "";
    public string Content { get; set; } = "";
    public DateTime CreatedDate { get; set; }
}
