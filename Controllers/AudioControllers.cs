using Microsoft.AspNetCore.Mvc;
using finalyearproject.Services.Interfaces;
using finalyearproject.DTOs;
using finalyearproject.Wrappers;
using  finalyearproject.Models;

namespace finalyearproject.Controllers;
[ApiController]
[Route("api/[controller]")]
public class AudioControllers : ControllerBase
{
    private  readonly IAudio _audio;
    public AudioControllers(IAudio audio)
    {
        _audio = audio;
    }
    
    
    [HttpPost("upload")]
    [DisableRequestSizeLimit]
    public async Task<ApiResponse<AudioFile>> Upload(Guid meetingId,IFormFile file)
    {
        var result = await _audio.UploadAsync(meetingId,file);

        return result;
    }

    [HttpGet("user/{id}")]
    public async Task<ApiResponse<List<AudioFile>>> GetAllAudioFileByUserIdAsync(Guid id)
    {
        var result = await _audio.GetAllAudioFileByUserIdAsync(id);

        return result;
    }

    [HttpGet]
    public async Task<ApiResponse<List<AudioFile>>> GetAllAudioFilesAsync()
    {
        var  result =  await  _audio.GetAllAudioFilesAsync();
        return result;
    }

    [HttpGet ("meeting/{meetingId}")]
    public async Task<ApiResponse<AudioFile>> GetAudioByMeetingIdAsync(Guid meetingId)
    {
        var  result =  await _audio.GetAudioByMeetingIdAsync(meetingId);
        return result;
    }
    
    

}