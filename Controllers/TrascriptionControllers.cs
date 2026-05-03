using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using finalyearproject.Data;
using finalyearproject.Models;
using finalyearproject.Services.Interfaces;
using finalyearproject.Wrappers;

namespace finalyearproject.Controllers;
[ApiController]
[Route("api/[controller]")]
public class TrascriptionControllers :ControllerBase
{
    private readonly ITranscriber _transcriber;
    private readonly AppDbContext _context;
    private readonly ILogger<TrascriptionControllers> _logger;
    public TrascriptionControllers(ITranscriber transcriber, AppDbContext context,ILogger<TrascriptionControllers> logger)
    {
        _transcriber = transcriber;
        _context = context;
        _logger = logger;
    }
    
    
    [HttpPost("{id}/transcribe")]
    public async Task<ApiResponse<String>> Transcribe(Guid id)
    {
        _logger.LogInformation("Transcription process started for audio file with id {AudioId}", id);
        var audio = await _context.AudioFiles
            .FirstOrDefaultAsync(x => x.Id == id);

        if (audio == null)
        {
            _logger.LogWarning("Audio file with id {AudioId} not found", id);
            return ApiResponse<string>.FailureResponse("Audio file not found",404);
        }

        try
        {
            audio.Status = stutus.prossesing;

            await _context.SaveChangesAsync();

            var physicalPath = Path.Combine(
                Directory.GetCurrentDirectory(),
                "wwwroot",
                audio.FilePath.TrimStart('/'));

            var transcript =
                await _transcriber.TranscribeAsync(physicalPath);

            var minuteTrascription =new  Transcript()
            {
                MeetingId = audio.MeetingId,
                RawText = transcript,
                WhisperModel = "base",
                CreatedAt = DateTime.UtcNow
            };

            audio.Status = stutus.transcribed;
            
            _logger.LogInformation("Transcription completed for audio file with id {AudioId}", id);
            
            _context.Transcripts.Add(minuteTrascription);
            await _context.SaveChangesAsync();


           

            return  ApiResponse<string>.SuccessResponse(minuteTrascription.RawText,"Transcription completed successfully",200);
        }
        catch (Exception ex)
        {
            audio.Status = stutus.failed;

            await _context.SaveChangesAsync();
            
            _logger.LogError(ex, "Transcription failed for audio file with id {AudioId}", id);

            return ApiResponse<string>.FailureResponse(ex.Message,500);
        }
    }
    
    

}