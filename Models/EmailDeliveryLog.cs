namespace finalyearproject.Models;

public class EmailDeliveryLog
{
    public Guid Id { get; set; }
    public Guid MeetingId { get; set; }
    public string SendGridMessageId { get; set; } = string.Empty;
    public string RecipientEmail { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public DateTime EventAt { get; set; }
    public string? BounceReason { get; set; }
    public string? RawPayload { get; set; }
    public DateTime CreatedAt { get; set; }
    public Meeting Meeting { get; set; } = null!;
}