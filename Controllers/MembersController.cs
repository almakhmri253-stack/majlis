using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MajlisManagement.DTOs;
using MajlisManagement.Middleware;
using MajlisManagement.Services.Interfaces;

namespace MajlisManagement.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MembersController : ControllerBase
{
    private readonly IMemberService _members;

    public MembersController(IMemberService members) => _members = members;

    /// <summary>إضافة عضو جديد (Admin فقط)</summary>
    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create([FromBody] CreateMemberDto dto)
    {
        var result = await _members.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    /// <summary>قائمة جميع الأعضاء</summary>
    [HttpGet]
    [RequirePermission("ViewMembers")]
    public async Task<IActionResult> GetAll()
    {
        var result = await _members.GetAllAsync();
        return Ok(result);
    }

    /// <summary>جلب عضو بالمعرف</summary>
    [HttpGet("{id:int}")]
    [RequirePermission("ViewMembers")]
    public async Task<IActionResult> GetById(int id)
    {
        var result = await _members.GetByIdAsync(id);
        return Ok(result);
    }

    /// <summary>تعديل بيانات عضو</summary>
    [HttpPut("{id:int}")]
    [RequirePermission("ManageMembers")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateMemberDto dto)
    {
        var result = await _members.UpdateAsync(id, dto);
        return Ok(result);
    }

    /// <summary>حذف عضو</summary>
    [HttpDelete("{id:int}")]
    [RequirePermission("ManageMembers")]
    public async Task<IActionResult> Delete(int id)
    {
        await _members.DeleteAsync(id);
        return Ok(new { message = "تم حذف العضو" });
    }

    /// <summary>تسجيل دفع اشتراك لعضو</summary>
    [HttpPost("{id:int}/payments")]
    [RequirePermission("ManageMembers")]
    public async Task<IActionResult> AddPayment(int id, [FromBody] AddMemberPaymentDto dto)
    {
        var result = await _members.AddPaymentAsync(id, dto);
        return Ok(result);
    }

    /// <summary>سجل مدفوعات عضو</summary>
    [HttpGet("{id:int}/payments")]
    [RequirePermission("ViewMembers")]
    public async Task<IActionResult> GetPayments(int id)
    {
        var result = await _members.GetMemberPaymentsAsync(id);
        return Ok(result);
    }

    /// <summary>قائمة المتأخرين في سداد الاشتراكات</summary>
    [HttpGet("delinquent")]
    [RequirePermission("ViewMembers")]
    public async Task<IActionResult> GetDelinquent()
    {
        var result = await _members.GetDelinquentMembersAsync();
        return Ok(result);
    }
}
