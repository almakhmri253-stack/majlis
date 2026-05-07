using MajlisManagement.DTOs;

namespace MajlisManagement.Services.Interfaces;

public interface IMemberService
{
    Task<MemberResponseDto> CreateAsync(CreateMemberDto dto);
    Task<List<MemberResponseDto>> GetAllAsync();
    Task<MemberResponseDto> GetByIdAsync(int id);
    Task<MemberResponseDto> UpdateAsync(int id, UpdateMemberDto dto);
    Task DeleteAsync(int id);
    Task<MemberPaymentResponseDto> AddPaymentAsync(int memberId, AddMemberPaymentDto dto);
    Task<List<MemberResponseDto>> GetDelinquentMembersAsync();
    Task<List<MemberPaymentResponseDto>> GetMemberPaymentsAsync(int memberId);
}
