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

    public async Task<string> TranscribeAsync(string filePath)
    {
        var apiKey = _configuration["Groq:ApiKey"];

        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", apiKey);

        using var form = new MultipartFormDataContent();

        using var stream = File.OpenRead(filePath);

        var fileContent = new StreamContent(stream);

        fileContent.Headers.ContentType =
            new MediaTypeHeaderValue("audio/wav");

        form.Add(
            fileContent,
            "file",
            Path.GetFileName(filePath));

        form.Add(
            new StringContent("whisper-large-v3"),
            "model");

        var response = await _httpClient.PostAsync(
            "https://api.groq.com/openai/v1/audio/transcriptions",
            form);

        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();

        using var doc = JsonDocument.Parse(json);

        return doc.RootElement
            .GetProperty("text")
            .GetString()!;
    }
}
