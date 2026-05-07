namespace MajlisManagement.Models;

public class MajlisEvent
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? MediaPath { get; set; }
    public string MediaType { get; set; } = "none"; // "image" | "video" | "none"
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int CreatedByUserId { get; set; }
    public User? CreatedBy { get; set; }
    public ICollection<EventMedia> Media { get; set; } = new List<EventMedia>();
}
