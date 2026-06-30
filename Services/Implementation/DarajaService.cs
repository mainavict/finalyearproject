using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using finalyearproject.DTOs;
using finalyearproject.Services.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace finalyearproject.Services.Implementation;

public class DarajaService : IDarajaService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _config;
    private readonly ILogger<DarajaService> _logger;

    // Sandbox Base URL (Change to https://api.safaricom.co.ke for production)
    private const string BaseUrl = "https://sandbox.safaricom.co.ke";

    public DarajaService(HttpClient httpClient, IConfiguration config, ILogger<DarajaService> logger)
    {
        _httpClient = httpClient;
        _config = config;
        _logger = logger;
    }

    public async Task<string> GetAccessTokenAsync()
    {
        var consumerKey = _config["Daraja:ConsumerKey"];
        var consumerSecret = _config["Daraja:ConsumerSecret"];
        
        if (string.IsNullOrEmpty(consumerKey) || string.IsNullOrEmpty(consumerSecret))
            throw new Exception("Daraja Consumer Key or Secret is missing in configuration.");

        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{consumerKey}:{consumerSecret}"));

        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);

        var response = await _httpClient.GetAsync($"{BaseUrl}/oauth/v1/generate?grant_type=client_credentials");
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<JsonElement>(content);
        
        return result.GetProperty("access_token").GetString() 
            ?? throw new Exception("Failed to extract access_token from Daraja response.");
    }

    public async Task<StkPushResponseDto> InitiateStkPushAsync(StkPushRequestDto request)
    {
        var token = await GetAccessTokenAsync();
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var shortcode = _config["Daraja:Shortcode"];
        var passkey = _config["Daraja:Passkey"];
        var callbackUrl = _config["Daraja:CallbackUrl"];

        var timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
        var password = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{shortcode}{passkey}{timestamp}"));

        // Format phone number (Ensure it starts with 254)
        var phoneNumber = request.PhoneNumber.Trim();
        if (phoneNumber.StartsWith("+")) phoneNumber = phoneNumber.Substring(1);
        if (phoneNumber.StartsWith("0")) phoneNumber = "254" + phoneNumber.Substring(1);
        if (!phoneNumber.StartsWith("254")) phoneNumber = "254" + phoneNumber;

        var payload = new
        {
            BusinessShortCode = shortcode,
            Password = password,
            Timestamp = timestamp,
            TransactionType = "CustomerPayBillOnline",
            Amount = request.Amount,
            PartyA = phoneNumber,
            PartyB = shortcode,
            PhoneNumber = phoneNumber,
            CallBackURL = callbackUrl,
            AccountReference = request.UserId, // Pass UserId as AccountReference
            TransactionDesc = $"MeetingApp {request.PlanType} Subscription"
        };

        var json = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync($"{BaseUrl}/mpesa/stkpush/v1/processrequest", content);
        var responseContent = await response.Content.ReadAsStringAsync();

        _logger.LogInformation("Daraja STK Push Raw Response: {Response}", responseContent);

        if (!response.IsSuccessStatusCode)
        {
            throw new Exception($"Daraja API Error: {responseContent}");
        }

        return JsonSerializer.Deserialize<StkPushResponseDto>(responseContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) 
            ?? throw new Exception("Failed to parse STK push response.");
    }
}