using MajlisManagement.DTOs;

namespace MajlisManagement.Services.Interfaces;

public interface IComplaintService
{
    Task<ComplaintResponseDto> CreateAsync(int? userId, CreateComplaintDto dto);
    Task<List<ComplaintResponseDto>> GetAllAsync();
    Task<List<ComplaintResponseDto>> GetPublicAsync();
    Task<ComplaintResponseDto> GetByIdAsync(int id);
    Task<ComplaintResponseDto> RespondAsync(int id, RespondComplaintDto dto);
    Task<ComplaintResponseDto> TogglePublicAsync(int id);
}
