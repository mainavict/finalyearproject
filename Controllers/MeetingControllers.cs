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
    
    
    
    
    
  
    
}