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
        
        // ✅ Return the token in the response body (Next.js will set the cookie)
        return Ok(ApiResponse<AuthResponseDto>.SuccessResponse(result, result.Message, 201));
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginDto dto)
    {
        var result = await _authService.LoginAsync(dto);

        if (!result.Success)
            return Unauthorized(ApiResponse<string>.FailureResponse(result.Message, 401));
        
        // ✅ Return the token in the response body (Next.js will set the cookie)
        return Ok(ApiResponse<AuthResponseDto>.SuccessResponse(result, result.Message, 200));
    }

    [HttpPost("logout")]
    public IActionResult Logout()
    {
        // Next.js will handle clearing the cookie
        return Ok(ApiResponse<string>.SuccessResponse(" ", "logedout successfully",200));
    }
}