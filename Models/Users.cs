using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace finalyearproject.Models;

public class User
{
    [Key]
    public Guid UserId { get; set; } = Guid.NewGuid();
    
    public string FullName { get; set; } = string.Empty;
    
    public string Email { get; set; } = string.Empty;
    
    public bool IsActive { get; set; } = true;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public string PasswordHash { get; set; } = string.Empty;
    
    public bool IsDeleted { get; set; } = false;
    
    // ✅ NEW: Clerk User ID (nullable for migration)
    public string? ClerkUserId { get; set; }
    
    
    public string SubscriptionTier { get; set; } = "trial"; // "trial", "pro", "ultra_pro"
    
    public DateTime? SubscriptionExpiryDate { get; set; }
    
    public int MonthlyMinuteLimit { get; set; } = 10; // 10 mins for trial
    
    public int MinutesUsedThisMonth { get; set; } = 0;
    
    
    public ICollection<Payment>? Payments { get; set; }
    public ICollection<Meeting> Meetings { get; set; } = new List<Meeting>();
}