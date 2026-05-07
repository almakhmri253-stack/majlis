using System.ComponentModel.DataAnnotations;

namespace MajlisManagement.DTOs;

public class UserResponseDto
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public int BookingsCount { get; set; }
}

public class UpdateUserRoleDto
{
    [Required]
    public string Role { get; set; } = string.Empty; // Admin | User
}

public class AdminResetPasswordDto
{
    [Required]
    [MinLength(6)]
    public string NewPassword { get; set; } = string.Empty;
}

public class UpdateUserDto
{
    public string? FullName { get; set; }
    public string? PhoneNumber { get; set; }
}

public class UpdatePermissionsDto
{
    public bool ViewBookings      { get; set; }
    public bool CreateBookings    { get; set; }
    public bool ConfirmBookings   { get; set; }
    public bool DeleteBookings    { get; set; }
    public bool ViewMembers       { get; set; }
    public bool ManageMembers     { get; set; }
    public bool ViewAllComplaints { get; set; }
    public bool RespondComplaints { get; set; }
    public bool ViewDashboard     { get; set; }
    public bool ViewReports       { get; set; }
}
