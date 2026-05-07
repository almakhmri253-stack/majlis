using MajlisManagement.DTOs;

namespace MajlisManagement.Services.Interfaces;

public interface IEventService
{
    Task<List<EventResponseDto>> GetAllAsync();
    Task<EventResponseDto> CreateAsync(int userId, CreateEventDto dto);
    Task<EventResponseDto> UpdateAsync(int id, UpdateEventDto dto);
    Task DeleteAsync(int id);
}
