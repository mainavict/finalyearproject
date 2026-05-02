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

    public int? AudioDurationSecs { get; set; }

    public DateTime CreatedAt { get; set; }
}

public class CreateMeetingDto
{
    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public DateTime? ScheduledAt { get; set; }

}



public class UpdateMeetingDto
    {
        public string? Title { get; set; }

        public string? Description { get; set; }

        public DateTime? ScheduledAt { get; set; }
    }



    public class UpdateMeetingStatusDto
    {
        public string Status { get; set; }
    }


    public class ApproveMeetingDto
    {
        public bool Approve { get; set; }
    }
