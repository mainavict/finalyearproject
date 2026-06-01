using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using finalyearproject.Data;
using finalyearproject.DTOs;
using finalyearproject.Models;
using finalyearproject.Services.Interfaces;

namespace finalyearproject.Controllers;

[ApiController]
[Route("api/[controller]")]
public class WebhookController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IRecallAiService _recallAiService;
    private readonly IStorage _storage;
    private readonly ILogger<WebhookController> _logger;

    public WebhookController(
        AppDbContext context,
        IRecallAiService recallAiService,
        IStorage storage,
        ILogger<WebhookController> logger)
    {
        _context         = context;
        _recallAiService = recallAiService;
        _storage         = storage;
        _logger          = logger;
    }

    /// <summary>
    /// Receives Recall.ai webhook events.
    /// When a bot finishes recording, the audio is downloaded from Recall.ai,
    /// uploaded to Backblaze B2, and an AudioFile record is persisted and linked
    /// to the relevant meeting.
    /// </summary>
    [HttpPost("recall")]
    public async Task<IActionResult> RecallWebhook([FromBody] RecallWebhookPayload payload)
    {
        _logger.LogInformation(
            "Recall.ai webhook received. Event: {Event}", Sanitize(payload.Event));

        // Only act on bot.done — the recording is complete at this point
        if (payload.Event != "bot.done")
            return Ok(new { message = $"Event '{payload.Event}' acknowledged, no action required." });

        var botData = payload.Data?.Bot;
        if (botData == null)
        {
            _logger.LogWarning("bot.done webhook received with no bot data.");
            return BadRequest(new { message = "Missing bot data in webhook payload." });
        }

        // ── Find the meeting this bot belongs to ──────────────────────────────
        var meeting = await _context.Meetings
            .FirstOrDefaultAsync(m => m.RecallBotId == botData.Id);

        if (meeting == null)
        {
            _logger.LogWarning(
                "No meeting found for Recall.ai bot {BotId}", Sanitize(botData.Id));
            return NotFound(new { message = $"No meeting found for bot {botData.Id}." });
        }

        // ── Process each recording produced by the bot ────────────────────────
        var recordings = botData.Outputs?.Recordings;
        if (recordings == null || recordings.Count == 0)
        {
            _logger.LogWarning(
                "bot.done for {BotId} but no recordings in payload.", Sanitize(botData.Id));
            return Ok(new { message = "Bot done but no recordings to process." });
        }

        foreach (var recordingRef in recordings)
        {
            await ProcessRecordingAsync(meeting, recordingRef);
        }

        return Ok(new { message = "Webhook processed successfully." });
    }

    // ── Private helper ────────────────────────────────────────────────────────

    private async Task ProcessRecordingAsync(Meeting meeting, RecallRecordingRef recordingRef)
    {
        _logger.LogInformation(
            "Processing Recall.ai recording {RecordingId} for meeting {MeetingId}",
            Sanitize(recordingRef.Id), meeting.Id);

        try
        {
            // 1. Get signed media URLs from Recall.ai
            var media = await _recallAiService.GetRecordingMediaAsync(recordingRef.Id);

            var audioUrl = media.Media?.Audio;
            if (string.IsNullOrEmpty(audioUrl))
            {
                _logger.LogWarning(
                    "No audio URL for recording {RecordingId}", Sanitize(recordingRef.Id));
                return;
            }

            // 2. Stream the audio from Recall.ai
            var (audioStream, contentType, fileName) =
                await _recallAiService.DownloadRecordingAudioAsync(audioUrl);

            // 3. Wrap the stream as IFormFile so IStorage can accept it
            var formFile = new RecallAudioFormFile(audioStream, fileName, contentType);

            // 4. Upload to Backblaze B2
            var fileKey = await _storage.UploadAsync(formFile, "audio");
            var fileUrl = _storage.GetFileUrl(fileKey);

            // 5. Persist AudioFile record
            var audioFile = new AudioFile
            {
                Id          = Guid.NewGuid(),
                MeetingId   = meeting.Id,
                FileName    = fileName,
                FilePath    = fileKey,
                FileUrl     = fileUrl,
                ContentType = contentType,
                FileSize    = audioStream.CanSeek ? audioStream.Length : 0,
                Status      = stutus.pending
            };

            _context.AudioFiles.Add(audioFile);

            // 6. Update meeting with the audio URL and mark as Completed
            meeting.AudioUrl  = fileUrl;
            meeting.Status    = MeetingStatus.Completed;
            meeting.EndedAt   = recordingRef.EndsAt ?? DateTime.UtcNow;
            meeting.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Audio for recording {RecordingId} uploaded to B2. FileKey={FileKey}, MeetingId={MeetingId}",
                Sanitize(recordingRef.Id), fileKey, meeting.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to process recording {RecordingId} for meeting {MeetingId}",
                Sanitize(recordingRef.Id), meeting.Id);
        }
    }

    // ── Security helpers ──────────────────────────────────────────────────────

    /// <summary>Strips newlines and carriage returns to prevent log-forging.</summary>
    private static string Sanitize(string value) =>
        value.Replace("\r", string.Empty)
             .Replace("\n", string.Empty);

    // ── Adapter: wrap a Stream as IFormFile ───────────────────────────────────

    private sealed class RecallAudioFormFile : IFormFile    {
        private readonly Stream _stream;

        public RecallAudioFormFile(Stream stream, string fileName, string contentType)
        {
            _stream     = stream;
            FileName    = fileName;
            ContentType = contentType;
            Name        = "file";
            Length      = stream.CanSeek ? stream.Length : 0;
        }

        public string ContentType { get; }
        public string ContentDisposition => $"form-data; name=\"file\"; filename=\"{FileName}\"";
        public IHeaderDictionary Headers  => new HeaderDictionary();
        public long Length { get; }
        public string Name { get; }
        public string FileName { get; }

        public void CopyTo(Stream target)                             => _stream.CopyTo(target);
        public Task CopyToAsync(Stream target, CancellationToken ct)  => _stream.CopyToAsync(target, ct);
        public Stream OpenReadStream()                                => _stream;
    }
}
