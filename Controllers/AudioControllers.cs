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

}