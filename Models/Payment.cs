using System.Text.Json.Serialization;

namespace finalyearproject.Models;

public enum PaymentStatus
{
    Pending = 0,
    Completed = 1,
    Failed = 2
}

public class Payment
{
    public int Id { get; set; }
    public string CheckoutRequestID { get; set; } = string.Empty; // From Daraja
    public Guid UserId { get; set; }
    public string PlanType { get; set; } = string.Empty; // "pro", "ultra_pro"
    public int Amount { get; set; }
    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    
    [JsonIgnore]
    public User? User { get; set; }
}