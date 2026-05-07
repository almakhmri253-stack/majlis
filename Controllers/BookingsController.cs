using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MajlisManagement.DTOs;
using MajlisManagement.Middleware;
using MajlisManagement.Services.Interfaces;

namespace MajlisManagement.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class BookingsController : ControllerBase
{
    private readonly IBookingService _bookings;

    public BookingsController(IBookingService bookings) => _bookings = bookings;

    /// <summary>إنشاء حجز جديد</summary>
    [HttpPost]
    [RequirePermission("CreateBookings")]
    public async Task<IActionResult> Create([FromBody] CreateBookingDto dto)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var result = await _bookings.CreateAsync(userId, dto);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    /// <summary>جلب حجز بالمعرف</summary>
    [HttpGet("{id:int}")]
    [RequirePermission("ViewBookings")]
    public async Task<IActionResult> GetById(int id)
    {
        var result = await _bookings.GetByIdAsync(id);
        return Ok(result);
    }

    /// <summary>قائمة الحجوزات مع فلترة وتصفح</summary>
    [HttpGet]
    [RequirePermission("ViewBookings")]
    public async Task<IActionResult> GetAll([FromQuery] BookingFilterDto filter)
    {
        var (items, total) = await _bookings.GetAllAsync(filter);
        return Ok(new { items, total, page = filter.Page, pageSize = filter.PageSize });
    }

    /// <summary>التقويم الشهري</summary>
    [HttpGet("calendar")]
    [RequirePermission("ViewBookings")]
    public async Task<IActionResult> Calendar([FromQuery] int year = 0, [FromQuery] int month = 0)
    {
        if (year == 0) year = DateTime.Now.Year;
        if (month == 0) month = DateTime.Now.Month;
        var result = await _bookings.GetCalendarAsync(year, month);
        return Ok(result);
    }

    /// <summary>فحص التعارض قبل الحجز</summary>
    [HttpGet("check-conflict")]
    [RequirePermission("CreateBookings")]
    public async Task<IActionResult> CheckConflict(
        [FromQuery] DateTime start,
        [FromQuery] DateTime end,
        [FromQuery] int? excludeId = null)
    {
        var conflicts = await _bookings.GetConflictingBookingsAsync(start, end, excludeId);
        return Ok(new { hasConflict = conflicts.Any(), conflicts });
    }

    /// <summary>تعديل حجز (المستخدم)</summary>
    [HttpPut("{id:int}")]
    [RequirePermission("CreateBookings")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateBookingDto dto)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var result = await _bookings.UpdateAsync(id, userId, dto);
        return Ok(result);
    }

    /// <summary>تعديل حجز من قبل الإدارة (تأكيد/رفض/تعديل)</summary>
    [HttpPatch("{id:int}/admin")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> AdminUpdate(int id, [FromBody] AdminUpdateBookingDto dto)
    {
        var result = await _bookings.AdminUpdateAsync(id, dto);
        return Ok(result);
    }

    /// <summary>حذف حجز</summary>
    [HttpDelete("{id:int}")]
    [RequirePermission("DeleteBookings")]
    public async Task<IActionResult> Delete(int id)
    {
        await _bookings.DeleteAsync(id);
        return Ok(new { message = "تم حذف الحجز" });
    }
}
