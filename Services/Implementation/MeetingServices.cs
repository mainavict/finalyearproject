using  finalyearproject.Services.Interfaces;
using finalyearproject.DTOs;
using Microsoft.EntityFrameworkCore;
using finalyearproject.Data;
using finalyearproject.Wrappers;
namespace finalyearproject.Services.Implementation;

public class MeetingServices:Imeeting
{
    
    private ILogger<MeetingServices> _logger;
    private readonly AppDbContext _context;

    public MeetingServices(ILogger<MeetingServices> logger, AppDbContext context)
    {
        _logger = logger;
        _context = context;
    }
    
    public async Task<ApiResponse<ResponseMeetingDto>> CreateMeetingAsync(CreateMeetingDto createDto, Guid userId)
{
    _logger.LogInformation("CreateMeetingAsync called");
    
    var user = await _context.Users.FindAsync(userId);
    if (user == null) {
        _logger.LogWarning("User with id {UserId} not found", userId);
        return ApiResponse<ResponseMeetingDto>.FailureResponse("User not found", 404);
    }
    
    var newMeeting = new Models.Meeting
    {
        Title = createDto.Title,
        Description = createDto.Description,
        ScheduledAt = createDto.ScheduledAt,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
        CreatorId = userId,
        
        // ✅ Store meeting URL separately
        MeetingUrl = createDto.MeetingUrl,
        
        // ✅ Set status based on bot deployment
        Status = createDto.DeployBot ? Models.MeetingStatus.Scheduled : Models.MeetingStatus.Created
    };
    
    _context.Meetings.Add(newMeeting);
    await _context.SaveChangesAsync();
    
    _logger.LogInformation(
        "Meeting created: {MeetingId}, Bot deployment: {DeployBot}, Status: {Status}",
        newMeeting.Id, 
        createDto.DeployBot,
        newMeeting.Status
    );
    
    var response = new ResponseMeetingDto
    {
        Id = newMeeting.Id,
        Title = newMeeting.Title,
        Description = newMeeting.Description,
        Status = newMeeting.Status.ToString(),
        CreatedBy = newMeeting.CreatorId,
        ApprovedAt = newMeeting.ApprovedAt,
        ApprovedBy = newMeeting.ApprovedBy,
        MeetingUrl = newMeeting.MeetingUrl, // ✅ Add this to your ResponseMeetingDto
        AudioUrl = newMeeting.AudioUrl,
        ScheduledAt = newMeeting.ScheduledAt,
        EndedAt = newMeeting.EndedAt,
        AudioDurationSecs = newMeeting.AudioDurationSecs,
        CreatedAt = newMeeting.CreatedAt
    };
    
    return ApiResponse<ResponseMeetingDto>.SuccessResponse(response, "Meeting created successfully", 201);
}

    public  async Task<ApiResponse<ResponseMeetingDto>> GetMeetingByIdAsync(Guid meetingId)
    {
        var  meeting = await _context.Meetings.FindAsync(meetingId);

        if (meeting == null)
        {
            _logger.LogWarning("Meeting with id {MeetingId} not found", meetingId);
            return ApiResponse<ResponseMeetingDto>.FailureResponse("Meeting not found",404);
        }
        
        var  response = new  ResponseMeetingDto()
        {
            Id = meeting.Id,
            Title = meeting.Title,
            Description = meeting.Description,
            Status = meeting.Status.ToString(),
            CreatedBy = meeting.CreatorId,
            ApprovedBy = meeting.ApprovedBy,
            AudioUrl = meeting.AudioUrl,
            ScheduledAt = meeting.ScheduledAt,
            EndedAt = meeting.EndedAt,
            ApprovedAt = meeting.ApprovedAt,
            AudioDurationSecs = meeting.AudioDurationSecs,
            CreatedAt = meeting.CreatedAt
        };
        
        _logger.LogInformation("Meeting with id {MeetingId} retrieved successfully", meetingId);
        return ApiResponse<ResponseMeetingDto>.SuccessResponse(response,"Meeting retrieved successfully",200);
    }

    public async Task<ApiResponse<IEnumerable<ResponseMeetingDto>>> GetMeetingsByUserIdAsync(Guid userId)
    {
        var user  = await _context.Users.FindAsync(userId);
        if (user == null)
        {
            _logger.LogWarning("User with id {UserId} not found", userId);
            return ApiResponse<IEnumerable<ResponseMeetingDto>>.FailureResponse("User not found", 404);
        }

        var meetings = await _context.Meetings.Where(m => m.CreatorId == userId).ToListAsync();
        
        var  response = meetings.Select(meeting => new ResponseMeetingDto()
        {
            Id = meeting.Id,
            Title = meeting.Title,
            Description = meeting.Description,
            Status = meeting.Status.ToString(),
            CreatedBy = meeting.CreatorId,
            MeetingUrl = meeting.MeetingUrl,
            ApprovedBy = meeting.ApprovedBy,
            AudioUrl = meeting.AudioUrl,
            ScheduledAt = meeting.ScheduledAt,
            EndedAt = meeting.EndedAt,
            ApprovedAt = meeting.ApprovedAt,
            AudioDurationSecs = meeting.AudioDurationSecs,
            CreatedAt = meeting.CreatedAt
        });
        
        
        return ApiResponse<IEnumerable<ResponseMeetingDto>>.SuccessResponse(response ,"Meetings retrieved successfully",200);
        
    }

