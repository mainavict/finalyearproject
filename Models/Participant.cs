using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace finalyearproject.Models;

public class Participant
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
    
    [ForeignKey("Meeting")]
    public Guid MeetingId { get; set; }
    
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;
    
    [MaxLength(300)]
    public string? Email { get; set; }
    
    [MaxLength(100)]
    public string? Role { get; set; } // e.g., "Host", "Attendee", "Presenter"
    
    public bool IsHost { get; set; } = false;
    
    public DateTime JoinedAt { get; set; }
    
    public DateTime? LeftAt { get; set; }
    
    public int DurationSeconds { get; set; } // How long they were in the meeting
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation property
    public Meeting Meeting { get; set; } = null!;
}