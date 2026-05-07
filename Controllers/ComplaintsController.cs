using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MajlisManagement.DTOs;
using MajlisManagement.Middleware;
using MajlisManagement.Services.Interfaces;

namespace MajlisManagement.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ComplaintsController : ControllerBase
{
    private readonly IComplaintService _complaints;

    public ComplaintsController(IComplaintService complaints) => _complaints = complaints;

    /// <summary>إرسال ملاحظة أو شكوى (مسموح للجميع، حتى بدون تسجيل)</summary>
    [HttpPost]
    [AllowAnonymous]
    public async Task<IActionResult> Create([FromBody] CreateComplaintDto dto)
    {
        int? userId = null;
        if (User.Identity?.IsAuthenticated == true)
            userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var result = await _complaints.CreateAsync(userId, dto);
        return Ok(new { message = "تم إرسال ملاحظتك بنجاح", id = result.Id });
    }

    /// <summary>الشكاوى والمقترحات المنشورة للعموم (بدون تسجيل دخول)</summary>
    [HttpGet("public")]
    [AllowAnonymous]
    public async Task<IActionResult> GetPublic()
    {
        var result = await _complaints.GetPublicAsync();
        return Ok(result);
    }

    /// <summary>نشر أو إلغاء نشر شكوى (Admin فقط)</summary>
    [HttpPatch("{id:int}/publish")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> TogglePublish(int id)
    {
        var result = await _complaints.TogglePublicAsync(id);
        var msg = result.IsPublic ? "تم نشر الرسالة للعموم" : "تم إلغاء النشر";
        return Ok(new { message = msg, isPublic = result.IsPublic });
    }

    /// <summary>عرض جميع الملاحظات</summary>
    [HttpGet]
    [Authorize]
    [RequirePermission("ViewAllComplaints")]
    public async Task<IActionResult> GetAll()
    {
        var result = await _complaints.GetAllAsync();
        return Ok(result);
    }

    /// <summary>جلب ملاحظة بالمعرف</summary>
    [HttpGet("{id:int}")]
    [Authorize]
    [RequirePermission("ViewAllComplaints")]
    public async Task<IActionResult> GetById(int id)
    {
        var result = await _complaints.GetByIdAsync(id);
        return Ok(result);
    }

    /// <summary>الرد على ملاحظة</summary>
    [HttpPatch("{id:int}/respond")]
    [Authorize]
    [RequirePermission("RespondComplaints")]
    public async Task<IActionResult> Respond(int id, [FromBody] RespondComplaintDto dto)
    {
        var result = await _complaints.RespondAsync(id, dto);
        return Ok(result);
    }
}
