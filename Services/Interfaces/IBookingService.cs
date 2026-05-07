using MajlisManagement.DTOs;
using MajlisManagement.Models;

namespace MajlisManagement.Services.Interfaces;

public interface IBookingService
{
    Task<BookingResponseDto> CreateAsync(int userId, CreateBookingDto dto);
    Task<BookingResponseDto> GetByIdAsync(int id);
    Task<(List<BookingResponseDto> Items, int Total)> GetAllAsync(BookingFilterDto filter);
    Task<List<BookingCalendarDto>> GetCalendarAsync(int year, int month);
    Task<BookingResponseDto> UpdateAsync(int id, int userId, UpdateBookingDto dto);
    Task<BookingResponseDto> AdminUpdateAsync(int id, AdminUpdateBookingDto dto);
    Task DeleteAsync(int id);

    // فحص التعارض
    Task<bool> HasConflictAsync(DateTime start, DateTime end, int? excludeId = null);
    // قائمة التعارضات عند الحجز كعزاء
    Task<List<BookingResponseDto>> GetConflictingBookingsAsync(DateTime start, DateTime end, int? excludeId = null);
}
