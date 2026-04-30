namespace finalyearproject.Models;

public class Transcript
{
    public Guid Id { get; set; }
    public Guid MeetingId { get; set; }
    public string RawText { get; set; } = string.Empty;
    public string? Segments { get; set; } // JSON string
    public string? WhisperModel { get; set; }
    public int? DurationSecs { get; set; }
    public int? WordCount { get; set; }
    public DateTime CreatedAt { get; set; }
    
    public Meeting Meeting { get; set; } = null!;
}