    public async  Task<ApiResponse<IEnumerable<ResponseMeetingDto>>> GetAllMeetingsAsync()
    {
        var  meetings = await _context.Meetings.ToListAsync();
        
        var  response =  meetings.Select(meeting => new ResponseMeetingDto()
        {
            Id = meeting.Id,
            Title = meeting.Title,
            Description = meeting.Description,
            Status = meeting.Status.ToString(),
            CreatedBy = meeting.CreatorId,
            ApprovedBy = meeting.ApprovedBy,
            AudioUrl = meeting.AudioUrl,
            ScheduledAt = meeting.ScheduledAt,
            EndedAt = meeting.EndedAt,
            ApprovedAt = meeting.ApprovedAt,
            AudioDurationSecs = meeting.AudioDurationSecs,
            CreatedAt = meeting.CreatedAt
        });
        
        return ApiResponse<IEnumerable<ResponseMeetingDto>>.SuccessResponse(response,"All meetings retrieved successfully",200);
    }

    public async  Task<ApiResponse<ResponseMeetingDto>> UpdateMeetingAsync(Guid meetingId, UpdateMeetingDto updateDto, Guid userId)
    {
        var meeting = await _context.Meetings
            .FirstOrDefaultAsync(m => m.Id == meetingId && m.CreatorId == userId && !m.IsDeleted);
       if (meeting == null)
       {
           _logger.LogWarning("Meeting with id {MeetingId} not found", meetingId);
           return ApiResponse<ResponseMeetingDto>.FailureResponse("Meeting not found", 404);
       }
       
       var    user = await _context.Users.FindAsync(userId);
       if (user == null)
       {
           _logger.LogWarning("User with id {UserId} not found", userId);
           return ApiResponse<ResponseMeetingDto>.FailureResponse("User not found", 404);
       }
       

       if (!string.IsNullOrEmpty(updateDto.Title))
       {
           meeting.Title = updateDto.Title;
       }

       if (!string.IsNullOrEmpty(updateDto.Description))
       {
           meeting.Description = updateDto.Description;
       }
       
       if (!updateDto.ScheduledAt.HasValue)
       {
           meeting.ScheduledAt = updateDto.ScheduledAt;
       }
       
       await _context.SaveChangesAsync();
       
       var  response = new ResponseMeetingDto()
       {
           Id = meeting.Id,
           Title = meeting.Title,
           Description = meeting.Description,
           Status = meeting.Status.ToString(),
           CreatedBy = meeting.CreatorId,
           ApprovedBy = meeting.ApprovedBy,
           AudioUrl = meeting.AudioUrl,
           ScheduledAt = meeting.ScheduledAt,
           EndedAt = meeting.EndedAt,
           ApprovedAt = meeting.ApprovedAt,
           AudioDurationSecs = meeting.AudioDurationSecs,
           CreatedAt = meeting.CreatedAt
        };  
       
       
       return ApiResponse<ResponseMeetingDto>.SuccessResponse(response,"Meeting updated successfully",200);
       }
       
    
 
    public async Task<ApiResponse<bool>> DeleteMeetingAsync(Guid meetingId, Guid userId)
    {
        var  meeting  = await _context.Meetings.FindAsync(meetingId);

        if (meeting == null)
        {
            _logger.LogWarning("Meeting with id {MeetingId} not found", meetingId);
            return ApiResponse<bool>.FailureResponse("Meeting not found", 404);
        }
        
        _context.Meetings.Remove(meeting);
        await _context.SaveChangesAsync();
        _logger.LogInformation("Meeting with id {MeetingId} deleted successfully", meetingId);
        return ApiResponse<bool>.SuccessResponse(true,"Meeting deleted successfully",200);
        
    }

    public async  Task<ApiResponse<ResponseMeetingDto>> UpdateMeetingStatusAsync(Guid meetingId, UpdateMeetingStatusDto statusDto, Guid userId)
    {
       var meeting =  await  _context.Meetings.FindAsync(meetingId);
       if (meeting == null)
       {
           _logger.LogWarning("Meeting with id {MeetingId} not found", meetingId);
           return ApiResponse<ResponseMeetingDto>.FailureResponse("Meeting not found", 404);
       }
       
       meeting.Status = Enum.TryParse<Models.MeetingStatus>(statusDto.Status, true, out var newStatus) ? newStatus : meeting.Status;
       await _context.SaveChangesAsync();
       
       
       return ApiResponse<ResponseMeetingDto>.SuccessResponse(new ResponseMeetingDto()
       {
           Id = meeting.Id,
           Title = meeting.Title,
           Description = meeting.Description,
           Status = meeting.Status.ToString(),
           CreatedBy = meeting.CreatorId,
           ApprovedBy = meeting.ApprovedBy,
           AudioUrl = meeting.AudioUrl,
           ScheduledAt = meeting.ScheduledAt,
           EndedAt = meeting.EndedAt,
           ApprovedAt = meeting.ApprovedAt,
           AudioDurationSecs = meeting.AudioDurationSecs,
           CreatedAt = meeting.CreatedAt
       },"Meeting status updated successfully",200);
    }
    
}