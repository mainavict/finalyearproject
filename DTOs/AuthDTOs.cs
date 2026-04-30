namespace finalyearproject.DTOs;


   
        
    public class LoginRequest
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public class LoginResponse
    {
        public string AccessToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
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
