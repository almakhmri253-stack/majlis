using Microsoft.EntityFrameworkCore;
using MajlisManagement.Data;
using MajlisManagement.DTOs;
using MajlisManagement.Models;
using MajlisManagement.Services.Interfaces;

namespace MajlisManagement.Services;

public class MemberService : IMemberService
{
    private readonly AppDbContext _db;

    public MemberService(AppDbContext db) => _db = db;

    public async Task<MemberResponseDto> CreateAsync(CreateMemberDto dto)
    {
        if (await _db.Members.AnyAsync(m => m.PhoneNumber == dto.PhoneNumber))
            throw new InvalidOperationException("رقم الجوال مستخدم لعضو آخر");

        var member = new Member
        {
            FullName            = dto.FullName,
            PhoneNumber         = dto.PhoneNumber,
            NationalId          = dto.NationalId,
            Address             = dto.Address,
            MonthlySubscription = dto.MonthlySubscription,
            TotalPaymentDue     = dto.TotalPaymentDue,
            PaidAmount          = dto.PaidAmount,
            LastPaymentDate     = dto.LastPaymentDate,
            JoinDate            = DateTime.UtcNow
        };

        _db.Members.Add(member);
        await _db.SaveChangesAsync();
        return await GetByIdAsync(member.Id);
    }

    public async Task<List<MemberResponseDto>> GetAllAsync()
    {
        var members = await _db.Members
            .Include(m => m.Payments)
            .OrderBy(m => m.FullName)
            .ToListAsync();

        return members.Select(MapToDto).ToList();
    }

    public async Task<MemberResponseDto> GetByIdAsync(int id)
    {
        var member = await _db.Members
            .Include(m => m.Payments)
            .FirstOrDefaultAsync(m => m.Id == id)
            ?? throw new KeyNotFoundException("العضو غير موجود");

        return MapToDto(member);
    }

    public async Task<MemberResponseDto> UpdateAsync(int id, UpdateMemberDto dto)
    {
        var member = await _db.Members.FindAsync(id)
            ?? throw new KeyNotFoundException("العضو غير موجود");

        if (dto.PhoneNumber != null && dto.PhoneNumber != member.PhoneNumber)
        {
            if (await _db.Members.AnyAsync(m => m.PhoneNumber == dto.PhoneNumber && m.Id != id))
                throw new InvalidOperationException("رقم الجوال مستخدم لعضو آخر");
            member.PhoneNumber = dto.PhoneNumber;
        }

        if (dto.FullName != null)             member.FullName             = dto.FullName;
        if (dto.NationalId != null)           member.NationalId           = dto.NationalId;
        if (dto.Address != null)              member.Address              = dto.Address;
        if (dto.Status.HasValue)              member.Status               = dto.Status.Value;
        if (dto.MonthlySubscription.HasValue) member.MonthlySubscription  = dto.MonthlySubscription.Value;
        if (dto.TotalPaymentDue.HasValue)     member.TotalPaymentDue      = dto.TotalPaymentDue.Value;
        if (dto.PaidAmount.HasValue)          member.PaidAmount           = dto.PaidAmount.Value;
        if (dto.LastPaymentDate.HasValue)     member.LastPaymentDate      = dto.LastPaymentDate.Value;

        await _db.SaveChangesAsync();
        return await GetByIdAsync(id);
    }

    public async Task DeleteAsync(int id)
    {
        var member = await _db.Members.FindAsync(id)
            ?? throw new KeyNotFoundException("العضو غير موجود");

        _db.Members.Remove(member);
        await _db.SaveChangesAsync();
    }

    public async Task<MemberPaymentResponseDto> AddPaymentAsync(int memberId, AddMemberPaymentDto dto)
    {
        var member = await _db.Members.FindAsync(memberId)
            ?? throw new KeyNotFoundException("العضو غير موجود");

        if (await _db.MemberPayments.AnyAsync(p =>
            p.MemberId == memberId && p.Year == dto.Year && p.Month == dto.Month))
            throw new InvalidOperationException($"تم تسجيل دفع شهر {dto.Month}/{dto.Year} لهذا العضو مسبقاً");

        var payment = new MemberPayment
        {
            MemberId = memberId,
            Year = dto.Year,
            Month = dto.Month,
            Amount = dto.Amount,
            IsPaid = true,
            PaidAt = DateTime.UtcNow,
            Note = dto.Note
        };

        _db.MemberPayments.Add(payment);
        await _db.SaveChangesAsync();

        return new MemberPaymentResponseDto
        {
            Id = payment.Id,
            MemberId = memberId,
            MemberName = member.FullName,
            Year = payment.Year,
            Month = payment.Month,
            Amount = payment.Amount,
            IsPaid = payment.IsPaid,
            PaidAt = payment.PaidAt,
            Note = payment.Note
        };
    }

    public async Task<List<MemberResponseDto>> GetDelinquentMembersAsync()
    {
        var members = await _db.Members
            .Include(m => m.Payments)
            .Where(m => m.Status == MemberStatus.Active)
            .ToListAsync();

        return members
            .Select(MapToDto)
            .Where(m => m.OverdueAmount > 0)
            .OrderByDescending(m => m.OverdueAmount)
            .ToList();
    }

    public async Task<List<MemberPaymentResponseDto>> GetMemberPaymentsAsync(int memberId)
    {
        var member = await _db.Members.FindAsync(memberId)
            ?? throw new KeyNotFoundException("العضو غير موجود");

        var payments = await _db.MemberPayments
            .Where(p => p.MemberId == memberId)
            .OrderByDescending(p => p.Year)
            .ThenByDescending(p => p.Month)
            .ToListAsync();

        return payments.Select(p => new MemberPaymentResponseDto
        {
            Id = p.Id,
            MemberId = p.MemberId,
            MemberName = member.FullName,
            Year = p.Year,
            Month = p.Month,
            Amount = p.Amount,
            IsPaid = p.IsPaid,
            PaidAt = p.PaidAt,
            Note = p.Note
        }).ToList();
    }

    private static MemberResponseDto MapToDto(Member m)
    {
        var overdueAmount = Math.Max(0, m.TotalPaymentDue - m.PaidAmount);
        return new MemberResponseDto
        {
            Id                  = m.Id,
            FullName            = m.FullName,
            PhoneNumber         = m.PhoneNumber,
            NationalId          = m.NationalId,
            Address             = m.Address,
            Status              = m.Status.ToString(),
            JoinDate            = m.JoinDate,
            MonthlySubscription = m.MonthlySubscription,
            TotalPaymentDue     = m.TotalPaymentDue,
            PaidAmount          = m.PaidAmount,
            OverdueAmount       = overdueAmount,
            LastPaymentDate     = m.LastPaymentDate,
            UnpaidMonths        = 0,
            TotalDebt           = overdueAmount
        };
    }
}
