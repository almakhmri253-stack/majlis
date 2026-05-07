using MajlisManagement.DTOs;

namespace MajlisManagement.Services.Interfaces;

public interface IAuthService
{
    Task<AuthResponseDto> RegisterAsync(RegisterDto dto);
    Task<AuthResponseDto> LoginAsync(LoginDto dto, string? ipAddress = null);
    Task ChangePasswordAsync(int userId, ChangePasswordDto dto);
}
