using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace finalyearproject.Models;

public enum MeetingStatus
{
    Created,
    Scheduled,
    Completed,
    Approved,
    Rejected
}
public class Meeting
{
    [Key]
    public Guid Id { get; set; } =   Guid.NewGuid();
    [ForeignKey("Creator")]
    public Guid CreatorId { get; set; }
    public Guid? ApprovedBy { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public MeetingStatus Status { get; set; } = MeetingStatus.Created;
    
    [MaxLength(4000)]
    public string? AudioUrl { get; set; } = String.Empty;
    public int? AudioDurationSecs{ get; set; }
    public string? AudioMimeType { get; set; }
    public DateTime? ScheduledAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsDeleted { get; set; } = false;

    public User Creator { get; set; } = null!;
    public Transcript? Transcript { get; set; }
    public Minutes? Minutes { get; set; }
}