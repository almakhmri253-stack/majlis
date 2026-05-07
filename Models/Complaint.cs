namespace MajlisManagement.Models;

public enum ComplaintStatus
{
    New = 1,          // جديد
    UnderReview = 2,  // قيد المراجعة
    Resolved = 3      // تم الحل
}

public enum ComplaintType
{
    Complaint   = 0,  // شكوى (default)
    Suggestion  = 1,  // اقتراح
}

public class Complaint
{
    public int Id { get; set; }
    public int? UserId { get; set; }       // null = مجهول
    public User? User { get; set; }

    public ComplaintType Type { get; set; } = ComplaintType.Complaint;
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public bool IsAnonymous { get; set; } = false;

    public ComplaintStatus Status { get; set; } = ComplaintStatus.New;
    public string? AdminResponse { get; set; }
    public bool IsPublic { get; set; } = false;   // نشر للجميع على الشاشة الرئيسية

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? RespondedAt { get; set; }
}
