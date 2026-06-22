namespace finalyearproject.DTOs;

public class MinutesDto
{
    public Guid Id { get; set; }
    public Guid MeetingId { get; set; }
    public string Status { get; set; } = string.Empty;
    public int Version { get; set; }
    public string? DraftJson { get; set; }
    public string? ApprovedHtml { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

// ✅ Fixed: singular name (C# convention)
public class CreateMinutesDto
{
    public Guid MeetingId { get; set; }
}

// ✅ NEW: Response DTO for full meeting data
public class MinutesResponseDto
{
    public Guid MeetingId { get; set; }
    public string MeetingTitle { get; set; } = string.Empty;
    public string? MeetingDate { get; set; }
    public string? Description { get; set; }
    public DateTime? ScheduledAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public int? AudioDurationSecs { get; set; }
    public string? AudioUrl { get; set; }
    public MeetingMinutesData? Minutes { get; set; }
    public string? Transcript { get; set; }
    public List<ParticipantDto> Participants { get; set; } = new();
}

// ✅ NEW: Parsed minutes structure
public class MeetingMinutesData
{
    public string? MeetingTitle { get; set; }
    public string? MeetingDate { get; set; }
    public List<string> Attendees { get; set; } = new();
    public List<string> AbsentWithApology { get; set; } = new();
    public List<string> AbsentWithoutApology { get; set; } = new();
    public string? MeetingSummary { get; set; }
    public List<DiscussionPointDto> DiscussionPoints { get; set; } = new();
    public List<string> Decisions { get; set; } = new();
    public List<ActionItemDto> ActionItems { get; set; } = new();
    public string? NextMeeting { get; set; }
}

public class DiscussionPointDto
{
    public string Topic { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
}

public class ActionItemDto
{
    public string Owner { get; set; } = string.Empty;
    public string Task { get; set; } = string.Empty;
    public string? DueDate { get; set; }
}

public class ParticipantDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Role { get; set; }
    public bool IsHost { get; set; }
    public DateTime JoinedAt { get; set; }
    public DateTime? LeftAt { get; set; }
    public int DurationSeconds { get; set; }
}

public class GeminiRequest
{
    public object[] contents { get; set; } = Array.Empty<object>();
}

public class GeminiResponse
{
    public Candidate[] candidates { get; set; } = Array.Empty<Candidate>();
}

public class Candidate
{
    public Content content { get; set; } = new();
}

public class Content
{
    public Part[] parts { get; set; } = Array.Empty<Part>();
}

public class Part
{
    public string text { get; set; } = string.Empty;
}