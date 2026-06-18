using finalyearproject.Services.Interfaces;
using System.Text;
using System.Text.Json;
using finalyearproject.Data;
using finalyearproject.DTOs;
using finalyearproject.Models;
using finalyearproject.Wrappers;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;

namespace finalyearproject.Services.Implementation;

public class MinutesServices : Iminutes
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _apiUrl;
    private readonly string _fallbackApiUrl; // ✅ NEW: Fallback model URL
    private readonly string _modelName;
    private readonly string _fallbackModelName; // ✅ NEW: Fallback model
    private readonly AppDbContext _context;
    private readonly ILogger<MinutesServices> _logger;
    private readonly AsyncRetryPolicy _retryPolicy;
    
    private const int MAX_TRANSCRIPT_CHARS = 100000;
    private const int MAX_RETRIES = 5; // ✅ INCREASED: More retries for 503 errors

    public MinutesServices(
        HttpClient httpClient, 
        IConfiguration configuration, 
        AppDbContext context,
        ILogger<MinutesServices> logger)
    {
        _httpClient = httpClient;
        _apiKey = configuration["GeminiApi:ApiKey"] 
                  ?? throw new ArgumentNullException("API Key is missing");
        _apiUrl = configuration["GeminiApi:Url"] 
                  ?? throw new ArgumentNullException("API URL is missing");
        _modelName = configuration["GeminiApi:Model"] ?? "gemini-2.5-flash";
        
        // ✅ NEW: Fallback model configuration
        _fallbackApiUrl = configuration["GeminiApi:FallbackUrl"] 
            ?? "https://generativelanguage.googleapis.com/v1beta/models/gemini-1.5-flash:generateContent";
        _fallbackModelName = configuration["GeminiApi:FallbackModel"] ?? "gemini-1.5-flash";
        
        _context = context;
        _logger = logger;

        // ✅ IMPROVED: Retry policy with longer waits for 503 errors
        _retryPolicy = Policy
            .Handle<HttpRequestException>(ex => 
            {
                // Retry on 503 (Service Unavailable) or 429 (Rate Limit)
                return ex.Message.Contains("503") || 
                       ex.Message.Contains("429") ||
                       ex.Message.Contains("ServiceUnavailable") ||
                       ex.Message.Contains("TooManyRequests");
            })
            .Or<TaskCanceledException>()
            .WaitAndRetryAsync(
                MAX_RETRIES,
                retryAttempt => 
                {
                    // ✅ Exponential backoff with jitter: 5s, 10s, 20s, 40s, 80s
                    var baseDelay = TimeSpan.FromSeconds(Math.Pow(2, retryAttempt) * 2.5);
                    var jitter = TimeSpan.FromMilliseconds(new Random().Next(0, 2000));
                    return baseDelay + jitter;
                },
                (exception, timeSpan, retryCount, context) =>
                {
                    _logger.LogWarning(
                        "⚠️ Retry {RetryCount}/{MaxRetries} due to: {ExceptionMessage}. Waiting {TimeSpan}s",
                        retryCount, MAX_RETRIES, exception.Message, timeSpan.TotalSeconds);
                });
    }

    public async Task<string> GenerateMinutesAsync(Guid transcriptId)
    {
        var transcriptRecord = await _context.Transcripts.FindAsync(transcriptId);
        if (transcriptRecord == null)
        {
            throw new ArgumentException("Transcript not found for the given ID.");
        }

        if (string.IsNullOrWhiteSpace(transcriptRecord.RawText))
        {
            throw new ArgumentException("Transcript is empty.");
        }

        var transcriptText = transcriptRecord.RawText;
        if (transcriptText.Length > MAX_TRANSCRIPT_CHARS)
        {
            _logger.LogWarning(
                "Transcript too long ({Length} chars). Truncating to {MaxChars} chars.",
                transcriptText.Length, MAX_TRANSCRIPT_CHARS);
            transcriptText = transcriptText.Substring(0, MAX_TRANSCRIPT_CHARS) + 
                           "\n\n[TRANSCRIPT TRUNCATED DUE TO LENGTH]";
        }

        var prompt = BuildOptimizedPrompt(transcriptText);

        // ✅ Try primary model first, then fallback
        try
        {
            return await _retryPolicy.ExecuteAsync(async () =>
            {
                return await CallGeminiApiAsync(prompt, _apiUrl, _modelName);
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Primary model failed. Trying fallback model: {FallbackModel}", _fallbackModelName);
            
            // ✅ Fallback to alternative model
            return await _retryPolicy.ExecuteAsync(async () =>
            {
                return await CallGeminiApiAsync(prompt, _fallbackApiUrl, _fallbackModelName);
            });
        }
    }

    private string BuildOptimizedPrompt(string transcript)
    {
        return $@"You are an expert meeting secretary with 20 years of experience. Your task is to analyze the meeting transcript below and generate comprehensive, structured meeting minutes.

## CRITICAL RULES:
1. **Accuracy First**: Only include information explicitly stated in the transcript. Never invent, assume, or hallucinate information.
2. **Professional Tone**: Use formal, clear, and concise language.
3. **Completeness**: Capture all important discussions, decisions, and action items.
4. **Structure**: Follow the exact JSON schema provided below.
5. **No Hallucinations**: If information is not in the transcript, use null or empty arrays.
6. **Action Items**: Every action item MUST have a specific owner (person's name). If no owner is mentioned, use ""Unassigned"".
7. **JSON Only**: Return ONLY valid JSON. No markdown, no code blocks, no explanations, no backticks.

## OUTPUT SCHEMA:
{{
  ""meetingTitle"": ""string or null - The main topic or title of the meeting"",
  ""meetingDate"": ""string or null - Date in format 'YYYY-MM-DD' if mentioned, otherwise null"",
  ""attendees"": [""array of strings - Full names of all participants""],
  ""absentWithApology"": [""array of strings - People who sent apologies""],
  ""absentWithoutApology"": [""array of strings - People who were expected but absent""],
  ""meetingSummary"": ""string - A comprehensive 2-3 paragraph summary of the entire meeting"",
  ""discussionPoints"": [
    {{
      ""topic"": ""string - The main topic discussed"",
      ""summary"": ""string - Detailed summary of the discussion on this topic""
    }}
  ],
  ""decisions"": [""array of strings - All decisions made during the meeting""],
  ""actionItems"": [
    {{
      ""owner"": ""string - Name of the person responsible"",
      ""task"": ""string - Specific task to be completed"",
      ""dueDate"": ""string or null - Due date if mentioned, otherwise null""
    }}
  ],
  ""nextMeeting"": ""string or null - Date/time of next meeting if scheduled""
}}

## TRANSCRIPT:
---
{transcript}
---

Now analyze the transcript and generate the meeting minutes as JSON:";
    }

    private async Task<string> CallGeminiApiAsync(string prompt, string apiUrl, string modelName)
    {
        var requestBody = new
        {
            contents = new[]
            {
                new
                {
                    parts = new[] { new { text = prompt } }
                }
            },
            generationConfig = new
            {
                temperature = 0.3,
                topP = 0.8,
                topK = 40,
                maxOutputTokens = 8192,
            }
        };

        var jsonContent = new StringContent(
            JsonSerializer.Serialize(requestBody),
            Encoding.UTF8,
            "application/json");

        var requestUri = $"{apiUrl}?key={_apiKey}";
        
        _logger.LogInformation("🤖 Calling Gemini API with model: {Model}", modelName);

        var response = await _httpClient.PostAsync(requestUri, jsonContent);
        
        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            _logger.LogError("❌ Gemini API error: {StatusCode} - {Error}", response.StatusCode, errorContent);
            throw new HttpRequestException($"Gemini API returned {response.StatusCode}: {errorContent}");
        }

        var responseJson = await response.Content.ReadAsStringAsync();
        var geminiResponse = JsonSerializer.Deserialize<GeminiResponse>(responseJson);

        var generatedMinutes = geminiResponse?.candidates?[0]?.content?.parts?[0]?.text;

        if (string.IsNullOrWhiteSpace(generatedMinutes))
        {
            _logger.LogWarning("⚠️ Gemini returned empty response");
            throw new InvalidOperationException("Gemini returned empty response");
        }

        var cleanedJson = CleanJsonResponse(generatedMinutes);
        
        try
        {
            JsonDocument.Parse(cleanedJson);
            _logger.LogInformation("✅ Successfully generated and validated meeting minutes JSON with {Model}", modelName);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "❌ Generated minutes is not valid JSON: {Json}", cleanedJson);
            throw new InvalidOperationException("Generated minutes is not valid JSON", ex);
        }

        return cleanedJson;
    }

    private string CleanJsonResponse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return "{}";

        json = json.Trim();

        if (json.StartsWith("```json"))
            json = json.Substring(7);
        else if (json.StartsWith("```"))
            json = json.Substring(3);

        if (json.EndsWith("```"))
            json = json.Substring(0, json.Length - 3);

        json = json.Trim();
        json = System.Text.RegularExpressions.Regex.Replace(json, @",\s*([\]}])", @"$1");

        return json;
    }

    public async Task<ApiResponse<Minutes>> GetMinutesByMeetingIdAsync(Guid meetingId)
    {
        var minutes = await _context.Minutes
            .Include(m => m.Meeting)
            .FirstOrDefaultAsync(m => m.MeetingId == meetingId);
            
        if (minutes == null)
        {
            return ApiResponse<Minutes>.FailureResponse(
                "Minutes not found for the given meeting ID.", 
                statusCode: 404);
        }
        
        return ApiResponse<Minutes>.SuccessResponse(
            minutes, 
            "Minutes retrieved successfully.", 
            statusCode: 200);
    }

    public async Task<ApiResponse<List<Minutes>>> GetAllMinutesAsync()
    {
        var minutesList = await _context.Minutes
            .Include(m => m.Meeting)
            .OrderByDescending(m => m.CreatedAt)
            .ToListAsync();
            
        return ApiResponse<List<Minutes>>.SuccessResponse(
            minutesList, 
            "All minutes retrieved successfully.", 
            statusCode: 200);
    }
}