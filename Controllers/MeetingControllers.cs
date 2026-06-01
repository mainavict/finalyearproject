using Microsoft.AspNetCore.Mvc;
using finalyearproject.Services.Interfaces;
using finalyearproject.DTOs;
using finalyearproject.Wrappers;
using finalyearproject.Data;


namespace finalyearproject.Controllers;
[ApiController]
[Route("api/[controller]")]
public class MeetingControllers : ControllerBase
{
    
    private  readonly Imeeting _meetingService;
    private readonly IRecallAiService _recallAiService;
    private readonly AppDbContext _context;
    private readonly ILogger<MeetingControllers> _logger;
    
    public MeetingControllers(
        Imeeting meetingService,
        IRecallAiService recallAiService,
        AppDbContext context,
        ILogger<MeetingControllers> logger)
    {
        _meetingService  = meetingService;
        _recallAiService = recallAiService;
        _context         = context;
        _logger          = logger;
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

    /// <summary>
    /// Sends a Recall.ai recording bot to a live meeting.
    /// Supply the meeting's video URL (Zoom, Teams, Meet…).
    /// </summary>
    [HttpPost("{id}/bot")]
    public async Task<ApiResponse<SendBotResponseDto>> SendBot(Guid id, [FromBody] SendBotDto dto)
    {
        var meeting = await _context.Meetings.FindAsync(id);
        if (meeting == null)
        {
            _logger.LogWarning("Meeting {MeetingId} not found when sending bot", id);
            return ApiResponse<SendBotResponseDto>.FailureResponse("Meeting not found", 404);
        }

        try
        {
            var bot = await _recallAiService.CreateBotAsync(dto.MeetingUrl, dto.BotName ?? "Minutes Assistant");

            meeting.RecallBotId = bot.Id;
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Bot {BotId} dispatched to meeting {MeetingId}", bot.Id, id);

            return ApiResponse<SendBotResponseDto>.SuccessResponse(
                new SendBotResponseDto
                {
                    BotId     = bot.Id,
                    MeetingId = id,
                    Message   = "Bot is joining the meeting"
                },
                "Bot dispatched successfully",
                202);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to dispatch bot for meeting {MeetingId}", id);
            return ApiResponse<SendBotResponseDto>.FailureResponse(ex.Message, 500);
        }
    }


  

    
}