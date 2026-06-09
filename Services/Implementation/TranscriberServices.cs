// Services/Implementation/GroqTranscriptionService.cs
using System.Net.Http.Headers;
using System.Text.Json;
using finalyearproject.Services.Interfaces;

namespace finalyearproject.Services.Implementation;

public class GroqTranscriptionService : ITranscriber
{
    private readonly IConfiguration _configuration;
    private readonly HttpClient _httpClient;

    public GroqTranscriptionService(
        IConfiguration configuration,
        HttpClient httpClient)
    {
        _configuration = configuration;
        _httpClient = httpClient;
    }

    // ─── Existing: local file ────────────────────────────────────────────────

    public async Task<string> TranscribeAsync(string filePath)
    {
        await using var stream = File.OpenRead(filePath);
        return await TranscribeFromStreamAsync(stream, Path.GetFileName(filePath));
    }

  

    public async Task<string> TranscribeFromStreamAsync(
        Stream audioStream,
        string fileName)
    {
        var apiKey = _configuration["Groq:ApiKey"];

        // Don't set DefaultRequestHeaders — it's not thread-safe for per-request auth
        using var requestMessage = new HttpRequestMessage(
            HttpMethod.Post,
            "https://api.groq.com/openai/v1/audio/transcriptions");

        requestMessage.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", apiKey);

        using var form = new MultipartFormDataContent();

        var fileContent = new StreamContent(audioStream);
        fileContent.Headers.ContentType =
            new MediaTypeHeaderValue(GetMimeType(fileName));

        form.Add(fileContent, "file", fileName);
        form.Add(new StringContent("whisper-large-v3"), "model");

        requestMessage.Content = form;

        var response = await _httpClient.SendAsync(requestMessage);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);

        return doc.RootElement
            .GetProperty("text")
            .GetString()!;
    }
    

    private static string GetMimeType(string fileName) =>
        Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".mp3"  => "audio/mpeg",
            ".mp4"  => "audio/mp4",
            ".m4a"  => "audio/m4a",
            ".wav"  => "audio/wav",
            ".webm" => "audio/webm",
            ".ogg"  => "audio/ogg",
            _       => "audio/mpeg"
        };
}