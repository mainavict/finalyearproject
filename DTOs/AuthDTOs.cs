namespace finalyearproject.DTOs;


   
public class RegisterDto
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}  
public class LoginDto
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}
public class UserSessionDto
{
    public Guid UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}

public class AuthResponseDto
{
    public string Token { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public Guid? UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}

    public class RefreshTokenRequest
    {
        public string RefreshToken { get; set; } = string.Empty;
    }
    
    public class  forgotPasswordDTO
    {
        public string Email { get; set; } = string.Empty;
    }
    
    public class resetPasswordDTO
        {
        public string Email { get; set; } = string.Empty;
        }
