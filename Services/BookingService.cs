using Microsoft.EntityFrameworkCore;
using MajlisManagement.Data;
using MajlisManagement.DTOs;
using MajlisManagement.Models;
using MajlisManagement.Services.Interfaces;

namespace MajlisManagement.Services;

public class BookingService : IBookingService
{
    private readonly AppDbContext _db;

    public BookingService(AppDbContext db) => _db = db;

    public async Task<BookingResponseDto> CreateAsync(int userId, CreateBookingDto dto)
    {
        if (dto.StartDate >= dto.EndDate)
            throw new InvalidOperationException("تاريخ البداية يجب أن يكون قبل تاريخ النهاية");

        if (dto.StartDate.Date < DateTime.Today)
            throw new InvalidOperationException("لا يمكن الحجز في تاريخ سابق");

        var conflicts = await GetConflictingBookingsAsync(dto.StartDate, dto.EndDate);

        // العزاء يتجاوز الحجوزات غير المؤكدة فقط
        if (dto.Type == BookingType.Condolence)
        {
            var confirmedConflicts = conflicts.Where(c => c.Status == BookingStatus.Confirmed.ToString()).ToList();
            if (confirmedConflicts.Any())
                throw new InvalidOperationException(
                    $"يوجد حجز مؤكد في هذه الفترة: {confirmedConflicts.First().GuestName}. " +
                    "لا يمكن تجاوز الحجوزات المؤكدة حتى لحجوزات العزاء.");
        }
        else if (conflicts.Any())
        {
            throw new InvalidOperationException(
                $"يوجد تعارض في الحجز مع: {conflicts.First().GuestName} " +
                $"({conflicts.First().StartDate:yyyy/MM/dd} - {conflicts.First().EndDate:yyyy/MM/dd})");
        }

        var booking = new Booking
        {
            UserId = userId,
            GuestName = dto.GuestName,
            PhoneNumber = dto.PhoneNumber,
            StartDate = dto.StartDate.Date,
            EndDate = dto.EndDate.Date,
            Type = dto.Type,
            Notes = dto.Notes,
            Cost = dto.Cost,
            // العزاء يبدأ بحالة Confirmed مباشرة
            Status = dto.Type == BookingType.Condolence ? BookingStatus.Confirmed : BookingStatus.Pending
        };

        _db.Bookings.Add(booking);

        // إلغاء الحجوزات غير المؤكدة المتعارضة مع العزاء
        if (dto.Type == BookingType.Condolence && conflicts.Any())
        {
            var pendingConflicts = await _db.Bookings
                .Where(b => conflicts.Select(c => c.Id).Contains(b.Id))
                .ToListAsync();

            foreach (var conflict in pendingConflicts)
            {
                conflict.Status = BookingStatus.Cancelled;
                conflict.AdminNote = $"تم الإلغاء تلقائياً بسبب حجز عزاء للفترة {booking.StartDate:yyyy/MM/dd} - {booking.EndDate:yyyy/MM/dd}";
                conflict.UpdatedAt = DateTime.UtcNow;
            }
        }

        await _db.SaveChangesAsync();
        return await GetByIdAsync(booking.Id);
    }

    public async Task<BookingResponseDto> GetByIdAsync(int id)
    {
        var booking = await _db.Bookings
            .Include(b => b.User)
            .Include(b => b.Payment)
            .FirstOrDefaultAsync(b => b.Id == id)
            ?? throw new KeyNotFoundException("الحجز غير موجود");

        return MapToDto(booking);
    }

    public async Task<(List<BookingResponseDto> Items, int Total)> GetAllAsync(BookingFilterDto filter)
    {
        var query = _db.Bookings
            .Include(b => b.User)
            .Include(b => b.Payment)
            .AsQueryable();

        if (filter.From.HasValue)
            query = query.Where(b => b.StartDate >= filter.From.Value.Date);

        if (filter.To.HasValue)
            query = query.Where(b => b.EndDate <= filter.To.Value.Date);

        if (filter.Type.HasValue)
            query = query.Where(b => b.Type == filter.Type.Value);

        if (filter.Status.HasValue)
            query = query.Where(b => b.Status == filter.Status.Value);

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(b => b.CreatedAt)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync();

        return (items.Select(MapToDto).ToList(), total);
    }

