using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using finalyearproject.DTOs;
using finalyearproject.Services.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;


namespace finalyearproject.Services.Implementation;

public class RecallAiService : IRecallAiService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<RecallAiService> _logger;

    // Your excellent JSON options setup
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    
    public RecallAiService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<RecallAiService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;

        var apiKey = configuration["RecallAi:ApiKey"] 
            ?? throw new InvalidOperationException("RecallAi:ApiKey is missing.");
        var baseUrl = configuration["RecallAi:BaseUrl"] 
            ?? throw new InvalidOperationException("RecallAi:BaseUrl is missing.");

        _httpClient.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Token", apiKey);
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }
    
    
    
    public async Task<List<RecallParticipantEvent>?> GetParticipantEventsAsync(string recordingId, CancellationToken ct = default)
{
    _logger.LogInformation("👥 Fetching participant events for recording: {RecordingId}", recordingId);

    var endpoint = $"recording/{recordingId}/";
    using var response = await _httpClient.GetAsync(endpoint, ct);
    
    if (!response.IsSuccessStatusCode)
    {
        _logger.LogWarning("⚠️ Failed to fetch recording details: {StatusCode}", response.StatusCode);
        return null;
    }

    var json = await response.Content.ReadAsStringAsync(ct);
    using var doc = JsonDocument.Parse(json);
    var root = doc.RootElement;

    // Try to find participant_events in media_shortcuts
    if (root.TryGetProperty("media_shortcuts", out var mediaShortcuts) &&
        mediaShortcuts.TryGetProperty("participant_events", out var participantEvents) &&
        participantEvents.TryGetProperty("data", out var data) &&
        data.TryGetProperty("participant_events_download_url", out var downloadUrlProp))
    {
        var downloadUrl = downloadUrlProp.GetString();
        
        if (!string.IsNullOrEmpty(downloadUrl))
        {
            _logger.LogInformation("📥 Downloading participant events from: {Url}", downloadUrl);
            
            // Download the participant events JSON
            using var eventsResponse = await _httpClient.GetAsync(downloadUrl, ct);
            if (eventsResponse.IsSuccessStatusCode)
            {
                var eventsJson = await eventsResponse.Content.ReadAsStringAsync(ct);
                var events = JsonSerializer.Deserialize<List<RecallParticipantEvent>>(eventsJson, new JsonSerializerOptions 
                { 
                    PropertyNameCaseInsensitive = true 
                });
                
                _logger.LogInformation("✅ Found {Count} participant events", events?.Count ?? 0);
                return events;
            }
        }
    }

    _logger.LogWarning("⚠️ No participant events found in recording data");
    return null;
}
    
    

    public async Task<RecallBotResponse?> CreateBotAsync(RecallBotRequest request, CancellationToken ct = default)
    {
        _logger.LogInformation("Creating Recall.ai bot for meeting: {MeetingUrl}", Sanitize(request.MeetingUrl));

        using var response = await _httpClient.PostAsJsonAsync("bot/", request, _jsonOptions, ct);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<RecallBotResponse>(_jsonOptions, ct);
    }

 public async Task<RecallRecordingMedia?> GetRecordingMediaAsync(string recordingId, CancellationToken ct = default)
{
    _logger.LogInformation("📥 Fetching media metadata for recording: {RecordingId}", recordingId);

    using var response = await _httpClient.GetAsync($"recording/{recordingId}/", ct);
    var rawJson = await response.Content.ReadAsStringAsync(ct);
    
    _logger.LogInformation("📋 Raw media response for {RecordingId}: {Response}", recordingId, rawJson);
    
    response.EnsureSuccessStatusCode();

    // Parse the response manually to handle the actual structure
    using var doc = JsonDocument.Parse(rawJson);
    var root = doc.RootElement;
    
    string? downloadUrl = null;
    string contentType = "video/mp4";
    
    // Try to get audio_mixed first
    if (root.TryGetProperty("media_shortcuts", out var mediaShortcuts))
    {
        if (mediaShortcuts.TryGetProperty("audio_mixed", out var audioMixed) && 
            audioMixed.ValueKind != JsonValueKind.Null &&
            audioMixed.TryGetProperty("data", out var audioData) &&
            audioData.TryGetProperty("download_url", out var audioUrl))
        {
            downloadUrl = audioUrl.GetString();
            contentType = "audio/mp4";
            _logger.LogInformation("✅ Found audio_mixed URL");
        }
        // Fall back to video_mixed if audio isn't available
        else if (mediaShortcuts.TryGetProperty("video_mixed", out var videoMixed) && 
                 videoMixed.ValueKind != JsonValueKind.Null &&
                 videoMixed.TryGetProperty("data", out var videoData) &&
                 videoData.TryGetProperty("download_url", out var videoUrl))
        {
            downloadUrl = videoUrl.GetString();
            contentType = "video/mp4";
            _logger.LogInformation("✅ Using video_mixed URL (audio not available)");
        }
    }
    
    if (string.IsNullOrEmpty(downloadUrl))
    {
        _logger.LogError("❌ No audio or video URL found in response");
        return null;
    }
    
    _logger.LogInformation("✅ Found media URL: {Url}", downloadUrl);
    
    // Return a simple object with the URL
    return new RecallRecordingMedia(
        Id: recordingId,
        Media: new RecallMediaLinks(
            Audio: contentType.StartsWith("audio") ? downloadUrl : null,
            Video: contentType.StartsWith("video") ? downloadUrl : null
        )
    );
}
   public async Task<string> GetTranscriptAsync(string botId, CancellationToken ct = default)
{
    _logger.LogInformation("📥 Fetching transcript for bot: {BotId}", botId);

    // ✅ CORRECT: Fetch bot details which includes the transcript
    var endpoint = $"bot/{botId}/";
    
    _logger.LogInformation("🔗 Calling Recall.ai endpoint: {Endpoint}", endpoint);

    using var response = await _httpClient.GetAsync(endpoint, ct);

    // ✅ LOG THE ERROR BODY if the request fails
    if (!response.IsSuccessStatusCode)
    {
        var errorBody = await response.Content.ReadAsStringAsync(ct);
        _logger.LogError("❌ Failed to fetch bot details for {BotId}. Status: {StatusCode}. Response: {ErrorBody}", 
            botId, response.StatusCode, errorBody);
        
        throw new HttpRequestException($"Recall.ai bot fetch failed ({response.StatusCode}): {errorBody}");
    }

    var json = await response.Content.ReadAsStringAsync(ct);
    _logger.LogInformation("✅ Successfully fetched bot details for bot {BotId} (Length: {Length})", botId, json.Length);
    
    // Parse the bot details and extract transcript
    using var doc = JsonDocument.Parse(json);
    var root = doc.RootElement;
    
    // Try to extract transcript from the bot response
    // Recall.ai includes transcript in the bot object under "transcript" or "outputs.transcript"
    if (root.TryGetProperty("transcript", out var transcriptProp))
    {
        var transcriptText = transcriptProp.GetString();
        if (!string.IsNullOrEmpty(transcriptText))
        {
            _logger.LogInformation("✅ Found transcript in bot response (Length: {Length})", transcriptText.Length);
            return transcriptText;
        }
    }
    
    // Try nested under outputs
    if (root.TryGetProperty("outputs", out var outputsProp) && 
        outputsProp.TryGetProperty("transcript", out var outputsTranscriptProp))
    {
        var transcriptText = outputsTranscriptProp.GetString();
        if (!string.IsNullOrEmpty(transcriptText))
        {
            _logger.LogInformation("✅ Found transcript in outputs (Length: {Length})", transcriptText.Length);
            return transcriptText;
        }
    }

    _logger.LogWarning("⚠️ Could not find transcript in bot response. Returning full JSON.");
    return json;
}

