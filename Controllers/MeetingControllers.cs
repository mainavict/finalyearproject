using Microsoft.AspNetCore.Mvc;
using finalyearproject.Services.Interfaces;
using finalyearproject.DTOs;
using finalyearproject.Wrappers;


namespace finalyearproject.Controllers;
[ApiController]
[Route("api/[controller]")]
public class MeetingControllers : ControllerBase
{
    
    private  readonly Imeeting _meetingService;
    
    public MeetingControllers(Imeeting meetingService)
    {
        _meetingService = meetingService;
    }
    
    
    
    [HttpPost]
    public  async Task<ApiResponse<ResponseMeetingDto>> CreateMeeting([FromBody] CreateMeetingDto createDto, Guid userId)
    {
        var result = await _meetingService.CreateMeetingAsync(createDto, userId);
        return result;
    }
    
    
    
    [HttpGet]
    public async  Task<ApiResponse<IEnumerable<ResponseMeetingDto>>> GetAllMeetingsAsync()
    {
        var result = await _meetingService.GetAllMeetingsAsync();
        return result;
    }


    [HttpGet("{meetingid}")]

    public async Task<ApiResponse<ResponseMeetingDto>> GetMeetingByIdAsync(Guid meetingId)
    {
        var result = await _meetingService.GetMeetingByIdAsync(meetingId);
        return result;
    }

    [HttpGet("user/{createrid}")]

    public async Task<ApiResponse<IEnumerable<ResponseMeetingDto>>> GetMeetingsByUserIdAsync(Guid userId)
    {
        var result = await _meetingService.GetMeetingsByUserIdAsync(userId);
        return result;
    }

    [HttpPut]
    public async Task<ApiResponse<ResponseMeetingDto>> UpdateMeetingAsync(Guid meetingId, UpdateMeetingDto updateDto,
        Guid userId)
    {
        var result = await _meetingService.UpdateMeetingAsync(meetingId, updateDto, userId);
        return result;
    }
    
    [HttpDelete("{id}")]
    public async Task<ApiResponse<bool>> DeleteMeetingAsync(Guid meetingId, Guid userId)
    {
        var result = await _meetingService.DeleteMeetingAsync(meetingId, userId);
        return result;
    }
    
    
  
    
}