using finalyearproject.Services.Interfaces;
using System.Text;
using System.Text.Json;
using finalyearproject.Data;
using finalyearproject.DTOs;
using Microsoft.Extensions.Configuration;

namespace finalyearproject.Services.Implementation;

public class MinutesServices:Iminutes
{
    
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _apiUrl;
    private  readonly AppDbContext _context;
    
    
    
    public MinutesServices(HttpClient httpClient, IConfiguration configuration,AppDbContext context)
            {
                _httpClient = httpClient;
                _apiKey = configuration["GeminiApi:ApiKey"] 
                          ?? throw new ArgumentNullException("API Key is missing");
                _apiUrl = configuration["GeminiApi:Url"] 
                          ?? throw new ArgumentNullException("API URL is missing");
                
                _context = context;
            }
    
    
    
    public async Task<string> GenerateMinutesAsync( Guid  transcriptId)
    {
        var transcriptRecord = await _context.Transcripts.FindAsync(transcriptId);
        if (transcriptRecord == null)        {
            throw new ArgumentException("Transcript not found for the given ID.");
        }
        
        
        // System instruction + the actual transcript data
        var prompt = $@"
You are an expert executive assistant. Please read the following transcript and generate concise, professional meeting minutes. 
Format the output with clear headings: 
_ In attendance 
-Absent  with apology,
-Abesent without apology 
- Meeting Summary
- Key Discussion Points
- Decisions Made
- Action Items (with assignees if mentioned).

Transcript:
{transcriptRecord.RawText}";

        var requestBody = new GeminiRequest
        {
            contents = new[]
            {
                new
                {
                    parts = new[] { new { text = prompt } }
                }
            }
        };

        var jsonContent = new StringContent(
            JsonSerializer.Serialize(requestBody),
            Encoding.UTF8,
            "application/json");

        // Append API key to the query string
        var requestUri = $"{_apiUrl}?key={_apiKey}";
        
        Console.WriteLine($"\n=== DEBUG URL: {requestUri} ===\n");
        
        var response = await _httpClient.PostAsync(requestUri, jsonContent);
        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync();
        var geminiResponse = JsonSerializer.Deserialize<GeminiResponse>(responseJson);

        // Extract the generated text from the response payload
        var generatedMinutes = geminiResponse?.candidates?[0]?.content?.parts?[0]?.text;
        

        return generatedMinutes ?? "Failed to generate minutes.";
    }

    
}