public async Task<(Stream Stream, string ContentType, string FileName)> DownloadRecordingAudioAsync(
    string downloadUrl, CancellationToken ct = default)
{
    _logger.LogInformation("📥 Downloading media from: {Url}", downloadUrl);

    // ✅ Create a new HttpClient WITHOUT the Recall.ai Authorization header
    // because the pre-signed S3 URL already has its own signature
    using var httpClient = new HttpClient();
    
    using var request = new HttpRequestMessage(HttpMethod.Get, downloadUrl);
    
    // ✅ Explicitly ensure NO Authorization header is sent
    request.Headers.Authorization = null;
    
    using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);

    if (!response.IsSuccessStatusCode)
    {
        var err = await response.Content.ReadAsStringAsync(ct);
        throw new HttpRequestException($"Download failed ({response.StatusCode}): {err}");
    }

    var contentType = response.Content.Headers.ContentType?.MediaType ?? "video/mp4";
    
    // ✅ Handle both audio and video files
    var extension = contentType switch
    {
        "audio/mpeg" => ".mp3",
        "audio/wav" => ".wav",
        "audio/mp4" => ".m4a",
        "video/mp4" => ".mp4",
        _ => ".mp4"
    };

    using var networkStream = await response.Content.ReadAsStreamAsync(ct);
    var memoryStream = new MemoryStream();
    await networkStream.CopyToAsync(memoryStream, ct);
    memoryStream.Position = 0;

    var fileName = $"recording_{Guid.NewGuid():N}{extension}";

    _logger.LogInformation("✅ Downloaded {FileName} ({Size} bytes)", fileName, memoryStream.Length);

    return (memoryStream, contentType, fileName);
}
    /// <summary>
    /// Strips newlines and carriage returns to prevent log-forging/injection attacks.
    /// </summary>
    private static string Sanitize(string value) =>
        value?.Replace("\r", string.Empty).Replace("\n", string.Empty) ?? string.Empty;
}


