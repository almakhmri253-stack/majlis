namespace MajlisManagement.DTOs;

public class CreateEventDto
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public List<string> Media { get; set; } = new(); // base64 data URLs
}

public class UpdateEventDto
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public List<int> DeleteMediaIds { get; set; } = new();
    public List<string> Media { get; set; } = new(); // base64 data URLs for new media
}

public class EventMediaDto
{
    public int Id { get; set; }
    public string MediaUrl { get; set; } = string.Empty;
    public string MediaType { get; set; } = "image";
    public int SortOrder { get; set; }
}

public class EventResponseDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public List<EventMediaDto> Media { get; set; } = new();
    // backward compat for old single-media events
    public string? MediaUrl { get; set; }
    public string MediaType { get; set; } = "none";
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
}
