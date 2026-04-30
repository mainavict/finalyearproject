

namespace finalyearproject.Models;

public  enum PlanTier
{
    Free,
    Pro,
    Enterprise,
}

public class Organization
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public PlanTier Plan { get; set; } = PlanTier.Free;
    public int MeetingsUsedThisMonth { get; set; } = 0;
    public DateTime PlanRenewsAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
// Navigation properties
    public ICollection<User> Users { get; set; } = [];
    public ICollection<Meeting> Meetings { get; set; } = [];
  
}