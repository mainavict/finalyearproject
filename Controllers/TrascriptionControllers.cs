// Controllers/TranscriptionController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using finalyearproject.Data;
using finalyearproject.Models;
using finalyearproject.Services.Interfaces;
using finalyearproject.Wrappers;

namespace finalyearproject.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TranscriptionController : ControllerBase
{
    private readonly ITranscriber _transcriber;
    private readonly IBackblazeB2Service _b2Service;
    private readonly AppDbContext _context;
    private readonly ILogger<TranscriptionController> _logger;

    public TranscriptionController(
        ITranscriber transcriber,
        IBackblazeB2Service b2Service,
        AppDbContext context,
        ILogger<TranscriptionController> logger)
    {
        _transcriber = transcriber;
        _b2Service   = b2Service;
        _context     = context;
        _logger      = logger;
    }

    [HttpPost("{id}/transcribe")]
    public async Task<ApiResponse<string>> Transcribe(Guid id)
    {
        _logger.LogInformation(
            "Transcription started for audio file {AudioId}", id);

        var audio = await _context.AudioFiles
            .FirstOrDefaultAsync(x => x.Id == id);

        if (audio == null)
        {
            _logger.LogWarning("Audio file {AudioId} not found", id);
            return ApiResponse<string>.FailureResponse("Audio file not found", 404);
        }

        try
        {
            audio.Status = stutus.prossesing;
            await _context.SaveChangesAsync();

            string transcript;

            // ── Route: B2 file vs local file ─────────────────────────────────
            if (!string.IsNullOrEmpty(audio.FilePath))
            {
                _logger.LogInformation(
                    "Fetching audio from Backblaze B2: {B2FilePath}", audio.FilePath);

                await using var audioStream =
                    await _b2Service.DownloadFileAsync(audio.FilePath);

                transcript = await _transcriber.TranscribeFromStreamAsync(
                    audioStream,
                    Path.GetFileName(audio.FilePath));
            }
            else
            {
                // Fallback: local file (your existing behaviour)
                var physicalPath = Path.Combine(
                    Directory.GetCurrentDirectory(),
                    "wwwroot",
                    audio.FilePath.TrimStart('/'));

                transcript = await _transcriber.TranscribeAsync(physicalPath);
            }

            // ── Save transcript ───────────────────────────────────────────────
            var minuteTranscription = new Transcript
            {
                MeetingId    = audio.MeetingId,
                RawText      = transcript,
                WhisperModel = "whisper-large-v3",
                CreatedAt    = DateTime.UtcNow
            };

            audio.Status = stutus.transcribed;

            _context.Transcripts.Add(minuteTranscription);
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Transcription completed for audio file {AudioId}", id);

            return ApiResponse<string>.SuccessResponse(
                minuteTranscription.RawText,
                "Transcription completed successfully",
                200);
        }
        catch (Exception ex)
        {
            audio.Status = stutus.failed;
            await _context.SaveChangesAsync();

            _logger.LogError(ex,
                "Transcription failed for audio file {AudioId}", id);

            return ApiResponse<string>.FailureResponse(ex.Message, 500);
        }
    }
    
    
}