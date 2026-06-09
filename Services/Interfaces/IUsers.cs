using finalyearproject.DTOs;
using finalyearproject.Wrappers;
namespace finalyearproject.Services.Interfaces;

public interface IUsers


{
    
    Task <ApiResponse<ReadUserDTOs>> CreateUserAsync(CreateUserDTO dto);
    Task<ApiResponse<ReadUserDTOs>> GetByIdAsync(Guid id);

    Task<ApiResponse<ReadUserDTOs>> GetByEmailAsync(string email);

    Task<ApiResponse<IEnumerable<ReadUserDTOs>>> GetAllAsync();
    
    Task<ApiResponse<ReadUserDTOs>> UpdateUserAsync(Guid id, UpdateUserDto dto);
    
    Task<ApiResponse<bool>> DeleteUserAsync(Guid id);
}