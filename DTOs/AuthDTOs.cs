using System.ComponentModel.DataAnnotations;

namespace MajlisManagement.DTOs;

public class RegisterDto
{
    [Required(ErrorMessage = "الاسم الكامل مطلوب")]
    [MaxLength(100)]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "رقم الجوال مطلوب")]
    [MaxLength(20)]
    public string PhoneNumber { get; set; } = string.Empty;

    [Required(ErrorMessage = "البريد الإلكتروني مطلوب")]
    [EmailAddress(ErrorMessage = "البريد الإلكتروني غير صالح")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "كلمة المرور مطلوبة")]
    [MinLength(8, ErrorMessage = "كلمة المرور يجب أن تكون 8 أحرف على الأقل")]
    [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).{8,}$",
        ErrorMessage = "كلمة المرور يجب أن تحتوي على حرف كبير وحرف صغير ورقم على الأقل")]
    public string Password { get; set; } = string.Empty;
}

public class LoginDto
{
    [Required(ErrorMessage = "البريد الإلكتروني مطلوب")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "كلمة المرور مطلوبة")]
    public string Password { get; set; } = string.Empty;
}

public class AuthResponseDto
{
    public string Token { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public PermissionsDto Permissions { get; set; } = new();
}

public class PermissionsDto
{
    public bool ViewBookings    { get; set; }
    public bool CreateBookings  { get; set; }
    public bool ConfirmBookings { get; set; }
    public bool DeleteBookings  { get; set; }
    public bool ViewMembers     { get; set; }
    public bool ManageMembers   { get; set; }
    public bool ViewAllComplaints { get; set; }
    public bool RespondComplaints { get; set; }
    public bool ViewDashboard   { get; set; }
    public bool ViewReports     { get; set; }

    public static PermissionsDto AdminAll() => new()
    {
        ViewBookings = true, CreateBookings = true, ConfirmBookings = true, DeleteBookings = true,
        ViewMembers  = true, ManageMembers  = true,
        ViewAllComplaints = true, RespondComplaints = true,
        ViewDashboard = true, ViewReports = true
    };
}

public class ChangePasswordDto
{
    [Required]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required]
    [MinLength(8, ErrorMessage = "كلمة المرور يجب أن تكون 8 أحرف على الأقل")]
    [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).{8,}$",
        ErrorMessage = "كلمة المرور يجب أن تحتوي على حرف كبير وحرف صغير ورقم على الأقل")]
    public string NewPassword { get; set; } = string.Empty;
}
