namespace MajlisManagement.Models;

public enum BookingType
{
    Wedding = 1,     // زواج
    Condolence = 2,  // عزاء
    General = 3      // مناسبة عامة
}

public enum BookingStatus
{
    Pending = 1,    // قيد الانتظار
    Confirmed = 2,  // مؤكد
    Cancelled = 3,  // ملغي
    Completed = 4   // مكتمل
}

public class Booking
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;

    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }

    public BookingType Type { get; set; }
    public BookingStatus Status { get; set; } = BookingStatus.Pending;

    public string GuestName { get; set; } = string.Empty;   // اسم صاحب المناسبة
    public string PhoneNumber { get; set; } = string.Empty;
    public string? Notes { get; set; }

    public decimal Cost { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public string? AdminNote { get; set; } // ملاحظة الإدارة عند الرفض أو التعديل

    public Payment? Payment { get; set; }
}
