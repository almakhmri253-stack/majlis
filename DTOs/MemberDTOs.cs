using System.ComponentModel.DataAnnotations;
using MajlisManagement.Models;

namespace MajlisManagement.DTOs;

public class CreateMemberDto
{
    [Required(ErrorMessage = "الاسم الكامل مطلوب")]
    [MaxLength(100)]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "رقم الجوال مطلوب")]
    [MaxLength(20)]
    public string PhoneNumber { get; set; } = string.Empty;

    [MaxLength(20)]
    public string? NationalId { get; set; }

    public string? Address { get; set; }

    [Range(0, double.MaxValue)]
    public decimal MonthlySubscription { get; set; } = 0;

    [Range(0, double.MaxValue)]
    public decimal TotalPaymentDue { get; set; } = 0;

    [Range(0, double.MaxValue)]
    public decimal PaidAmount { get; set; } = 0;

    public DateTime? LastPaymentDate { get; set; }
}

public class UpdateMemberDto
{
    public string? FullName { get; set; }
    public string? PhoneNumber { get; set; }
    public string? NationalId { get; set; }
    public string? Address { get; set; }
    public MemberStatus? Status { get; set; }
    public decimal? MonthlySubscription { get; set; }
    public decimal? TotalPaymentDue { get; set; }
    public decimal? PaidAmount { get; set; }
    public DateTime? LastPaymentDate { get; set; }
}

public class AddMemberPaymentDto
{
    [Required]
    public int Year { get; set; }

    [Required]
    [Range(1, 12)]
    public int Month { get; set; }

    [Required]
    [Range(0.01, double.MaxValue)]
    public decimal Amount { get; set; }

    public string? Note { get; set; }
}

public class MemberResponseDto
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string? NationalId { get; set; }
    public string? Address { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime JoinDate { get; set; }
    public decimal MonthlySubscription { get; set; }
    public decimal TotalPaymentDue { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal OverdueAmount { get; set; }   // = TotalPaymentDue - PaidAmount
    public DateTime? LastPaymentDate { get; set; }
    public int UnpaidMonths { get; set; }         // للتوافق مع الكود القديم
    public decimal TotalDebt { get; set; }         // = OverdueAmount
}

public class MemberPaymentResponseDto
{
    public int Id { get; set; }
    public int MemberId { get; set; }
    public string MemberName { get; set; } = string.Empty;
    public int Year { get; set; }
    public int Month { get; set; }
    public decimal Amount { get; set; }
    public bool IsPaid { get; set; }
    public DateTime? PaidAt { get; set; }
    public string? Note { get; set; }
}
