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
        
        var  user  = await  _context.Users.FindAsync(userId);
        if (user == null) {
            _logger.LogWarning("User with id {UserId} not found", userId);
            return ApiResponse<ResponseMeetingDto>.FailureResponse("User not found",404);
        }
        
        var newMeeting = new Models.Meeting
        {
            Title = createDto.Title,
            Description = createDto.Description,
            ScheduledAt = createDto.ScheduledAt,
            CreatedAt = DateTime.UtcNow,
            CreatedId = userId,
        };
        
        _context.Meetings.Add(newMeeting);
        await _context.SaveChangesAsync();
        _logger.LogInformation("CreateMeetingAsync completed");
        
        
        
        var  response =  new ResponseMeetingDto
        {
            Id = newMeeting.Id,
            Title = newMeeting.Title,
            Description = newMeeting.Description,
           Status = newMeeting.Status.ToString(),
            CreatedBy = newMeeting.CreatedId,
            ApprovedAt = newMeeting.ApprovedAt,
             ApprovedBy = newMeeting.ApprovedBy,
            AudioUrl = newMeeting.AudioUrl,
            ScheduledAt = newMeeting.ScheduledAt,
            EndedAt = newMeeting.EndedAt,
            AudioDurationSecs = newMeeting.AudioDurationSecs,
            
            CreatedAt = newMeeting.CreatedAt
        };
        
        return ApiResponse<ResponseMeetingDto>.SuccessResponse(response,"Meeting created successfully",201);
        
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
            CreatedBy = meeting.CreatedId,
            ApprovedBy = meeting.ApprovedBy,
            AudioUrl = meeting.AudioUrl,
            ScheduledAt = meeting.ScheduledAt,
            EndedAt = meeting.EndedAt,
            ApprovedAt = meeting.ApprovedAt,
            AudioDurationSecs = meeting.AudioDurationSecs,
            CreatedAt = meeting.CreatedAt
        };
        
        return ApiResponse<ResponseMeetingDto>.SuccessResponse(response,"Meeting retrieved successfully",200);
    }

    public Task<IEnumerable<ApiResponse<ResponseMeetingDto>>> GetMeetingsByUserIdAsync(Guid userId)
    {
        throw new NotImplementedException();
    }

    public Task<IEnumerable<ApiResponse<ResponseMeetingDto>>> GetAllMeetingsAsync()
    {
        throw new NotImplementedException();
    }

    public Task<ApiResponse<ResponseMeetingDto>> UpdateMeetingAsync(Guid meetingId, UpdateMeetingDto updateDto, Guid userId)
    {
        throw new NotImplementedException();
    }

    public Task<ApiResponse<bool>> DeleteMeetingAsync(Guid meetingId, Guid userId)
    {
        throw new NotImplementedException();
    }

    public Task<ApiResponse<ResponseMeetingDto>> UpdateMeetingStatusAsync(Guid meetingId, UpdateMeetingStatusDto statusDto, Guid userId)
    {
        throw new NotImplementedException();
    }
    
}