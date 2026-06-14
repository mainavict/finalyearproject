using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using finalyearproject.DTOs;
using finalyearproject.Security;
using finalyearproject.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace finalyearproject.Controllers;

[ApiController]
[Route("api/webhook/recall")]
public class RecallWebhookController : ControllerBase
{
    private readonly IRecallAiService _recallService;
    private readonly string _webhookSecret;
    private readonly ILogger<RecallWebhookController> _logger;

    public RecallWebhookController(
        IRecallAiService recallService,
        IConfiguration config,
        ILogger<RecallWebhookController> logger)
    {
        _recallService = recallService;
        _webhookSecret = config["RecallAi:WebhookSecret"] 
            ?? throw new InvalidOperationException("RecallAi:WebhookSecret is missing in appsettings.json.");
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> HandleBotStatusWebhook(CancellationToken ct)
    {
        // 1. Read verification headers using .ToString() (The safest way in ASP.NET Core)
        // It checks the exact casing Recall.ai sent, and falls back to lowercase svix- if needed.
        var msgId = Request.Headers["Webhook-Id"].ToString();
        if (string.IsNullOrEmpty(msgId)) msgId = Request.Headers["svix-id"].ToString();

        var msgTimestamp = Request.Headers["Webhook-Timestamp"].ToString();
        if (string.IsNullOrEmpty(msgTimestamp)) msgTimestamp = Request.Headers["svix-timestamp"].ToString();

        var msgSignature = Request.Headers["Webhook-Signature"].ToString();
        if (string.IsNullOrEmpty(msgSignature)) msgSignature = Request.Headers["svix-signature"].ToString();

        if (string.IsNullOrEmpty(msgId) || string.IsNullOrEmpty(msgTimestamp) || string.IsNullOrEmpty(msgSignature))
        {
            _logger.LogWarning("Missing verification headers. Found -> ID: {Id}, Timestamp: {Ts}, Signature: {Sig}", 
                string.IsNullOrEmpty(msgId) ? "MISSING" : "OK",
                string.IsNullOrEmpty(msgTimestamp) ? "MISSING" : "OK",
                string.IsNullOrEmpty(msgSignature) ? "MISSING" : "OK");
            
            return BadRequest("Missing verification headers");
        }

        // 2. Read the RAW body (CRITICAL: Do NOT use [FromBody] here)
        using var reader = new StreamReader(Request.Body);
        var rawPayload = await reader.ReadToEndAsync(ct);

        // 3. Verify Signature
        if (!RecallWebhookVerifier.VerifySignature(_webhookSecret, msgId, msgTimestamp, rawPayload, msgSignature))
        {
            _logger.LogWarning("Invalid webhook signature detected from IP: {RemoteIp}", HttpContext.Connection.RemoteIpAddress);
            return Unauthorized("Invalid signature");
        }

        // 4. Deserialize payload (now that it's verified)
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var payload = JsonSerializer.Deserialize<RecallWebhookPayload>(rawPayload, options);
        
        if (payload?.Data?.Bot == null)
        {
            return BadRequest("Invalid payload structure");
        }

        var bot = payload.Data.Bot;
        _logger.LogInformation("Received webhook event: {Event} for Bot: {BotId}", payload.Event, bot.Id);

        // 5. Handle "Bot Finished" Event
        // 5. Handle the "Recording Done" Event
        if (payload.Event == "recording.done" && bot.Outputs?.Recordings?.Count > 0)
        {
            var recordingId = bot.Outputs.Recordings[0].Id;
            _logger.LogInformation("✅ Recording is done! Fetching media for recording: {RecordingId}", recordingId);

            try
            {
                // Trigger recording fetch logic
                var media = await _recallService.GetRecordingMediaAsync(recordingId, ct);
                
                if (media?.Media != null)
                {
                    _logger.LogInformation("🎉 Successfully fetched recording media!");
                    _logger.LogInformation("   🎵 Audio URL: {AudioUrl}", media.Media.Audio);
                    _logger.LogInformation("   🎥 Video URL: {VideoUrl}", media.Media.Video);
                    
                    // TODO: Save these URLs to your database here!
                    // e.g., await _dbContext.Meetings.UpdateRecordingUrlsAsync(bot.Id, media.Media.Audio, media.Media.Video);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch media for recording {RecordingId}", recordingId);
            }
        }
        else if (payload.Event == "transcript.done")
        {
            _logger.LogInformation("✅ Transcript processing is complete for Bot: {BotId}", bot.Id);
            // TODO: Fetch and save the actual transcript text here later
        
        }

        // 6. Always return 200 OK quickly so Recall.ai knows the webhook was received
        return Ok(new { received = true });
    }
}