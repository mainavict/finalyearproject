using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using finalyearproject.Services.Interfaces;
using finalyearproject.Models;
using finalyearproject.Security;

namespace finalyearproject.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ParticipantControllers : ControllerBase
{
    private readonly IParticipantService _participantService;
    private readonly ILogger<ParticipantControllers> _logger;

    public ParticipantControllers(
        IParticipantService participantService,
        ILogger<ParticipantControllers> logger)
    {
        _participantService = participantService;
        _logger = logger;
    }

    [HttpGet("meeting/{meetingId}")]
    public async Task<IActionResult> GetParticipants(Guid meetingId)
    {
        var result = await _participantService.GetParticipantsByMeetingIdAsync(meetingId);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPost("meeting/{meetingId}")]
    public async Task<IActionResult> AddParticipant(Guid meetingId, [FromBody] Participant participant)
    {
        var result = await _participantService.AddParticipantAsync(meetingId, participant);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPut("{participantId}")]
    public async Task<IActionResult> UpdateParticipant(Guid participantId, [FromBody] Participant participant)
    {
        var result = await _participantService.UpdateParticipantAsync(participantId, participant);
        return StatusCode(result.StatusCode, result);
    }

    [HttpDelete("{participantId}")]
    public async Task<IActionResult> DeleteParticipant(Guid participantId)
    {
        var result = await _participantService.DeleteParticipantAsync(participantId);
        return StatusCode(result.StatusCode, result);
    }
}