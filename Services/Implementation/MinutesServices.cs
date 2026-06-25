using finalyearproject.Services.Interfaces;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
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
    private readonly string _fallbackApiUrl;
    private readonly string _modelName;
    private readonly string _fallbackModelName;
    private readonly AppDbContext _context;
    private readonly ILogger<MinutesServices> _logger;
    private readonly AsyncRetryPolicy _retryPolicy;
    
    private const int MAX_TRANSCRIPT_CHARS = 100000;
    private const int MAX_RETRIES = 5;

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
        
        // ✅ FIXED: Use correct fallback model name
        _fallbackApiUrl = configuration["GeminiApi:FallbackUrl"] 
            ?? "https://generativelanguage.googleapis.com/v1beta/models/gemini-2.0-flash:generateContent";
        _fallbackModelName = configuration["GeminiApi:FallbackModel"] ?? "gemini-2.0-flash";
        
        _context = context;
        _logger = logger;

        _retryPolicy = Policy
            .Handle<HttpRequestException>(ex => 
            {
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

        // Try primary model first, then fallback
        try
        {
            return await _retryPolicy.ExecuteAsync(async () =>
            {
                return await CallGeminiApiAsync(prompt, _apiUrl, _modelName);
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "⚠️ Primary model failed. Trying fallback model: {FallbackModel}", _fallbackModelName);
            
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
8. **Be Concise**: Keep summaries brief but complete. Don't repeat information.

## OUTPUT SCHEMA:
{{
  ""meetingTitle"": ""string or null"",
  ""meetingDate"": ""string or null (YYYY-MM-DD)"",
  ""attendees"": [""name1"", ""name2""],
  ""absentWithApology"": [""name1""],
  ""absentWithoutApology"": [""name1""],
  ""meetingSummary"": ""string (2-3 paragraphs max)"",
  ""discussionPoints"": [
    {{
      ""topic"": ""string"",
      ""summary"": ""string (concise but complete)""
    }}
  ],
  ""decisions"": [""string""],
  ""actionItems"": [
    {{
      ""owner"": ""string"",
      ""task"": ""string"",
      ""dueDate"": ""string or null""
    }}
  ],
  ""nextMeeting"": ""string or null""
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
                // ✅ INCREASED: From 8192 to 65536 for long transcripts
                maxOutputTokens = 65536,
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

        // ✅ Clean and attempt to repair truncated JSON
        var cleanedJson = CleanAndRepairJson(generatedMinutes);
        
        // Validate it's proper JSON
        try
        {
            JsonDocument.Parse(cleanedJson);
            _logger.LogInformation("✅ Successfully generated and validated meeting minutes JSON with {Model}", modelName);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "❌ Generated minutes is not valid JSON even after repair: {Json}", cleanedJson.Substring(0, Math.Min(500, cleanedJson.Length)));
            throw new InvalidOperationException("Generated minutes is not valid JSON", ex);
        }

        return cleanedJson;
    }

    // ✅ NEW: Enhanced JSON cleaning with repair for truncated responses
    private string CleanAndRepairJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return "{}";

        json = json.Trim();

        // Remove markdown code blocks
        if (json.StartsWith("```json"))
            json = json.Substring(7);
        else if (json.StartsWith("```"))
            json = json.Substring(3);

        if (json.EndsWith("```"))
            json = json.Substring(0, json.Length - 3);

        json = json.Trim();

        // Remove trailing commas before closing braces/brackets
        json = Regex.Replace(json, @",\s*([\]}])", @"$1");

        // ✅ NEW: Try to repair truncated JSON
        json = RepairTruncatedJson(json);

        return json;
    }

    // ✅ NEW: Attempt to repair JSON that was cut off mid-stream
    private string RepairTruncatedJson(string json)
    {
        // First, try parsing as-is
        try
        {
            JsonDocument.Parse(json);
            return json; // Already valid
        }
        catch
        {
            // Need to repair
        }

        _logger.LogWarning("⚠️ JSON appears truncated. Attempting to repair...");

        // Count open/close braces and brackets
        int openBraces = 0, closeBraces = 0;
        int openBrackets = 0, closeBrackets = 0;
        bool inString = false;
        bool escaped = false;

        foreach (char c in json)
        {
            if (escaped)
            {
                escaped = false;
                continue;
            }

            if (c == '\\' && inString)
            {
                escaped = true;
                continue;
            }

            if (c == '"')
            {
                inString = !inString;
                continue;
            }

            if (!inString)
            {
                switch (c)
                {
                    case '{': openBraces++; break;
                    case '}': closeBraces++; break;
                    case '[': openBrackets++; break;
                    case ']': closeBrackets++; break;
                }
            }
        }

        // If we're in a string, close it
        if (inString)
        {
            json += "\"";
        }

        // Close any open brackets/braces
        // We need to close them in reverse order of opening
        // Simple approach: close brackets first, then braces
        
        // If we have unclosed brackets, try to close them
        while (openBrackets > closeBrackets)
        {
            // Try to find the last incomplete array and close it
            json = json.TrimEnd();
            if (!json.EndsWith("]") && !json.EndsWith(","))
            {
                // Remove trailing partial content
                int lastComma = json.LastIndexOf(',');
                int lastOpenBracket = json.LastIndexOf('[');
                if (lastComma > lastOpenBracket && lastComma > json.LastIndexOf('}'))
                {
                    json = json.Substring(0, lastComma);
                }
            }
            json += "]";
            closeBrackets++;
        }

        // Close any open braces
        while (openBraces > closeBraces)
        {
            json = json.TrimEnd();
            if (!json.EndsWith("}") && !json.EndsWith(","))
            {
                int lastComma = json.LastIndexOf(',');
                int lastOpenBrace = json.LastIndexOf('{');
                if (lastComma > lastOpenBrace && lastComma > json.LastIndexOf(']'))
                {
                    json = json.Substring(0, lastComma);
                }
            }
            json += "}";
            closeBraces++;
        }

        _logger.LogInformation("✅ JSON repair attempted. Final length: {Length}", json.Length);
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

    public async Task<ApiResponse<List<Minutes>>> GetAllMinutesAsync(Guid userid)
    {
        
        var minutesList = await _context.Minutes
            .Include(m => m.Meeting)
            .Where(m => m.Meeting.CreatorId == userid) 
            .OrderByDescending(m => m.CreatedAt)
            .ToListAsync();
        
        return ApiResponse<List<Minutes>>.SuccessResponse(
            minutesList, 
            "Minutes retrieved successfully.", 
            statusCode: 200);
    }
}