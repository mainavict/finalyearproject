namespace finalyearproject.Models;

public class SubscriptionPlan
{
    public int Id { get; set; }
    public string PlanCode { get; set; } = string.Empty; // "trial", "payg_50", "pro", "ultra_pro"
    public string DisplayName { get; set; } = string.Empty; // "Free Trial", "Top-Up", "Pro", etc.
    public string Description { get; set; } = string.Empty;
    public int PriceKSH { get; set; }
    public int IncludedMinutes { get; set; }
    public int DurationDays { get; set; } // 0 for top-ups, 30 for monthly plans
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}