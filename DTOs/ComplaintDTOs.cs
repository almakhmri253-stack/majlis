using System.ComponentModel.DataAnnotations;
using MajlisManagement.Models;

namespace MajlisManagement.DTOs;

public class CreateComplaintDto
{
    public string Type { get; set; } = "Complaint"; // Complaint | Suggestion

    [Required(ErrorMessage = "عنوان الملاحظة مطلوب")]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "محتوى الملاحظة مطلوب")]
    [MaxLength(2000)]
    public string Content { get; set; } = string.Empty;

    public bool IsAnonymous { get; set; } = false;
}

public class RespondComplaintDto
{
    [Required(ErrorMessage = "الرد مطلوب")]
    [MaxLength(2000)]
    public string AdminResponse { get; set; } = string.Empty;

    public ComplaintStatus Status { get; set; } = ComplaintStatus.Resolved;
}

public class ComplaintResponseDto
{
    public int Id { get; set; }
    public string? SenderName { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public bool IsAnonymous { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? AdminResponse { get; set; }
    public bool IsPublic { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? RespondedAt { get; set; }
}
