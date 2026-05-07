using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MajlisManagement.Data;
using MajlisManagement.DTOs;

namespace MajlisManagement.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class UsersController : ControllerBase
{
    private readonly AppDbContext _db;

    public UsersController(AppDbContext db) => _db = db;

    /// <summary>قائمة جميع المستخدمين</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var users = await _db.Users
            .Select(u => new UserResponseDto
            {
                Id            = u.Id,
                FullName      = u.FullName,
                Email         = u.Email,
                PhoneNumber   = u.PhoneNumber,
                Role          = u.Role,
                IsActive      = u.IsActive,
                CreatedAt     = u.CreatedAt,
                BookingsCount = u.Bookings.Count
            })
            .OrderByDescending(u => u.CreatedAt)
            .ToListAsync();

        return Ok(users);
    }

    /// <summary>تفاصيل مستخدم</summary>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var u = await _db.Users
            .Where(u => u.Id == id)
            .Select(u => new UserResponseDto
            {
                Id            = u.Id,
                FullName      = u.FullName,
                Email         = u.Email,
                PhoneNumber   = u.PhoneNumber,
                Role          = u.Role,
                IsActive      = u.IsActive,
                CreatedAt     = u.CreatedAt,
                BookingsCount = u.Bookings.Count
            })
            .FirstOrDefaultAsync()
            ?? throw new KeyNotFoundException("المستخدم غير موجود");

        return Ok(u);
    }

    /// <summary>تغيير دور المستخدم</summary>
    [HttpPatch("{id:int}/role")]
    public async Task<IActionResult> ChangeRole(int id, [FromBody] UpdateUserRoleDto dto)
    {
        var currentUserId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        if (id == currentUserId)
            throw new InvalidOperationException("لا يمكنك تغيير دورك بنفسك");

        if (dto.Role != "Admin" && dto.Role != "User")
            throw new InvalidOperationException("الدور غير صحيح، يجب أن يكون Admin أو User");

        var user = await _db.Users.FindAsync(id)
            ?? throw new KeyNotFoundException("المستخدم غير موجود");

        user.Role = dto.Role;
        await _db.SaveChangesAsync();

        return Ok(new { message = $"تم تغيير دور {user.FullName} إلى {dto.Role}" });
    }

    /// <summary>تفعيل أو إيقاف مستخدم</summary>
    [HttpPatch("{id:int}/toggle")]
    public async Task<IActionResult> Toggle(int id)
    {
        var currentUserId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        if (id == currentUserId)
            throw new InvalidOperationException("لا يمكنك إيقاف حسابك بنفسك");

        var user = await _db.Users.FindAsync(id)
            ?? throw new KeyNotFoundException("المستخدم غير موجود");

        user.IsActive = !user.IsActive;
        await _db.SaveChangesAsync();

        var msg = user.IsActive ? "تم تفعيل الحساب" : "تم إيقاف الحساب";
        return Ok(new { message = msg, isActive = user.IsActive });
    }

    /// <summary>إعادة تعيين كلمة مرور مستخدم</summary>
    [HttpPatch("{id:int}/reset-password")]
    public async Task<IActionResult> ResetPassword(int id, [FromBody] AdminResetPasswordDto dto)
    {
        var user = await _db.Users.FindAsync(id)
            ?? throw new KeyNotFoundException("المستخدم غير موجود");

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword);
        await _db.SaveChangesAsync();

        return Ok(new { message = $"تم إعادة تعيين كلمة مرور {user.FullName}" });
    }

    /// <summary>تعديل بيانات مستخدم</summary>
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateUserDto dto)
    {
        var user = await _db.Users.FindAsync(id)
            ?? throw new KeyNotFoundException("المستخدم غير موجود");

        if (dto.FullName != null) user.FullName = dto.FullName;
        if (dto.PhoneNumber != null)
        {
            if (await _db.Users.AnyAsync(u => u.PhoneNumber == dto.PhoneNumber && u.Id != id))
                throw new InvalidOperationException("رقم الجوال مستخدم من قِبل مستخدم آخر");
            user.PhoneNumber = dto.PhoneNumber;
        }
        await _db.SaveChangesAsync();
        return Ok(new { message = "تم تحديث البيانات" });
    }

    /// <summary>جلب صلاحيات مستخدم</summary>
    [HttpGet("{id:int}/permissions")]
    public async Task<IActionResult> GetPermissions(int id)
    {
        var p = await _db.UserPermissions.FirstOrDefaultAsync(x => x.UserId == id);
        if (p == null)
        {
            p = new MajlisManagement.Models.UserPermission { UserId = id };
            _db.UserPermissions.Add(p);
            await _db.SaveChangesAsync();
        }
        return Ok(p);
    }

    /// <summary>تحديث صلاحيات مستخدم</summary>
    [HttpPut("{id:int}/permissions")]
    public async Task<IActionResult> SetPermissions(int id, [FromBody] UpdatePermissionsDto dto)
    {
        var user = await _db.Users.FindAsync(id)
            ?? throw new KeyNotFoundException("المستخدم غير موجود");

        if (user.Role == "Admin")
            throw new InvalidOperationException("لا يمكن تقييد صلاحيات المدير");

        var p = await _db.UserPermissions.FirstOrDefaultAsync(x => x.UserId == id);
        if (p == null)
        {
            p = new MajlisManagement.Models.UserPermission { UserId = id };
            _db.UserPermissions.Add(p);
        }

        p.ViewBookings      = dto.ViewBookings;
        p.CreateBookings    = dto.CreateBookings;
        p.ConfirmBookings   = dto.ConfirmBookings;
        p.DeleteBookings    = dto.DeleteBookings;
        p.ViewMembers       = dto.ViewMembers;
        p.ManageMembers     = dto.ManageMembers;
        p.ViewAllComplaints = dto.ViewAllComplaints;
        p.RespondComplaints = dto.RespondComplaints;
        p.ViewDashboard     = dto.ViewDashboard;
        p.ViewReports       = dto.ViewReports;

        await _db.SaveChangesAsync();
        return Ok(new { message = $"تم تحديث صلاحيات {user.FullName}" });
    }

    /// <summary>تطبيق نفس الصلاحيات على جميع المستخدمين العاديين</summary>
    [HttpPut("permissions/all")]
    public async Task<IActionResult> SetAllPermissions([FromBody] UpdatePermissionsDto dto)
    {
        var userIds = await _db.Users
            .Where(u => u.Role != "Admin")
            .Select(u => u.Id)
            .ToListAsync();

        if (!userIds.Any()) return Ok(new { message = "لا يوجد مستخدمون عاديون", count = 0 });

        var existing = await _db.UserPermissions
            .Where(p => userIds.Contains(p.UserId))
            .ToListAsync();

        var existingIds = existing.Select(p => p.UserId).ToHashSet();

        // أنشئ سجلات ناقصة
        foreach (var uid in userIds.Where(id => !existingIds.Contains(id)))
        {
            var np = new MajlisManagement.Models.UserPermission { UserId = uid };
            _db.UserPermissions.Add(np);
            existing.Add(np);
        }

        // طبّق الصلاحيات على الجميع
        foreach (var p in existing)
        {
            p.ViewBookings      = dto.ViewBookings;
            p.CreateBookings    = dto.CreateBookings;
            p.ConfirmBookings   = dto.ConfirmBookings;
            p.DeleteBookings    = dto.DeleteBookings;
            p.ViewMembers       = dto.ViewMembers;
            p.ManageMembers     = dto.ManageMembers;
            p.ViewAllComplaints = dto.ViewAllComplaints;
            p.RespondComplaints = dto.RespondComplaints;
            p.ViewDashboard     = dto.ViewDashboard;
            p.ViewReports       = dto.ViewReports;
        }

        await _db.SaveChangesAsync();
        return Ok(new { message = $"تم توحيد صلاحيات {existing.Count} مستخدم", count = existing.Count });
    }

    /// <summary>سجل دخول المستخدمين</summary>
    [HttpGet("login-logs")]
    public async Task<IActionResult> GetLoginLogs([FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        var total = await _db.LoginLogs.CountAsync();
        var logs = await _db.LoginLogs
            .OrderByDescending(l => l.LoginAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(l => new
            {
                l.Id,
                l.UserId,
                UserName  = l.User.FullName,
                UserEmail = l.User.Email,
                UserRole  = l.User.Role,
                l.LoginAt,
                l.IpAddress
            })
            .ToListAsync();

        return Ok(new { total, logs });
    }

    /// <summary>حذف سجل دخول</summary>
    [HttpDelete("login-logs/{id:int}")]
    public async Task<IActionResult> DeleteLoginLog(int id)
    {
        var log = await _db.LoginLogs.FindAsync(id)
            ?? throw new KeyNotFoundException("السجل غير موجود");
        _db.LoginLogs.Remove(log);
        await _db.SaveChangesAsync();
        return Ok(new { message = "تم حذف السجل" });
    }

    /// <summary>حذف جماعي لسجلات الدخول</summary>
    [HttpDelete("login-logs")]
    public async Task<IActionResult> BulkDeleteLoginLogs([FromBody] int[] ids)
    {
        if (ids == null || ids.Length == 0)
            return BadRequest(new { message = "لم يتم تحديد أي سجلات" });
        var logs = await _db.LoginLogs.Where(l => ids.Contains(l.Id)).ToListAsync();
        _db.LoginLogs.RemoveRange(logs);
        await _db.SaveChangesAsync();
        return Ok(new { message = $"تم حذف {logs.Count} سجل", count = logs.Count });
    }

    /// <summary>حذف مستخدم</summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var currentUserId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        if (id == currentUserId)
            throw new InvalidOperationException("لا يمكنك حذف حسابك بنفسك");

        var user = await _db.Users.FindAsync(id)
            ?? throw new KeyNotFoundException("المستخدم غير موجود");

        _db.Users.Remove(user);
        await _db.SaveChangesAsync();
        return Ok(new { message = "تم حذف المستخدم" });
    }
}
