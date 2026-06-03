using Microsoft.EntityFrameworkCore;
using finalyearproject.Data;
using finalyearproject.Models;
using finalyearproject.Services.Interfaces;
namespace finalyearproject.Services.Implementation;
public interface IAudioProcessingService
{
    Task ProcessAudioAsync(Guid audioId); // <--- THIS MUST BE HERE
}
public class AudioProcessingService: IAudioProcessingService
{
    private readonly AppDbContext _context;
    private readonly ITranscriber _transcriber; // Injects your GroqTranscriptionService
    private readonly IBackblazeB2Service _b2Service;
    private readonly ILogger<AudioProcessingService> _logger;
    
    public AudioProcessingService(
        AppDbContext context, 
        ITranscriber transcriber, 
        IBackblazeB2Service b2Service, 
        ILogger<AudioProcessingService> logger)
    {
        _context = context;
        _transcriber = transcriber;
        _b2Service = b2Service;
        _logger = logger;
    }
      public async Task ProcessAudioAsync(Guid audioId)
    {
        var audio = await _context.AudioFiles.FirstOrDefaultAsync(x => x.Id == audioId);
        if (audio == null) return;

        try
        {
            audio.Status = stutus.prossesing; 
            await _context.SaveChangesAsync();

            string transcript;

            if (!string.IsNullOrEmpty(audio.FilePath))
            {
                await using var audioStream = await _b2Service.DownloadFileAsync(audio.FilePath);
                transcript = await _transcriber.TranscribeFromStreamAsync(audioStream, Path.GetFileName(audio.FilePath));
            }
            else
            {
                var physicalPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", audio.FilePath.TrimStart('/'));
                transcript = await _transcriber.TranscribeAsync(physicalPath);
            }

            // 🔧 THE FIX: Check if a transcript already exists for this meeting
            var existingTranscript = await _context.Transcripts
                .FirstOrDefaultAsync(t => t.MeetingId == audio.MeetingId);

            if (existingTranscript != null)
            {
                // If it exists, UPDATE it instead of adding a new one
                _logger.LogInformation("Updating existing transcript for meeting {MeetingId}", audio.MeetingId);
                existingTranscript.RawText = transcript;
                existingTranscript.WhisperModel = "whisper-large-v3";
                existingTranscript.CreatedAt = DateTime.UtcNow;
            }
            else
            {
                // If it doesn't exist, INSERT a new one
                _logger.LogInformation("Creating new transcript for meeting {MeetingId}", audio.MeetingId);
                _context.Transcripts.Add(new Transcript
                {
                    MeetingId = audio.MeetingId,
                    RawText = transcript,
                    WhisperModel = "whisper-large-v3",
                    CreatedAt = DateTime.UtcNow
                });
            }

            audio.Status = stutus.transcribed;
            await _context.SaveChangesAsync();
            
            _logger.LogInformation("Successfully transcribed audio {AudioId}", audioId);
        }
        catch (Exception ex)
        {
            audio.Status = stutus.failed;
            await _context.SaveChangesAsync();
            _logger.LogError(ex, "Transcription failed for audio {AudioId}", audioId);
        }
    }
}