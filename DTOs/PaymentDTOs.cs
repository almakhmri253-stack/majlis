using System.ComponentModel.DataAnnotations;
using MajlisManagement.Models;

namespace MajlisManagement.DTOs;

public class CreatePaymentDto
{
    [Required]
    public int BookingId { get; set; }

    [Required]
    [Range(0.01, double.MaxValue, ErrorMessage = "المبلغ الإجمالي يجب أن يكون أكبر من صفر")]
    public decimal TotalAmount { get; set; }

    public string? Notes { get; set; }
}

public class AddPaymentTransactionDto
{
    [Required]
    [Range(0.01, double.MaxValue, ErrorMessage = "المبلغ يجب أن يكون أكبر من صفر")]
    public decimal Amount { get; set; }

    public string? Note { get; set; }
}

public class PaymentResponseDto
{
    public int Id { get; set; }
    public int BookingId { get; set; }
    public string GuestName { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal RemainingAmount { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? LastPaymentDate { get; set; }
    public string? Notes { get; set; }
    public List<TransactionDto> Transactions { get; set; } = new();
}

public class PaymentSummaryDto
{
    public decimal TotalAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal RemainingAmount { get; set; }
    public string Status { get; set; } = string.Empty;
}

public class TransactionDto
{
    public int Id { get; set; }
    public decimal Amount { get; set; }
    public DateTime PaidAt { get; set; }
    public string? Note { get; set; }
}
