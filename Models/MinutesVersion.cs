namespace finalyearproject.Models;

public class MinutesVersion
{
    public Guid Id { get; set; }
    public Guid MinutesId { get; set; }
    public Guid EditedBy { get; set; }
    public int VersionNumber { get; set; }
    public string DraftJson { get; set; } = string.Empty;
    public string? EditSummary { get; set; }
    public DateTime CreatedAt { get; set; }
    public Minutes Minutes { get; set; } = null!;
    public User Editor { get; set; } = null!;
}