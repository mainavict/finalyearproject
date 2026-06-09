using finalyearproject.DTOs;

namespace finalyearproject.Services.Interfaces;

public interface IAuthService
{

    Task<AuthResponseDto> RegisterAsync(RegisterDto dto);
    Task<AuthResponseDto> LoginAsync(LoginDto dto);
   
    
}