using Microsoft.EntityFrameworkCore;
using finalyearproject.Data;
using finalyearproject.Models;
using finalyearproject.Services.Interfaces;

namespace finalyearproject.Services.Implementation;

public class AudioProcessingService : IAudioProcessingService
{
    private readonly AppDbContext _context;
    private readonly ITranscriber _transcriber; 
    private readonly IBackblazeB2Service _b2Service;
    private readonly Iminutes _minutesService; 
    private readonly ILogger<AudioProcessingService> _logger;

    public AudioProcessingService(
        AppDbContext context, 
        ITranscriber transcriber, 
        IBackblazeB2Service b2Service, 
        Iminutes minutesService, 
        ILogger<AudioProcessingService> logger)
    {
        _context = context;
        _transcriber = transcriber;
        _b2Service = b2Service;
        _minutesService = minutesService; 
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

            string transcriptText;

            // 1. Get Audio and Transcribe
            if (!string.IsNullOrEmpty(audio.FilePath))
            {
                await using var audioStream = await _b2Service.DownloadFileAsync(audio.FilePath);
                transcriptText = await _transcriber.TranscribeFromStreamAsync(audioStream, Path.GetFileName(audio.FilePath));
            }
            else
            {
                var physicalPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", audio.FilePath.TrimStart('/'));
                transcriptText = await _transcriber.TranscribeAsync(physicalPath);
            }

            // 2. Save Transcript to DB
            var existingTranscript = await _context.Transcripts.FirstOrDefaultAsync(t => t.MeetingId == audio.MeetingId);
            Transcript transcriptRecord;

            if (existingTranscript != null)
            {
                existingTranscript.RawText = transcriptText;
                existingTranscript.WhisperModel = "whisper-large-v3";
                existingTranscript.CreatedAt = DateTime.UtcNow;
                transcriptRecord = existingTranscript;
            }
            else
            {
                transcriptRecord = new Transcript
                {
                    MeetingId = audio.MeetingId,
                    RawText = transcriptText,
                    WhisperModel = "whisper-large-v3",
                    CreatedAt = DateTime.UtcNow
                };
                _context.Transcripts.Add(transcriptRecord);
            }

            // Save transcript so it gets an ID for the Gemini service
            await _context.SaveChangesAsync(); 
            
            _logger.LogInformation("Transcription complete. Now generating minutes for Meeting {MeetingId}...", audio.MeetingId);

            // 3. Generate Minutes using Gemini (Returns a raw JSON string)
            string minutesJson = await _minutesService.GenerateMinutesAsync(transcriptRecord.Id);
            
            // Optional: Clean up markdown blocks if Gemini accidentally adds them
            minutesJson = CleanJsonString(minutesJson);

            // 4. Save the RAW JSON directly to the database (Your approach!)
            var existingMinutes = await _context.Minutes.FirstOrDefaultAsync(m => m.MeetingId == audio.MeetingId);

            if (existingMinutes != null)
            {
                // Update existing
                existingMinutes.DraftJson = minutesJson;
                existingMinutes.LlmModel = "Gemini Pro";
                existingMinutes.UpdatedAt = DateTime.UtcNow;
                _logger.LogInformation("Updated existing minutes for Meeting {MeetingId}", audio.MeetingId);
            }
            else
            {
                // Insert new
                var newMinutes = new Minutes()
                {
                    MeetingId = audio.MeetingId,
                    DraftJson = minutesJson,
                    LlmModel = "Gemini Pro",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                _context.Minutes.Add(newMinutes);
                _logger.LogInformation("Created new minutes for Meeting {MeetingId}", audio.MeetingId);
            }

            await _context.SaveChangesAsync();

            // 5. Mark Audio as Fully Complete
            audio.Status = stutus.transcribed; 
            await _context.SaveChangesAsync();
            
            _logger.LogInformation("Successfully processed audio and generated minutes for {AudioId}", audioId);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
        {
            // Smart retry for Groq rate limits
            _logger.LogWarning("Groq API rate limit hit (429). Reverting audio {AudioId} to pending.", audioId);
            audio.Status = stutus.pending; 
            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            audio.Status = stutus.failed;
            await _context.SaveChangesAsync();
            _logger.LogError(ex, "Processing failed for audio {AudioId}", audioId);
        }
    }

    // Helper to strip markdown if Gemini misbehaves
    private string CleanJsonString(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return "{}";
        json = json.Trim();
        if (json.StartsWith("```json")) json = json.Substring(7);
        if (json.StartsWith("```")) json = json.Substring(3);
        if (json.EndsWith("```")) json = json.Substring(0, json.Length - 3);
        return json.Trim();
    }
}