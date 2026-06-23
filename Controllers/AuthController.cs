using finalyearproject.DTOs;
using finalyearproject.Services.Interfaces;
using finalyearproject.Wrappers;
using finalyearproject.Data;
using finalyearproject.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace finalyearproject.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly AppDbContext _context;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        IAuthService authService,
        AppDbContext context,
        ILogger<AuthController> logger)
    {
        _authService = authService;
        _context = context;
        _logger = logger;
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

    [HttpPost("logout")]
    public IActionResult Logout()
    {
        return Ok(ApiResponse<string>.SuccessResponse(" ", "logged out successfully", 200));
    }

    // ✅ NEW: Sync Clerk user endpoint
    [HttpPost("sync-clerk-user")]
    public async Task<IActionResult> SyncClerkUser([FromBody] SyncClerkUserRequest request)
    {
        try
        {
            _logger.LogInformation("🔄 Syncing Clerk user: {ClerkUserId}", request.ClerkUserId);

            // Check if user exists by Clerk ID
            var existingByClerk = await _context.Users
                .FirstOrDefaultAsync(u => u.ClerkUserId == request.ClerkUserId);

            if (existingByClerk != null)
            {
                existingByClerk.FullName = request.FullName;
                existingByClerk.Email = request.Email;
                existingByClerk.IsActive = true;
                await _context.SaveChangesAsync();

                _logger.LogInformation("✅ Updated existing Clerk user: {UserId}", existingByClerk.UserId);
                return Ok(new { userId = existingByClerk.UserId });
            }

            // Check if user exists by email
            var existingByEmail = await _context.Users
                .FirstOrDefaultAsync(u => u.Email == request.Email && !u.IsDeleted);

            if (existingByEmail != null)
            {
                existingByEmail.ClerkUserId = request.ClerkUserId;
                existingByEmail.FullName = request.FullName;
                await _context.SaveChangesAsync();

                _logger.LogInformation("✅ Linked existing user to Clerk: {UserId}", existingByEmail.UserId);
                return Ok(new { userId = existingByEmail.UserId });
            }

            // Create new user
            var newUser = new User
            {
                UserId = Guid.NewGuid(),
                ClerkUserId = request.ClerkUserId,
                FullName = request.FullName,
                Email = request.Email,
                PasswordHash = "",
                IsActive = true,
                IsDeleted = false,
                CreatedAt = DateTime.UtcNow
            };

            _context.Users.Add(newUser);
            await _context.SaveChangesAsync();

            _logger.LogInformation("✅ Created new user from Clerk: {UserId}", newUser.UserId);
            return Ok(new { userId = newUser.UserId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error syncing Clerk user");
            return StatusCode(500, new { error = "Failed to sync user", details = ex.Message });
        }
    }
}

// ✅ DTO moved OUTSIDE the controller class
public class SyncClerkUserRequest
{
    public string ClerkUserId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
}