namespace finalyearproject.DTOs;

public class ResponseMeetingDto
{
    public Guid Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string Status { get; set; } = string.Empty;

    public Guid CreatedBy { get; set; }

    public Guid? ApprovedBy { get; set; }
    
    public string? AudioUrl { get; set; } = String.Empty;

    public DateTime? ScheduledAt { get; set; }

    public DateTime? EndedAt { get; set; }

    public DateTime? ApprovedAt { get; set; }
    public string? MeetingUrl { get; set; }
    public int? AudioDurationSecs { get; set; }

    public DateTime CreatedAt { get; set; }
}

public class UpdateMinutesRequest
{
    public MeetingMinutesData Minutes { get; set; } = null!;
}

public class CreateMeetingDto
{
    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public DateTime? ScheduledAt { get; set; }
    
    public string? MeetingUrl { get; set; }
    public bool DeployBot { get; set; } = false;

}



public class UpdateMeetingDto
    {
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DateTime? ScheduledAt { get; set; }
    
        public string? MeetingUrl { get; set; }
        public bool DeployBot { get; set; } = false;
    }



    public class UpdateMeetingStatusDto
    {
        public string Status { get; set; }
    }


    public class ApproveMeetingDto
    {
        public bool Approve { get; set; }
    }

    public class SendBotDto
    {
        /// <summary>Live meeting URL (Zoom, Teams, Google Meet, etc.).</summary>
        public string MeetingUrl { get; set; } = string.Empty;

        /// <summary>Optional display name shown in the meeting for the bot.</summary>
        public string? BotName { get; set; }
    }
