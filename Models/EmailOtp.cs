namespace finalyearproject.Models;

public enum OtpPurpose
{
    PasswordReset,
    EmailVerification 
   
}

public class EmailOtp
{
    public Guid Id { get; set; } =  Guid.NewGuid();

    // Who this OTP is for
    public string Email { get; set; } = string.Empty;

    // The hashed OTP (NEVER store plain OTP in production)
    public string CodeHash { get; set; } = string.Empty;
    
    public OtpPurpose Purpose { get; set; }
    
    public DateTime ExpiresAt { get; set; }
    
    public bool IsUsed { get; set; } = false;

    public int AttemptCount { get; set; } = 0;

    public DateTime CreatedAt { get; set; }

    public DateTime? UsedAt { get; set; }
}