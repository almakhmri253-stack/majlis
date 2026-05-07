using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MajlisManagement.DTOs;
using MajlisManagement.Middleware;
using MajlisManagement.Services.Interfaces;

namespace MajlisManagement.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PaymentsController : ControllerBase
{
    private readonly IPaymentService _payments;

    public PaymentsController(IPaymentService payments) => _payments = payments;

    /// <summary>إنشاء سجل دفع لحجز</summary>
    [HttpPost]
    [RequirePermission("ManagePayments")]
    public async Task<IActionResult> Create([FromBody] CreatePaymentDto dto)
    {
        var result = await _payments.CreateAsync(dto);
        return CreatedAtAction(nameof(GetByBooking), new { bookingId = result.BookingId }, result);
    }

    /// <summary>جلب سجل الدفع لحجز معين</summary>
    [HttpGet("booking/{bookingId:int}")]
    [RequirePermission("ViewPayments")]
    public async Task<IActionResult> GetByBooking(int bookingId)
    {
        var result = await _payments.GetByBookingIdAsync(bookingId);
        return Ok(result);
    }

    /// <summary>إضافة دفعة على سجل دفع</summary>
    [HttpPost("{paymentId:int}/transactions")]
    [RequirePermission("ManagePayments")]
    public async Task<IActionResult> AddTransaction(int paymentId, [FromBody] AddPaymentTransactionDto dto)
    {
        var result = await _payments.AddTransactionAsync(paymentId, dto);
        return Ok(result);
    }

    /// <summary>قائمة الحجوزات المتأخرة في الدفع</summary>
    [HttpGet("overdue")]
    [RequirePermission("ViewPayments")]
    public async Task<IActionResult> GetOverdue()
    {
        var result = await _payments.GetOverdueAsync();
        return Ok(result);
    }
}
