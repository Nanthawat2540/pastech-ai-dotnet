namespace PasTechAI.Domain.Entities;

public class Room
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Location { get; set; } = "";
    public int Capacity { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
}

public class RoomBooking
{
    public int Id { get; set; }
    public int RoomId { get; set; }
    public string RoomName { get; set; } = "";
    public string UserId { get; set; } = "";
    public string Title { get; set; } = "";
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public string Status { get; set; } = "confirmed";
    public DateTime CreatedAt { get; set; }
}
