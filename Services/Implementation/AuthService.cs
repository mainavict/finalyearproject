using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using finalyearproject.Data;
using finalyearproject.DTOs;
using finalyearproject.Models;
using finalyearproject.Services.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace finalyearproject.Services.Implementation;

public class AuthService : IAuthService
{
    private readonly AppDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly PasswordHasher<User> _passwordHasher = new();

    public AuthService(AppDbContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }

    public async Task<AuthResponseDto> RegisterAsync(RegisterDto dto)
    {
        if (await _context.Users.AnyAsync(u => u.Email == dto.Email && !u.IsDeleted))
        {
            return new AuthResponseDto { Success = false, Message = "Email is already registered." };
        }

        var user = new User
        {
            UserId = Guid.NewGuid(),
            FullName = dto.FullName,
            Email = dto.Email,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        // Securely hash the password
        user.PasswordHash = _passwordHasher.HashPassword(user, dto.Password);

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var token = GenerateJwtToken(user);

        return new AuthResponseDto 
        { 
            Success = true, 
            Message = "Registration successful!", 
            Token = token,
            UserId = user.UserId
        };
    }

    public async Task<AuthResponseDto> LoginAsync(LoginDto dto)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == dto.Email && !u.IsDeleted);

        if (user == null)
        {
            return new AuthResponseDto { Success = false, Message = "Invalid email or password." };
        }

        // Verify the hashed password
        var result = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, dto.Password);

        if (result == PasswordVerificationResult.Failed)
        {
            return new AuthResponseDto { Success = false, Message = "Invalid email or password." };
        }

        if (!user.IsActive)
        {
            return new AuthResponseDto { Success = false, Message = "Account is deactivated." };
        }

        var token = GenerateJwtToken(user);

        return new AuthResponseDto 
        { 
            Success = true, 
            Message = "Login successful!", 
            Token = token,
            UserId = user.UserId
        };
    }

    private string GenerateJwtToken(User user)
    {
        var jwtSettings = _configuration.GetSection("JwtSettings");
        var secretKey = jwtSettings["SecretKey"];

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Name, user.FullName)
        };

        var token = new JwtSecurityToken(
            issuer: jwtSettings["Issuer"],
            audience: jwtSettings["Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(Convert.ToDouble(jwtSettings["ExpiryInMinutes"])),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}