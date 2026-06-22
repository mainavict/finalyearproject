using Amazon.S3;
using Amazon.S3.Model;
using finalyearproject.Services.Interfaces;
using Microsoft.Extensions.Configuration;

public class BackblazeStorage : IStorage
{
    private readonly IAmazonS3 _client;
    private readonly string _bucketName;

    public BackblazeStorage(IConfiguration config)
    {
        var accessKey = config["BackblazeB2:ApplicationKeyId"];
        var secretKey = config["BackblazeB2:ApplicationKey"];
        var endpoint = config["BackblazeB2:Endpoint"];

        _bucketName = config["BackblazeB2:BucketName"]!;

        var configS3 = new AmazonS3Config
        {
            ServiceURL = endpoint,
            ForcePathStyle = true
        };

        _client = new AmazonS3Client(accessKey, secretKey, configS3);
    }

    public async Task<string> UploadAsync(IFormFile file, string folder)
    {
        var safeFileName = Path.GetFileNameWithoutExtension(file.FileName)
            .Replace(" ", "_")
            .Replace("[", "")
            .Replace("]", "")
            .Replace("(", "")
            .Replace(")", "");

        var extension = Path.GetExtension(file.FileName);
        var key = $"{folder}/{Guid.NewGuid()}_{safeFileName}{extension}";

        using var stream = file.OpenReadStream();

        var request = new PutObjectRequest
        {
            BucketName = _bucketName,
            Key = key,
            InputStream = stream,
            ContentType = file.ContentType,
            Headers = { ContentLength = file.Length }
        };

        await _client.PutObjectAsync(request);
        return key;
    }

    public async Task<string> GetPresignedUploadUrlAsync(string key, string contentType, int expirationMinutes = 15)
    {
        var request = new GetPreSignedUrlRequest
        {
            BucketName = _bucketName,
            Key = key,
            Verb = HttpVerb.PUT,
            Expires = DateTime.UtcNow.AddMinutes(expirationMinutes),
            ContentType = contentType
        };

        var presignedUrl = await _client.GetPreSignedURLAsync(request);
        return presignedUrl;
    }

    public string GetFileUrl(string fileKey)
    {
        return $"https://f004.backblazeb2.com/file/{_bucketName}/{fileKey}";
    }

    public Task DeleteAsync(string fileId)
    {
        return _client.DeleteObjectAsync(_bucketName, fileId);
    }
    
    
    
    public async Task<string> UploadStreamAsync(Stream stream, string fileName, string contentType, string folder)
    {
        var safeFileName = Path.GetFileNameWithoutExtension(fileName)
            .Replace(" ", "_")
            .Replace("[", "")
            .Replace("]", "")
            .Replace("(", "")
            .Replace(")", "");

        var extension = Path.GetExtension(fileName);
        var key = $"{folder}/{Guid.NewGuid()}_{safeFileName}{extension}";

        var request = new PutObjectRequest
        {
            BucketName = _bucketName,
            Key = key,
            InputStream = stream,
            ContentType = contentType,
            Headers = { ContentLength = stream.Length }
        };

        await _client.PutObjectAsync(request);
        return key;
    }
    
    
    
    
    
}