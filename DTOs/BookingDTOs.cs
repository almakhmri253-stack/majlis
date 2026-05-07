using System.ComponentModel.DataAnnotations;
using MajlisManagement.Models;

namespace MajlisManagement.DTOs;

public class CreateBookingDto
{
    [Required(ErrorMessage = "اسم صاحب المناسبة مطلوب")]
    [MaxLength(100)]
    public string GuestName { get; set; } = string.Empty;

    [Required(ErrorMessage = "رقم الجوال مطلوب")]
    [MaxLength(20)]
    public string PhoneNumber { get; set; } = string.Empty;

    [Required(ErrorMessage = "تاريخ البداية مطلوب")]
    public DateTime StartDate { get; set; }

    [Required(ErrorMessage = "تاريخ النهاية مطلوب")]
    public DateTime EndDate { get; set; }

    [Required(ErrorMessage = "نوع المناسبة مطلوب")]
    public BookingType Type { get; set; }

    public string? Notes { get; set; }

    [Range(0, double.MaxValue, ErrorMessage = "التكلفة يجب أن تكون قيمة موجبة")]
    public decimal Cost { get; set; }
}

public class UpdateBookingDto
{
    public string? GuestName { get; set; }
    public string? PhoneNumber { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public BookingType? Type { get; set; }
    public string? Notes { get; set; }
    public decimal? Cost { get; set; }
}

public class AdminUpdateBookingDto
{
    public BookingStatus? Status { get; set; }
    public string? AdminNote { get; set; }
    public decimal? Cost { get; set; }
}

public class BookingResponseDto
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string GuestName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int DurationDays { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public string? AdminNote { get; set; }
    public decimal Cost { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class BookingCalendarDto
{
    public int Id { get; set; }
    public string GuestName { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

public class BookingFilterDto
{
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public BookingType? Type { get; set; }
    public BookingStatus? Status { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
