using MajlisManagement.DTOs;

namespace MajlisManagement.Services.Interfaces;

public interface IPaymentService
{
    Task<PaymentResponseDto> CreateAsync(CreatePaymentDto dto);
    Task<PaymentResponseDto> GetByBookingIdAsync(int bookingId);
    Task<PaymentResponseDto> AddTransactionAsync(int paymentId, AddPaymentTransactionDto dto);
    Task<List<PaymentResponseDto>> GetOverdueAsync();
}
