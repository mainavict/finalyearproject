using finalyearproject.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using finalyearproject.DTOs;
using finalyearproject.Models;
using finalyearproject.Services.Interfaces;
using finalyearproject.Wrappers;
using finalyearproject.Security;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using  finalyearproject.Extensions;

namespace finalyearproject.Controllers;

[ApiController]
[Route("api/[controller]")] // ✅ Fixed: removed leading slash
[Authorize]
public class MinutesControllers : ControllerBase
{
    private readonly Iminutes _minutesService;
    private readonly AppDbContext _context;
    private readonly ILogger<MinutesControllers> _logger;
    
    public MinutesControllers(Iminutes minutesService, AppDbContext context, ILogger<MinutesControllers> logger)
    {
        _minutesService = minutesService;
        _context = context;
        _logger = logger;
    }
    
    [HttpPost("generate")]
    public async Task<ApiResponse<string>> GenerateMinutes([FromBody] CreateMinutesDto request) // ✅ Fixed: singular name
    {
        if (request.MeetingId == Guid.Empty)
        {
            return ApiResponse<string>.FailureResponse("Meeting id can't be null", statusCode: 400);
        }
        
        var meeting = await _context.Meetings.FindAsync(request.MeetingId);
        if (meeting == null)
        {
            return ApiResponse<string>.FailureResponse("Meeting not found.", statusCode: 404);
        }   
        
        var transcript = await _context.Transcripts.FirstOrDefaultAsync(t => t.MeetingId == request.MeetingId);
        if (transcript == null)
        {
            return ApiResponse<string>.FailureResponse("Transcript file for the meeting not found.", statusCode: 404);
        }
        
        try
        {
            var minutes = await _minutesService.GenerateMinutesAsync(transcript.Id);

            var minute = new Minutes()
            {
                MeetingId = request.MeetingId,
                DraftJson = minutes,
                LlmModel = "Gemini Pro",
                Status = "draft",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            
            _context.Minutes.Add(minute);
            await _context.SaveChangesAsync();
            
            return ApiResponse<string>.SuccessResponse(minutes, "Minutes generated successfully", statusCode: 200);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Error communicating with AI service");
            return ApiResponse<string>.FailureResponse($"Error communicating with the AI service. Details: {ex.Message}", statusCode: 500);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error occurred");
            return ApiResponse<string>.FailureResponse($"An unexpected error occurred. Details: {ex.Message}", statusCode: 500);
        }
    }

    // ✅ NEW: Get full meeting data with parsed minutes
    [HttpGet("{meetingId}")]
    public async Task<IActionResult> GetMeetingMinutes(Guid meetingId)
    {
        try
        {
            var userId = User.GetUserId();
            
            // Fetch meeting with all related data
            var meeting = await _context.Meetings
                .Include(m => m.Minutes)
                .Include(m => m.Transcript)
                .Include(m => m.Participants)
                .FirstOrDefaultAsync(m => m.Id == meetingId && m.CreatorId == userId);

            if (meeting == null)
            {
                return NotFound(ApiResponse<object>.FailureResponse("Meeting not found", 404));
            }

            // Parse minutes JSON
            MeetingMinutesData? minutesData = null;
            if (meeting.Minutes != null && !string.IsNullOrWhiteSpace(meeting.Minutes.DraftJson))
            {
                try
                {
                    var jsonOptions = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    };
                    minutesData = JsonSerializer.Deserialize<MeetingMinutesData>(meeting.Minutes.DraftJson, jsonOptions);
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "Failed to parse minutes JSON for meeting {MeetingId}", meetingId);
                }
            }

            // Map participants
            var participants = meeting.Participants?.Select(p => new ParticipantDto
            {
                Id = p.Id,
                Name = p.Name,
                Email = p.Email,
                Role = p.Role,
                IsHost = p.IsHost,
                JoinedAt = p.JoinedAt,
                LeftAt = p.LeftAt,
                DurationSeconds = p.DurationSeconds
            }).ToList() ?? new List<ParticipantDto>();

            // Build response
            var response = new MinutesResponseDto
            {
                MeetingId = meeting.Id,
                MeetingTitle = meeting.Title,
                MeetingDate = meeting.ScheduledAt?.ToString("yyyy-MM-dd"),
                Description = meeting.Description,
                ScheduledAt = meeting.ScheduledAt,
                EndedAt = meeting.EndedAt,
                AudioDurationSecs = meeting.AudioDurationSecs,
                AudioUrl = meeting.AudioUrl,
                Minutes = minutesData,
                Transcript = meeting.Transcript?.RawText,
                Participants = participants
            };

            return Ok(ApiResponse<MinutesResponseDto>.SuccessResponse(response, "Meeting data retrieved successfully", 200));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching meeting minutes for {MeetingId}", meetingId);
            return StatusCode(500, ApiResponse<object>.FailureResponse("An error occurred", 500));
        }
    }
    
    [HttpGet("all")]
    public async Task<ApiResponse<List<Minutes>>> GetAllMinutes()
    {
        var response = await _minutesService.GetAllMinutesAsync();
        return response;
    }
    
    [HttpPut("{meetingId}/minutes")]
    public async Task<IActionResult> UpdateMinutes(Guid meetingId, [FromBody] UpdateMinutesRequest request)
    {
        try
        {
            var userId = User.GetUserId();
            
            var meeting = await _context.Meetings
                .Include(m => m.Minutes)
                .FirstOrDefaultAsync(m => m.Id == meetingId && m.CreatorId == userId);
    
            if (meeting == null)
            {
                return NotFound(ApiResponse<object>.FailureResponse("Meeting not found", 404));
            }
    
            // Serialize the minutes data to JSON
            var minutesJson = JsonSerializer.Serialize(request.Minutes, new JsonSerializerOptions
            {
                WriteIndented = false
            });
    
            if (meeting.Minutes == null)
            {
                meeting.Minutes = new Minutes
                {
                    MeetingId = meetingId,
                    DraftJson = minutesJson,
                    Status = "draft",
                    LlmModel = "Manual Edit",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                _context.Minutes.Add(meeting.Minutes);
            }
            else
            {
                meeting.Minutes.DraftJson = minutesJson;
                meeting.Minutes.UpdatedAt = DateTime.UtcNow;
            }
    
            await _context.SaveChangesAsync();
    
            return Ok(ApiResponse<object>.SuccessResponse(null, "Minutes updated successfully", 200));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating minutes for meeting {MeetingId}", meetingId);
            return StatusCode(500, ApiResponse<object>.FailureResponse("An error occurred", 500));
        }
    }
}