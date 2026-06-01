using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using finalyearproject.DTOs;
using finalyearproject.Services.Interfaces;

namespace finalyearproject.Services.Implementation;

public class RecallAiService : IRecallAiService
{
    private readonly HttpClient _httpClient;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<RecallAiService> _logger;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy        = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition      = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public RecallAiService(
        HttpClient httpClient,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<RecallAiService> logger)
    {
        _httpClient        = httpClient;
        _httpClientFactory = httpClientFactory;
        _configuration     = configuration;
        _logger            = logger;

        var apiKey  = _configuration["RecallAi:ApiKey"]
                      ?? throw new InvalidOperationException("RecallAi:ApiKey is missing.");
        var baseUrl = _configuration["RecallAi:BaseUrl"]
                      ?? throw new InvalidOperationException("RecallAi:BaseUrl is missing.");

        _httpClient.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Token", apiKey);
        _httpClient.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
    }

    // ── Create bot ────────────────────────────────────────────────────────────

    public async Task<RecallBotResponse> CreateBotAsync(
        string meetingUrl,
        string botName = "Minutes Assistant")
    {
        var payload = new RecallBotRequest
        {
            MeetingUrl = meetingUrl,
            BotName    = botName
        };

        var json    = JsonSerializer.Serialize(payload, _jsonOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        _logger.LogInformation(
            "Creating Recall.ai bot for meeting URL: {MeetingUrl}", Sanitize(meetingUrl));

        var response = await _httpClient.PostAsync("bot", content);
        var body     = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException(
                $"Recall.ai bot creation failed ({response.StatusCode}): {body}");

        var bot = JsonSerializer.Deserialize<RecallBotResponse>(body, _jsonOptions)
                  ?? throw new InvalidOperationException("Null response from Recall.ai bot creation.");

        _logger.LogInformation("Recall.ai bot created with ID: {BotId}", bot.Id);
        return bot;
    }

    // ── Get recording media links ─────────────────────────────────────────────

    public async Task<RecallRecordingMedia> GetRecordingMediaAsync(string recordingId)
    {
        _logger.LogInformation(
            "Fetching media links for Recall.ai recording {RecordingId}", Sanitize(recordingId));

        var response = await _httpClient.GetAsync($"recording/{recordingId}/media/");
        var body     = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException(
                $"Recall.ai recording media fetch failed ({response.StatusCode}): {body}");

        var media = JsonSerializer.Deserialize<RecallRecordingMedia>(body, _jsonOptions)
                    ?? throw new InvalidOperationException("Null response from Recall.ai recording media.");

        return media;
    }

    // ── Download audio stream ─────────────────────────────────────────────────

    public async Task<(Stream stream, string contentType, string fileName)> DownloadRecordingAudioAsync(
        string audioUrl)
    {
        _logger.LogInformation("Downloading audio from Recall.ai URL: {AudioUrl}", audioUrl);

        // Use a fresh client with no auth headers — Recall.ai media URLs are pre-signed
        // and must not receive the Recall.ai Authorization token.
        var plainClient = _httpClientFactory.CreateClient();
        var response = await plainClient.GetAsync(
            audioUrl, HttpCompletionOption.ResponseHeadersRead);

        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException(
                $"Recall.ai audio download failed ({response.StatusCode}): {err}");
        }

        var contentType = response.Content.Headers.ContentType?.MediaType ?? "audio/mp4";
        var extension   = contentType switch
        {
            "audio/mpeg" => ".mp3",
            "audio/wav"  => ".wav",
            "audio/ogg"  => ".ogg",
            "audio/webm" => ".webm",
            _            => ".mp4"
        };

        var stream   = await response.Content.ReadAsStreamAsync();
        var fileName = $"recording{extension}";

        return (stream, contentType, fileName);
    }

    // ── Security helpers ──────────────────────────────────────────────────────

    /// <summary>Strips newlines and carriage returns to prevent log-forging.</summary>
    private static string Sanitize(string value) =>
        value.Replace("\r", string.Empty)
             .Replace("\n", string.Empty);
}
