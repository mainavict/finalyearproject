namespace finalyearproject.Models;

public class User
{
    public Guid UserId { get; set; } =  Guid.NewGuid();
    public Guid OrgId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Role { get; set; } = "Member";
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
// Navigation
    public Organization Organization { get; set; } = null!;
}