using System.Text;
using System.Text.Json;
using finalyearproject.Services.Interfaces;

namespace finalyearproject.Services.Implementation;

public class BackblazeB2Service : IBackblazeB2Service
{
    private readonly IConfiguration _configuration;
    private readonly HttpClient _httpClient;
    private readonly ILogger<BackblazeB2Service> _logger;

    private string? _authToken;
    private string? _apiUrl;
    private string? _downloadUrl;
    private DateTime _authExpiry = DateTime.MinValue;

    public BackblazeB2Service(
        IConfiguration configuration,
        HttpClient httpClient,
        ILogger<BackblazeB2Service> logger)
    {
        _configuration = configuration;
        _httpClient    = httpClient;
        _logger        = logger;
    }

    private async Task EnsureAuthorizedAsync()
    {
        if (_authToken != null && DateTime.UtcNow < _authExpiry)
            return;

        var keyId  = _configuration["BackblazeB2:ApplicationKeyId"]?.Trim();
        var appKey = _configuration["BackblazeB2:ApplicationKey"]?.Trim();

        if (string.IsNullOrEmpty(keyId) || string.IsNullOrEmpty(appKey))
            throw new InvalidOperationException(
                "BackblazeB2:ApplicationKeyId or BackblazeB2:ApplicationKey is missing from configuration.");

        var credentials = Convert.ToBase64String(
            Encoding.ASCII.GetBytes($"{keyId}:{appKey}"));

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "https://api.backblazeb2.com/b2api/v3/b2_authorize_account");

        request.Headers.TryAddWithoutValidation("Authorization", $"Basic {credentials}");

        var response = await _httpClient.SendAsync(request);
        var body     = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException(
                $"B2 authorization failed ({response.StatusCode}): {body}");

        using var doc = JsonDocument.Parse(body);
        var root      = doc.RootElement;

        // ── v3 API: apiUrl and downloadUrl are nested under apiInfo.storageApi ──
        var storageApi = root
            .GetProperty("apiInfo")
            .GetProperty("storageApi");

        _authToken   = root.GetProperty("authorizationToken").GetString()!;
        _apiUrl      = storageApi.GetProperty("apiUrl").GetString()!;
        _downloadUrl = storageApi.GetProperty("downloadUrl").GetString()!;
        _authExpiry  = DateTime.UtcNow.AddHours(23);

        _logger.LogInformation(
            "B2 authorized. ApiUrl={ApiUrl} | DownloadUrl={DownloadUrl}",
            _apiUrl, _downloadUrl);
    }
    public async Task<Stream> DownloadFileAsync(string b2FilePath)
    {
        await EnsureAuthorizedAsync();

        var bucketName = _configuration["BackblazeB2:BucketName"];

        var encodedPath = string.Join("/",
            b2FilePath.Split('/').Select(Uri.EscapeDataString));

        var url = $"{_downloadUrl}/file/{bucketName}/{encodedPath}";

        _logger.LogInformation("Downloading from B2 URL: {Url}", url);

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("Authorization", _authToken);

        var response = await _httpClient.SendAsync(
            request, HttpCompletionOption.ResponseHeadersRead);

        if (!response.IsSuccessStatusCode)
        {
            var errBody = await response.Content.ReadAsStringAsync();
            _logger.LogError(
                "B2 download failed [{Status}]: {Body}",
                (int)response.StatusCode, errBody);
            throw new HttpRequestException(
                $"B2 download failed ({response.StatusCode}): {errBody}");
        }

        return await response.Content.ReadAsStreamAsync();
    }

    public async Task<string> GetSignedUrlAsync(
        string b2FilePath,
        int validDurationSeconds = 3600)
    {
        await EnsureAuthorizedAsync();

        var bucketId   = _configuration["BackblazeB2:BucketId"];
        var bucketName = _configuration["BackblazeB2:BucketName"];

        if (string.IsNullOrEmpty(bucketId))
            throw new InvalidOperationException(
                "BackblazeB2:BucketId is missing from configuration.");

        var bodyJson = JsonSerializer.Serialize(new
        {
            bucketId,
            fileNamePrefix         = b2FilePath,
            validDurationInSeconds = validDurationSeconds
        });

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"{_apiUrl}/b2api/v3/b2_get_download_authorization");

        request.Headers.TryAddWithoutValidation("Authorization", _authToken);
        request.Content = new StringContent(bodyJson, Encoding.UTF8, "application/json");

        var response     = await _httpClient.SendAsync(request);
        var responseBody = await response.Content.ReadAsStringAsync();

        _logger.LogInformation(
            "B2 download auth response [{Status}]: {Body}",
            (int)response.StatusCode, responseBody);

        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException(
                $"B2 get_download_authorization failed ({response.StatusCode}): {responseBody}");

        using var doc = JsonDocument.Parse(responseBody);
        var root      = doc.RootElement;

        if (!root.TryGetProperty("authorizationToken", out var tkEl))
            throw new InvalidOperationException(
                $"Missing 'authorizationToken' in signed URL response: {responseBody}");

        var token       = tkEl.GetString()!;
        var encodedPath = string.Join("/",
            b2FilePath.Split('/').Select(Uri.EscapeDataString));

        return $"{_downloadUrl}/file/{bucketName}/{encodedPath}" +
               $"?Authorization={Uri.EscapeDataString(token)}";
    }
}