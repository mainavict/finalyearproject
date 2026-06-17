using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using finalyearproject.DTOs;
using finalyearproject.Security;
using finalyearproject.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using finalyearproject.Data;
using finalyearproject.Models;
using finalyearproject.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace finalyearproject.Controllers;

[ApiController]
[Route("api/webhook/recall")]
public class RecallWebhookController : ControllerBase
{
    private readonly IRecallAiService _recallService;
    private readonly IStorage _storage;
    private readonly AppDbContext _context;
    private readonly IHubContext<NotificationHub> _hubContext;
    private readonly IServiceProvider _serviceProvider;
    private readonly IParticipantService _participantService; 
    private readonly string _webhookSecret;
    private readonly ILogger<RecallWebhookController> _logger;

    public RecallWebhookController(
        IRecallAiService recallService,
        IStorage storage,
        AppDbContext context,
        IHubContext<NotificationHub> hubContext,
        IServiceProvider serviceProvider,
        IParticipantService participantService, 
        IConfiguration config,
        ILogger<RecallWebhookController> logger)
    {
        _recallService = recallService;
        _storage = storage;
        _context = context;
        _hubContext = hubContext;
        _serviceProvider = serviceProvider;
        _participantService = participantService; 
        _webhookSecret = config["RecallAi:WebhookSecret"] 
            ?? throw new InvalidOperationException("RecallAi:WebhookSecret is missing in appsettings.json.");
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> HandleBotStatusWebhook(CancellationToken ct)
    {
        // 1. Read verification headers
        var msgId = Request.Headers["Webhook-Id"].ToString();
        if (string.IsNullOrEmpty(msgId)) msgId = Request.Headers["svix-id"].ToString();

        var msgTimestamp = Request.Headers["Webhook-Timestamp"].ToString();
        if (string.IsNullOrEmpty(msgTimestamp)) msgTimestamp = Request.Headers["svix-timestamp"].ToString();

        var msgSignature = Request.Headers["Webhook-Signature"].ToString();
        if (string.IsNullOrEmpty(msgSignature)) msgSignature = Request.Headers["svix-signature"].ToString();

        if (string.IsNullOrEmpty(msgId) || string.IsNullOrEmpty(msgTimestamp) || string.IsNullOrEmpty(msgSignature))
        {
            return BadRequest("Missing verification headers");
        }

        // 2. Read the RAW body
        using var reader = new StreamReader(Request.Body);
        var rawPayload = await reader.ReadToEndAsync(ct);

        // 3. Verify Signature
        if (!RecallWebhookVerifier.VerifySignature(_webhookSecret, msgId, msgTimestamp, rawPayload, msgSignature))
        {
            return Unauthorized("Invalid signature");
        }

        // 4. Deserialize payload
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var payload = JsonSerializer.Deserialize<RecallWebhookPayload>(rawPayload, options);
        
        if (payload?.Data?.Bot == null)
        {
            return BadRequest("Invalid payload structure");
        }

        var bot = payload.Data.Bot;
        _logger.LogInformation("Received webhook event: {Event} for Bot: {BotId}", payload.Event, bot.Id);

        // 5. Find the meeting by RecallBotId
        var meeting = await _context.Meetings
            .FirstOrDefaultAsync(m => m.RecallBotId == bot.Id, ct);

        if (meeting == null)
        {
            _logger.LogWarning("Meeting not found for bot {BotId}", bot.Id);
            return Ok(new { received = true });
        }

        // ==========================================
        // 6. ROUTE THE EVENTS
        // ==========================================

        // EVENT 1: Participant events are ready - Extract and save participants
        if (payload.Event == "participant_events.done")
        {
            _logger.LogInformation("Participant events done for meeting: {MeetingId}", meeting.Id);
            
            // Fire and forget: Extract and save participants
            _ = ProcessParticipantsAsync(payload, meeting.Id);
        }
        // EVENT 2: Recording is ready to download
        else if (payload.Event == "recording.done")
        {
            _logger.LogInformation(" Recording is done! Starting audio download for Meeting: {MeetingId}", meeting.Id);

            // Extract recording ID robustly
            string? recordingId = null;

            if (bot.Outputs?.Recordings != null && bot.Outputs.Recordings.Count > 0)
                recordingId = bot.Outputs.Recordings[0].Id;
            else if (payload.Data.Recording?.Id != null)
                recordingId = payload.Data.Recording.Id;
            else if (payload.Data.Outputs?.Recording?.Id != null)
                recordingId = payload.Data.Outputs.Recording.Id;

            if (string.IsNullOrEmpty(recordingId))
            {
                _logger.LogWarning("Could not find recording ID in webhook payload.");
                return Ok(new { received = true });
            }

            // Fire and forget: Download, upload to B2, and create AudioFile record
            _ = ProcessRecordingAsync(recordingId, meeting.Id);
        }
        // EVENT 3: Bot has left the call
        else if (payload.Event == "bot.call_ended")
        {
            _logger.LogInformation(" Bot left the meeting: {MeetingId}", meeting.Id);
            meeting.EndedAt = DateTime.UtcNow;
            meeting.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(ct);
        }
        // EVENT 4: Bot has completely finished all processing
        else if (payload.Event == "bot.done")
        {
            _logger.LogInformation("🎉 Bot completely finished for meeting: {MeetingId}", meeting.Id);
            
            meeting.Status = MeetingStatus.Completed;
            meeting.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(ct);

            // Send SignalR notification to the user
            var userId = meeting.CreatorId.ToString();
            await _hubContext.Clients.User(userId).SendAsync("ReceiveTranscriptionUpdate", new
            {
                meetingtitle = meeting.Title,
                meetingId = meeting.Id,
                status = "Completed",
                message = "Your meeting recording has been saved and is ready for processing!"
            });
        }

        // 7. Always return 200 OK quickly
        return Ok(new { received = true });
    }

    // ==========================================
    // BACKGROUND TASK: EXTRACT & SAVE PARTICIPANTS
    // ==========================================
    private async Task ProcessParticipantsAsync(RecallWebhookPayload payload, Guid meetingId)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var participantService = scope.ServiceProvider.GetRequiredService<IParticipantService>();
        var recallService = scope.ServiceProvider.GetRequiredService<IRecallAiService>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<RecallWebhookController>>();

        try
        {
            logger.LogInformation("Starting participant extraction for meeting: {MeetingId}", meetingId);

            // Get the recording ID to fetch participant events
            var bot = payload.Data?.Bot;
            string? recordingId = null;

            if (bot?.Outputs?.Recordings != null && bot.Outputs.Recordings.Count > 0)
                recordingId = bot.Outputs.Recordings[0].Id;
            else if (payload.Data?.Recording?.Id != null)
                recordingId = payload.Data.Recording.Id;
            else if (payload.Data?.Outputs?.Recording?.Id != null)
                recordingId = payload.Data.Outputs.Recording.Id;

            if (string.IsNullOrEmpty(recordingId))
            {
                logger.LogWarning("Could not find recording ID for participant extraction");
                return;
            }

            // Fetch participant events from Recall.ai
            var participantEvents = await recallService.GetParticipantEventsAsync(recordingId);

            if (participantEvents == null || !participantEvents.Any())
            {
                logger.LogWarning("No participant events found for recording {RecordingId}", recordingId);
                return;
            }

            logger.LogInformation("Found {Count} participant events", participantEvents.Count);

            // Convert to RecallParticipant format
            var recallParticipants = participantEvents.Select(pe => new RecallParticipant
            {
                Name = pe.Participant?.Label ?? "Unknown",
                Email = null, // Recall.ai doesn't provide emails directly
                Role = pe.IsHost ? "Host" : "Attendee",
                IsHost = pe.IsHost,
                JoinedAt = pe.CreatedAt,
                LeftAt = pe.LeftAt,
                DurationSeconds = pe.DurationSeconds ?? 0
            }).ToList();

            // Save participants using the service
            var result = await participantService.AddParticipantsFromRecallAsync(meetingId, recallParticipants);

            if (result.Success)
            {
                logger.LogInformation("Successfully saved {Count} participants for meeting {MeetingId}", 
                    recallParticipants.Count, meetingId);
            }
            else
            {
                logger.LogWarning("Failed to save participants: {Message}", result.Message);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Critical error processing participants for meeting {MeetingId}", meetingId);
        }
    }

    // ==========================================
    // BACKGROUND TASK: DOWNLOAD & UPLOAD AUDIO
    // ==========================================
    private async Task ProcessRecordingAsync(string recordingId, Guid meetingId)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var recallService = scope.ServiceProvider.GetRequiredService<IRecallAiService>();
        var storage = scope.ServiceProvider.GetRequiredService<IStorage>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<RecallWebhookController>>();

        try
        {
            logger.LogInformation("Starting background download for recording: {RecordingId}", recordingId);

            // UPDATED: Check for both audio and video URLs
            RecallRecordingMedia? media = null;
            int maxRetries = 3;
            
            for (int i = 0; i < maxRetries; i++)
            {
                media = await recallService.GetRecordingMediaAsync(recordingId);
                
                // Check if we have EITHER audio OR video
                if (media?.Media?.Audio != null || media?.Media?.Video != null)
                {
                    logger.LogInformation("Got media URL on attempt {Attempt}", i + 1);
                    break;
                }
                
                logger.LogWarning("No media URL yet (attempt {Attempt}/{MaxRetries}). Waiting 10 seconds...", i + 1, maxRetries);
                await Task.Delay(TimeSpan.FromSeconds(10));
            }
            
            // Use video if audio isn't available
            string? downloadUrl = media?.Media?.Audio ?? media?.Media?.Video;
            
            if (string.IsNullOrEmpty(downloadUrl))
            {
                logger.LogError("Failed to get media URL after {MaxRetries} attempts for recording {RecordingId}", maxRetries, recordingId);
                return;
            }

            logger.LogInformation("Downloading media from Recall.ai: {Url}", downloadUrl);

            var (mediaStream, contentType, fileName) = await recallService.DownloadRecordingAudioAsync(downloadUrl);

            logger.LogInformation("Uploading to Backblaze B2: {FileName}", fileName);

            string fileKey;
            await using (mediaStream)
            {
                fileKey = await storage.UploadStreamAsync(mediaStream, fileName, contentType, "audio");
            }
            
            var backblazeUrl = storage.GetFileUrl(fileKey);
            logger.LogInformation("Media uploaded to Backblaze: {Url}", backblazeUrl);

            var audioFile = new AudioFile
            {
                MeetingId = meetingId,
                FileName = fileName,
                FilePath = fileKey,
                FileUrl = backblazeUrl,
                ContentType = contentType,
                FileSize = 0,
                Status = stutus.pending 
            };

            context.AudioFiles.Add(audioFile);
            await context.SaveChangesAsync();

            var meeting = await context.Meetings.FindAsync(meetingId);
            if (meeting != null)
            {
                meeting.AudioUrl = backblazeUrl;
                meeting.AudioMimeType = contentType;
                meeting.UpdatedAt = DateTime.UtcNow;
                await context.SaveChangesAsync();
            }

            logger.LogInformation("AudioFile created with status 'pending'. TranscriptionWorker will pick it up automatically!");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Critical error processing recording for meeting {MeetingId}", meetingId);
        }
    }
}