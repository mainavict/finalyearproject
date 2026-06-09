using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using finalyearproject.Services.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace finalyearproject.Services.Implementation;

public class AssemblyAITranscriptionService : ITranscriber
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly ILogger<AssemblyAITranscriptionService> _logger;

    public AssemblyAITranscriptionService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<AssemblyAITranscriptionService> logger)
    {
        _httpClient = httpClient;
        _apiKey = configuration["AssemblyAI:ApiKey"] 
                  ?? throw new ArgumentNullException("AssemblyAI API Key is missing");
        _logger = logger;
    }

    // ─── Existing: local file ────────────────────────────────────────────────
    public async Task<string> TranscribeAsync(string filePath)
    {
        await using var stream = File.OpenRead(filePath);
        return await TranscribeFromStreamAsync(stream, Path.GetFileName(filePath));
    }

    // ─── New: stream from B2 ─────────────────────────────────────────────────
    public async Task<string> TranscribeFromStreamAsync(Stream audioStream, string fileName)
    {
        // 1. Upload the audio stream to AssemblyAI to get a temporary URL
        _logger.LogInformation("Uploading audio stream to AssemblyAI...");
        var uploadUrl = await UploadAudioAsync(audioStream);

        // 2. Submit the transcription request
        _logger.LogInformation("Submitting transcription request to AssemblyAI...");
        var transcriptId = await SubmitTranscriptionAsync(uploadUrl);

        // 3. Poll for the result
        _logger.LogInformation("Polling for transcription result (ID: {TranscriptId})...", transcriptId);
        return await PollForCompletionAsync(transcriptId);
    }

    private async Task<string> UploadAudioAsync(Stream audioStream)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.assemblyai.com/v2/upload");
        request.Headers.Add("authorization", _apiKey);
        
        // AssemblyAI expects raw octet-stream for uploads
        var content = new StreamContent(audioStream);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        request.Content = content;

        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("upload_url").GetString() 
               ?? throw new Exception("Failed to get upload URL from AssemblyAI");
    }

    private async Task<string> SubmitTranscriptionAsync(string audioUrl)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.assemblyai.com/v2/transcript");
        
        // Authorization is a Request Header (Correct)
        request.Headers.Add("authorization", _apiKey);

        var payload = new { audio_url = audioUrl };
        request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("id").GetString() 
               ?? throw new Exception("Failed to get transcript ID from AssemblyAI");
    }

    private async Task<string> PollForCompletionAsync(string transcriptId)
    {
        int maxAttempts = 60; // Max 3 minutes (60 attempts * 3 seconds)
        int attempt = 0;

        while (attempt < maxAttempts)
        {
            await Task.Delay(TimeSpan.FromSeconds(3)); // Wait 3 seconds between polls
            attempt++;

            using var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.assemblyai.com/v2/transcript/{transcriptId}");
            request.Headers.Add("authorization", _apiKey);

            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            var status = doc.RootElement.GetProperty("status").GetString();

            if (status == "completed")
            {
                _logger.LogInformation("Transcription completed successfully!");
                return doc.RootElement.GetProperty("text").GetString() ?? "";
            }
            else if (status == "error")
            {
                var errorMsg = doc.RootElement.GetProperty("error").GetString();
                throw new Exception($"AssemblyAI transcription failed: {errorMsg}");
            }
            
            _logger.LogDebug("Transcription status: {Status} (Attempt {Attempt}/{Max})", status, attempt, maxAttempts);
        }

        throw new TimeoutException("AssemblyAI transcription timed out after 3 minutes.");
    }
}