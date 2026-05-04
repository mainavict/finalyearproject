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
        var accessKey = config["Backblaze:KeyId"];
        var secretKey = config["Backblaze:ApplicationKey"];
        var endpoint = config["Backblaze:Endpoint"];

        _bucketName = config["Backblaze:Bucket"];

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
            ContentType = file.ContentType
        };

        await _client.PutObjectAsync(request);

        return key; // store this in DB
    }

    public string GetFileUrl(string fileKey)
    {
        return $"https://f004.backblazeb2.com/file/{_bucketName}/{fileKey}";
    }

    public Task DeleteAsync(string fileId)
    {
        return _client.DeleteObjectAsync(_bucketName, fileId);
    }
}