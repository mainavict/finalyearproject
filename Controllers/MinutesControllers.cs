using finalyearproject.Data;
using Microsoft.AspNetCore.Mvc;
using finalyearproject.DTOs;
using finalyearproject.Models;
using finalyearproject.Services.Interfaces;
using finalyearproject.Wrappers;

namespace finalyearproject.Controllers;
[ApiController]
[Route("/api[controller]")]
public class MinutesControllers: ControllerBase
{
    private readonly Iminutes _minutesService;
    private readonly AppDbContext  _context;
    
    public  MinutesControllers(Iminutes minutesService, AppDbContext context)
    {
        _minutesService = minutesService;
        _context = context;
    }
    
    [HttpPost("generate")]
    public async Task<ApiResponse<string>> GenerateMinutes([FromBody]  CreateMinutesDtos request)
    {
        if (string.IsNullOrWhiteSpace(request.MeetingId.ToString()) )
        {
            return ApiResponse<string >.FailureResponse("Meeting id can't be  null ",statusCode:400);
        }
        var  meeting  = request.MeetingId;
         var meetingExists = await _context.Meetings.FindAsync(meeting);
        if (meetingExists == null)        {
            return ApiResponse<string>.FailureResponse("Meeting not found.",statusCode:404);
        }   
        
        var transcript = _context.Transcripts.FirstOrDefault(t => t.MeetingId == meeting);
        if (transcript == null)        {
            return ApiResponse<string>.FailureResponse("Transcript file for the meeting not found.",  statusCode:404);
        }
        
        

        try
        {
            var minutes = await _minutesService.GenerateMinutesAsync(transcript.Id);

            var minute = new Minutes()
            {
                MeetingId = meeting,
                DraftJson = minutes,
                LlmModel = "Gemini Pro",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow

            };
            
            _context.Minutes.Add(minute);
            await _context.SaveChangesAsync();
            return ApiResponse<string>.SuccessResponse(minutes,"message json craeted",statusCode:200);
        }
        catch (HttpRequestException ex)
        {
            // TEMPORARY: Expose the real error for debugging
            
            return  ApiResponse<string>.FailureResponse($"Error communicating with the AI service. Details: {ex.Message}",statusCode:500);
        }
        catch (Exception ex)
        {
            // Log the error here
            return ApiResponse<string>.FailureResponse($"An unexpected error occurred. Details: {ex.Message}",statusCode:500);
        }
    }


    [HttpGet("{meetingId}")]
    public async Task<ApiResponse<Minutes>> GetMinutesByMeetingId(Guid meetingId)
    {
        if (meetingId == Guid.Empty)
        {
            return ApiResponse<Minutes>.FailureResponse("Meeting ID cannot be empty.", statusCode: 400);
        }

        var responce = await _minutesService.GetMinutesByMeetingIdAsync(meetingId);

        return responce;

    }
    
    [HttpGet]
    public async Task<ApiResponse<List<Minutes>>> GetAllMinutes()
    {
        var responce = await _minutesService.GetAllMinutesAsync();

        return responce;

     }
}