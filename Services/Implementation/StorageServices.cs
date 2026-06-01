using Amazon.S3;
using Amazon.S3.Model;
using finalyearproject.Services.Interfaces;
using Microsoft.Extensions.Configuration;

public class BackblazeStorage : IStorage
{
    private readonly IAmazonS3 _client;
    private readonly string _bucketName;
    private readonly string _endpoint;

    public BackblazeStorage(IConfiguration config)
    {
        var accessKey = config["BackblazeB2:ApplicationKeyId"];
        var secretKey = config["BackblazeB2:ApplicationKey"];
        _endpoint   = config["BackblazeB2:Endpoint"]!.TrimEnd('/');

        _bucketName = config["BackblazeB2:BucketName"]!;

        var configS3 = new AmazonS3Config
        {
            ServiceURL = _endpoint,
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
        return $"{_endpoint}/{_bucketName}/{fileKey}";
    }

    public Task DeleteAsync(string fileId)
    {
        return _client.DeleteObjectAsync(_bucketName, fileId);
    }
}