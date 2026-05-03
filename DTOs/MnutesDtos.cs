namespace finalyearproject.DTOs;

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