using Microsoft.EntityFrameworkCore;
using MajlisManagement.Data;
using MajlisManagement.DTOs;
using MajlisManagement.Models;
using MajlisManagement.Services.Interfaces;

namespace MajlisManagement.Services;

public class PaymentService : IPaymentService
{
    private readonly AppDbContext _db;

    public PaymentService(AppDbContext db) => _db = db;

    public async Task<PaymentResponseDto> CreateAsync(CreatePaymentDto dto)
    {
        var booking = await _db.Bookings.FindAsync(dto.BookingId)
            ?? throw new KeyNotFoundException("الحجز غير موجود");

        if (await _db.Payments.AnyAsync(p => p.BookingId == dto.BookingId))
            throw new InvalidOperationException("يوجد سجل دفع لهذا الحجز بالفعل");

        var payment = new Payment
        {
            BookingId = dto.BookingId,
            TotalAmount = dto.TotalAmount,
            PaidAmount = 0,
            Status = PaymentStatus.Unpaid,
            Notes = dto.Notes
        };

        _db.Payments.Add(payment);
        await _db.SaveChangesAsync();
        return await GetByBookingIdAsync(dto.BookingId);
    }

    public async Task<PaymentResponseDto> GetByBookingIdAsync(int bookingId)
    {
        var payment = await _db.Payments
            .Include(p => p.Booking)
            .Include(p => p.Transactions)
            .FirstOrDefaultAsync(p => p.BookingId == bookingId)
            ?? throw new KeyNotFoundException("سجل الدفع غير موجود");

        return MapToDto(payment);
    }

    public async Task<PaymentResponseDto> AddTransactionAsync(int paymentId, AddPaymentTransactionDto dto)
    {
        var payment = await _db.Payments
            .Include(p => p.Booking)
            .Include(p => p.Transactions)
            .FirstOrDefaultAsync(p => p.Id == paymentId)
            ?? throw new KeyNotFoundException("سجل الدفع غير موجود");

        if (payment.PaidAmount + dto.Amount > payment.TotalAmount)
            throw new InvalidOperationException(
                $"المبلغ المدفوع ({dto.Amount:F2}) يتجاوز المتبقي ({payment.TotalAmount - payment.PaidAmount:F2})");

        var transaction = new PaymentTransaction
        {
            PaymentId = paymentId,
            Amount = dto.Amount,
            Note = dto.Note,
            PaidAt = DateTime.UtcNow
        };

        _db.PaymentTransactions.Add(transaction);

        payment.PaidAmount += dto.Amount;
        payment.LastPaymentDate = DateTime.UtcNow;
        payment.Status = payment.PaidAmount >= payment.TotalAmount
            ? PaymentStatus.Paid
            : PaymentStatus.Partial;

        await _db.SaveChangesAsync();

        // إعادة التحميل لضمان البيانات المحدثة
        payment.Transactions.Add(transaction);
        return MapToDto(payment);
    }

    public async Task<List<PaymentResponseDto>> GetOverdueAsync()
    {
        var overdue = await _db.Payments
            .Include(p => p.Booking)
            .Include(p => p.Transactions)
            .Where(p => p.Status != PaymentStatus.Paid
                     && p.Booking.Status == BookingStatus.Confirmed)
            .ToListAsync();

        return overdue.Select(MapToDto).ToList();
    }

    private static PaymentResponseDto MapToDto(Payment p) => new()
    {
        Id = p.Id,
        BookingId = p.BookingId,
        GuestName = p.Booking?.GuestName ?? string.Empty,
        TotalAmount = p.TotalAmount,
        PaidAmount = p.PaidAmount,
        RemainingAmount = p.TotalAmount - p.PaidAmount,
        Status = p.Status.ToString(),
        CreatedAt = p.CreatedAt,
        LastPaymentDate = p.LastPaymentDate,
        Notes = p.Notes,
        Transactions = p.Transactions.Select(t => new TransactionDto
        {
            Id = t.Id,
            Amount = t.Amount,
            PaidAt = t.PaidAt,
            Note = t.Note
        }).OrderByDescending(t => t.PaidAt).ToList()
    };
}
