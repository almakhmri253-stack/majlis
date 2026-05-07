namespace MajlisManagement.Models;

public class EventMedia
{
    public int Id { get; set; }
    public int EventId { get; set; }
    public MajlisEvent Event { get; set; } = null!;
    public string MediaPath { get; set; } = string.Empty;
    public string MediaType { get; set; } = "image"; // "image" | "video"
    public int SortOrder { get; set; }
}
