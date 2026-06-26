namespace PasTechAI.Domain.Entities;

public class ConversationSummary
{
    public int Id { get; set; }
    public string SessionId { get; set; } = "";
    public string UserId { get; set; } = "";
    public string Summary { get; set; } = "";
    public DateTime CreatedDate { get; set; }
}