    public async Task<List<BookingCalendarDto>> GetCalendarAsync(int year, int month)
    {
        var start = new DateTime(year, month, 1);
        var end = start.AddMonths(1);

        return await _db.Bookings
            .Where(b => b.StartDate < end && b.EndDate >= start
                     && b.Status != BookingStatus.Cancelled)
            .Select(b => new BookingCalendarDto
            {
                Id = b.Id,
                GuestName = b.GuestName,
                StartDate = b.StartDate,
                EndDate = b.EndDate,
                Type = b.Type.ToString(),
                Status = b.Status.ToString()
            })
            .ToListAsync();
    }

    public async Task<BookingResponseDto> UpdateAsync(int id, int userId, UpdateBookingDto dto)
    {
        var booking = await _db.Bookings.FindAsync(id)
            ?? throw new KeyNotFoundException("الحجز غير موجود");

        if (booking.UserId != userId)
            throw new UnauthorizedAccessException("غير مصرح لك بتعديل هذا الحجز");

        if (booking.Status == BookingStatus.Confirmed || booking.Status == BookingStatus.Completed)
            throw new InvalidOperationException("لا يمكن تعديل حجز مؤكد أو مكتمل");

        var newStart = dto.StartDate?.Date ?? booking.StartDate;
        var newEnd = dto.EndDate?.Date ?? booking.EndDate;

        if (newStart != booking.StartDate || newEnd != booking.EndDate)
        {
            if (await HasConflictAsync(newStart, newEnd, id))
                throw new InvalidOperationException("يوجد تعارض في التواريخ الجديدة");
        }

        if (dto.GuestName != null) booking.GuestName = dto.GuestName;
        if (dto.PhoneNumber != null) booking.PhoneNumber = dto.PhoneNumber;
        if (dto.StartDate.HasValue) booking.StartDate = dto.StartDate.Value.Date;
        if (dto.EndDate.HasValue) booking.EndDate = dto.EndDate.Value.Date;
        if (dto.Type.HasValue) booking.Type = dto.Type.Value;
        if (dto.Notes != null) booking.Notes = dto.Notes;
        if (dto.Cost.HasValue) booking.Cost = dto.Cost.Value;
        booking.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return await GetByIdAsync(id);
    }

    public async Task<BookingResponseDto> AdminUpdateAsync(int id, AdminUpdateBookingDto dto)
    {
        var booking = await _db.Bookings.FindAsync(id)
            ?? throw new KeyNotFoundException("الحجز غير موجود");

        if (dto.Status.HasValue) booking.Status = dto.Status.Value;
        if (dto.AdminNote != null) booking.AdminNote = dto.AdminNote;
        if (dto.Cost.HasValue) booking.Cost = dto.Cost.Value;
        booking.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return await GetByIdAsync(id);
    }

    public async Task DeleteAsync(int id)
    {
        var booking = await _db.Bookings.FindAsync(id)
            ?? throw new KeyNotFoundException("الحجز غير موجود");

        _db.Bookings.Remove(booking);
        await _db.SaveChangesAsync();
    }

    public async Task<bool> HasConflictAsync(DateTime start, DateTime end, int? excludeId = null)
    {
        var conflicts = await GetConflictingBookingsAsync(start, end, excludeId);
        return conflicts.Any();
    }

    public async Task<List<BookingResponseDto>> GetConflictingBookingsAsync(DateTime start, DateTime end, int? excludeId = null)
    {
        var query = _db.Bookings
            .Include(b => b.User)
            .Include(b => b.Payment)
            .Where(b => b.Status != BookingStatus.Cancelled
                     && b.StartDate < end.Date
                     && b.EndDate > start.Date);

        if (excludeId.HasValue)
            query = query.Where(b => b.Id != excludeId.Value);

        var conflicts = await query.ToListAsync();
        return conflicts.Select(MapToDto).ToList();
    }

    private static BookingResponseDto MapToDto(Booking b) => new()
    {
        Id = b.Id,
        UserId = b.UserId,
        UserName = b.User?.FullName ?? string.Empty,
        GuestName = b.GuestName,
        PhoneNumber = b.PhoneNumber,
        StartDate = b.StartDate,
        EndDate = b.EndDate,
        DurationDays = (b.EndDate - b.StartDate).Days,
        Type = b.Type.ToString(),
        Status = b.Status.ToString(),
        Notes = b.Notes,
        AdminNote = b.AdminNote,
        Cost = b.Cost,
        CreatedAt = b.CreatedAt,
        Payment = b.Payment == null ? null : new PaymentSummaryDto
        {
            TotalAmount = b.Payment.TotalAmount,
            PaidAmount = b.Payment.PaidAmount,
            RemainingAmount = b.Payment.TotalAmount - b.Payment.PaidAmount,
            Status = b.Payment.Status.ToString()
        }
    };
}
