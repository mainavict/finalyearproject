namespace finalyearproject.DTOs;



public class MinutesDto
{
    public Guid             Id           { get; set; }
    public Guid             MeetingId    { get; set; }
    public string           Status       { get; set; } = string.Empty;
    public int              Version      { get; set; }
    public string ? DraftJson    { get; set; }
    public string?          ApprovedHtml { get; set; }
    public DateTime?        ApprovedAt   { get; set; }
    public DateTime         CreatedAt    { get; set; }
    public DateTime         UpdatedAt    { get; set; }
}
public class CreateMinutesDtos
{
    public Guid MeetingId { get; set; }
}


public class GeminiRequest
{
    public object[] contents { get; set; }
}

// Simplified model for reading Gemini's response
public class GeminiResponse
{
    public Candidate[] candidates { get; set; }
}

public class Candidate
{
    public Content content { get; set; }
}

public class Content
{
    public Part[] parts { get; set; }
}

public class Part
{
    public string text { get; set; }
}