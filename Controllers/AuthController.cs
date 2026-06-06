using finalyearproject.DTOs;
using finalyearproject.Services.Interfaces;
using finalyearproject.Wrappers;
using Microsoft.AspNetCore.Mvc;

namespace finalyearproject.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterDto dto)
    {
        var result = await _authService.RegisterAsync(dto);
        
        if (!result.Success)
            return BadRequest(ApiResponse<string>.FailureResponse(result.Message, 400));

        return Ok(ApiResponse<AuthResponseDto>.SuccessResponse(result, result.Message, 201));
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginDto dto)
    {
        var result = await _authService.LoginAsync(dto);

        if (!result.Success)
            return Unauthorized(ApiResponse<string>.FailureResponse(result.Message, 401));

        return Ok(ApiResponse<AuthResponseDto>.SuccessResponse(result, result.Message, 200));
    }
}