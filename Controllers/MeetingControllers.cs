using Microsoft.AspNetCore.Mvc;
using finalyearproject.Services.Interfaces;
using finalyearproject.DTOs;
using finalyearproject.Wrappers;
using finalyearproject.Data;
using finalyearproject.Models;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Authorization;
using   finalyearproject.Extensions;


namespace finalyearproject.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MeetingControllers : ControllerBase
{

    private readonly Imeeting _meetingService;
    private readonly IRecallAiService _recallService;
    private readonly AppDbContext _context;
    private readonly ILogger<MeetingControllers> _logger;

    public MeetingControllers(
        Imeeting meetingService,
        IRecallAiService recallAiService,
        AppDbContext context,
        ILogger<MeetingControllers> logger)
    {
        _meetingService = meetingService;
        _recallService = recallAiService;
        _context = context;
        _logger = logger;
    }



    [HttpPost]
    public async Task<ApiResponse<ResponseMeetingDto>> CreateMeeting([FromBody] CreateMeetingDto createDto)
    {
        Guid currentUserId = User.GetUserId(); 
        
        var result = await _meetingService.CreateMeetingAsync(createDto, currentUserId );
        return result;
    }



    [HttpGet]
    public async Task<ApiResponse<IEnumerable<ResponseMeetingDto>>> GetAllMeetingsAsync()
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

    [HttpGet("my-meetings")]

    public async Task<ApiResponse<IEnumerable<ResponseMeetingDto>>> GetMyMeetingsAsync()
    {
        
        Guid currentUserId = User.GetUserId(); 
        var result = await _meetingService.GetMeetingsByUserIdAsync(currentUserId );
        return result;
    }

    [HttpPut]
    public async Task<ApiResponse<ResponseMeetingDto>> UpdateMeetingAsync(Guid meetingId, UpdateMeetingDto updateDto
        )
    {
        Guid currentUserId = User.GetUserId(); 
        var result = await _meetingService.UpdateMeetingAsync(meetingId, updateDto, currentUserId);
        return result;
    }

    [HttpDelete("{id}")]
    public async Task<ApiResponse<bool>> DeleteMeetingAsync(Guid meetingId)
    {
        Guid currentUserId = User.GetUserId(); 
        
        var result = await _meetingService.DeleteMeetingAsync(meetingId, currentUserId);
        return result;
    }



    [HttpPost("start")]
    public async Task<IActionResult> StartRecording([FromBody] StartMeetingRequest request, CancellationToken ct)
    {
        try
        {
            // 1. Map frontend request to Recall.ai DTO
            var recallRequest = new RecallBotRequest
            {
                MeetingUrl = request.MeetingUrl,
                BotName = "Minutes Assistant",
                RecordingConfig = new RecallRecordingConfig
                {
                    Transcript = new RecallTranscriptConfig
                    {
                        Provider = new RecallTranscriptProvider
                        {
                            RecallAiStreaming = new RecallAiStreamingConfig()
                        }
                    }
                }
            };
            // 2. Call the service
            var response = await _recallService.CreateBotAsync(recallRequest, ct);

            if (response == null || string.IsNullOrEmpty(response.Id))
            {
                return StatusCode(500, "Failed to create bot. No response from Recall.ai.");
            }

            // 3. Return success to frontend
            return Ok(new SendBotResponseDto(
                BotId: response.Id,
                MeetingId: request.InternalMeetingId,
                Message: "Bot is joining the meeting successfully."
            ));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting meeting recording");
            return StatusCode(500, "An internal error occurred while starting the recording.");
        }
    }
}