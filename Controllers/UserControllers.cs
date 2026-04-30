using Microsoft.AspNetCore.Mvc;
using finalyearproject.Services.Interfaces;
using finalyearproject.DTOs;
using finalyearproject.Wrappers;

namespace finalyearproject.Controllers;
[Route("api/[controller]")]
[ApiController]
public class UsersController : ControllerBase
{
    private readonly IUsers _userService;

    public UsersController(IUsers userService)
    {
        _userService = userService;
    }

    [HttpPost]
    public async Task<ApiResponse<ReadUserDTOs>> CreateUser([FromBody] CreateUserDTO dto)
    {
        var result = await _userService.CreateUserAsync(dto);
        return result;
    }

    [HttpGet]
    public async Task<ApiResponse<IEnumerable<ReadUserDTOs>>> GetUsers()
    {
        var result = await _userService.GetAllAsync();
        return result;
    }

    [HttpGet("{id}")]
    public async Task<ApiResponse<ReadUserDTOs>> GetUser(Guid id)
    {
        var result = await _userService.GetByIdAsync(id);
        return result;
    }

    [HttpPut("{id}")]
    public async Task<ApiResponse<ReadUserDTOs>> UpdateUser(Guid id, [FromBody] UpdateUserDto dto)
    {
        var result = await _userService.UpdateUserAsync(id, dto);
        return result;
    }

    [HttpDelete("{id}")]
    public async Task<ApiResponse<bool>> DeleteUser(Guid id)
    {
        var result = await _userService.DeleteUserAsync(id);
        return result;
    }
}

