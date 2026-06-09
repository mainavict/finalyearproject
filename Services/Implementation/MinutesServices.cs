using finalyearproject.Services.Interfaces;
using System.Text;
using System.Text.Json;
using finalyearproject.Data;
using finalyearproject.DTOs;
using finalyearproject.Models;
using finalyearproject.Wrappers;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore; 

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
You are a professional meeting secretary.
    Read the following meeting transcript and extract structured meeting minutes.

    RULES:
    - Only include information explicitly stated in the transcript
    - Do not invent or assume anything
    - If a field cannot be determined, use null or empty array
    - Action items must have a named owner
    - Respond ONLY with raw valid JSON — no markdown, no code blocks,
      no explanation, no backticks. Just the JSON object.

    TRANSCRIPT:
    {{transcript}}

    Return this exact JSON structure:
    {{{{
      ""meetingTitle"":     ""string or null"",
      ""meetingDate"":      ""string or null"",
      ""attendees"":        [""name1"", ""name2""],
      ""absentWithApology"":[""name1""],
      ""absentWithoutApology"":[""name1""],
      ""meetingSummary"":   ""string"",
      ""discussionPoints"": [
        {{{{
          ""topic"":   ""string"",
          ""summary"": ""string""
        }}}}
      ],
      ""decisions"": [""string""],
      ""actionItems"": [
        {{{{
          ""owner"":   ""string"",
          ""task"":    ""string"",
          ""dueDate"": ""string or null""
        }}}}
      ],
      ""nextMeeting"": ""string or null""
    }}}}

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
    
    
    public async Task <ApiResponse<Minutes>> GetMinutesByMeetingIdAsync(Guid meetingId)
    {
        var minutes = await _context.Minutes.FirstOrDefaultAsync(m => m.MeetingId == meetingId);
        if (minutes == null)
        {
            return ApiResponse<Minutes>.FailureResponse("Minutes not found for the given meeting ID.", statusCode: 404);
        }
        return  ApiResponse<Minutes>.SuccessResponse(minutes, "Minutes retrieved successfully.", statusCode: 200);
    }
    
    public async Task <ApiResponse<List<Minutes>>> GetAllMinutesAsync()
    {
        var minutesList = await _context.Minutes.ToListAsync();
        return ApiResponse<List<Minutes>>.SuccessResponse(minutesList, "All minutes retrieved successfully.", statusCode: 200);
    }

    
}