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

    public async Task<RecallBotResponse?> CreateBotAsync(RecallBotRequest request, CancellationToken ct = default)
    {
        _logger.LogInformation("Creating Recall.ai bot for meeting: {MeetingUrl}", Sanitize(request.MeetingUrl));

        using var response = await _httpClient.PostAsJsonAsync("bot/", request, _jsonOptions, ct);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<RecallBotResponse>(_jsonOptions, ct);
    }

    public async Task<RecallRecordingMedia?> GetRecordingMediaAsync(string recordingId, CancellationToken ct = default)
    {
        _logger.LogInformation("Fetching media metadata for recording: {RecordingId}", Sanitize(recordingId));

        using var response = await _httpClient.GetAsync($"recording/{recordingId}/", ct);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<RecallRecordingMedia>(_jsonOptions, ct);
    }

    public async Task<(Stream Stream, string ContentType, string FileName)> DownloadRecordingAudioAsync(
        string audioUrl, CancellationToken ct = default)
    {
        _logger.LogInformation("Downloading audio from Recall.ai URL: {AudioUrl}", Sanitize(audioUrl));

        using var request = new HttpRequestMessage(HttpMethod.Get, audioUrl);
        // Intentionally NOT adding Authorization header; Recall.ai media URLs are pre-signed.

        // 1. Wrap response in 'using' to prevent socket leaks
        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);

        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException($"Recall.ai audio download failed ({response.StatusCode}): {err}");
        }

        var contentType = response.Content.Headers.ContentType?.MediaType ?? "audio/mp4";
        var extension = contentType switch
        {
            "audio/mpeg" => ".mp3",
            "audio/wav"  => ".wav",
            "audio/ogg"  => ".ogg",
            "audio/webm" => ".webm",
            _            => ".mp4"
        };

        // 2. STREAM LIFECYCLE FIX:
        // The network stream is tied to the 'response' object. If we return it directly, 
        // the 'using' block will dispose the response, closing the stream and causing 
        // an ObjectDisposedException when the controller tries to read it.
        // We must copy it to a MemoryStream (or a temp file for very large recordings).
        using var networkStream = await response.Content.ReadAsStreamAsync(ct);
        var memoryStream = new MemoryStream();
        await networkStream.CopyToAsync(memoryStream, ct);
        
        // Reset position to 0 so the controller can read it from the beginning
        memoryStream.Position = 0; 

        var fileName = $"recording_{Guid.NewGuid():N}{extension}";

        return (memoryStream, contentType, fileName);
    }

    /// <summary>
    /// Strips newlines and carriage returns to prevent log-forging/injection attacks.
    /// </summary>
    private static string Sanitize(string value) =>
        value?.Replace("\r", string.Empty).Replace("\n", string.Empty) ?? string.Empty;
}