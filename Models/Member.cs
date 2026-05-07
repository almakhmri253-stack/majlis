namespace MajlisManagement.Models;

public enum MemberStatus
{
    Active = 1,     // نشط
    Inactive = 2,   // غير نشط
    Suspended = 3   // موقوف
}

public class Member
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string? NationalId { get; set; }
    public string? Address { get; set; }

    public MemberStatus Status { get; set; } = MemberStatus.Active;
    public DateTime JoinDate { get; set; } = DateTime.UtcNow;
    public decimal MonthlySubscription { get; set; } = 0;

    public decimal TotalPaymentDue { get; set; } = 0;   // إجمالي مبلغ الدفع المطلوب
    public decimal PaidAmount { get; set; } = 0;          // المبلغ المدفوع فعلاً
    public DateTime? LastPaymentDate { get; set; }         // تاريخ آخر تحديث

    public ICollection<MemberPayment> Payments { get; set; } = new List<MemberPayment>();
}

public class MemberPayment
{
    public int Id { get; set; }
    public int MemberId { get; set; }
    public Member Member { get; set; } = null!;

    public int Year { get; set; }
    public int Month { get; set; }
    public decimal Amount { get; set; }
    public bool IsPaid { get; set; } = false;
    public DateTime? PaidAt { get; set; }
    public string? Note { get; set; }
}
