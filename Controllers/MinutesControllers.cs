using Microsoft.AspNetCore.Mvc;
using finalyearproject.DTOs;
using finalyearproject.Services.Interfaces;
using finalyearproject.Wrappers;
using finalyearproject.Models;

namespace finalyearproject.Controllers;
[ApiController]
[Route("/api[controller]")]
public class MinutesControllers: ControllerBase
{
    private readonly Iminutes _minutesService;
    
    public  MinutesControllers(Iminutes minutesService)
    {
        _minutesService = minutesService;
    }
    
    [HttpPost("generate")]
    public async Task<IActionResult> GenerateMinutes([FromBody]  CreateMinutesDtos request)
    {
        if (string.IsNullOrWhiteSpace(request.Transcript))
        {
            return BadRequest("Transcript cannot be empty.");
        }

        try
        {
            var minutes = await _minutesService.GenerateMinutesAsync(request.Transcript);
            return Ok(new { Minutes = minutes });
        }
        catch (HttpRequestException ex)
        {
            // TEMPORARY: Expose the real error for debugging
            return StatusCode(500, $"Error communicating with the AI service. Details: {ex.Message}");
        }
        catch (Exception ex)
        {
            // Log the error here
            return StatusCode(500, "An unexpected error occurred.");
        }
    }

    
}