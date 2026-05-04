using Amazon.S3;
using Amazon.S3.Model;

namespace finalyearproject.Services.Implementation;

public class BackblazePresignedUrlService
{
    private readonly IConfiguration _configuration;

    public BackblazePresignedUrlService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string GeneratePreSignedUrl(string objectKey)
    {
        var b2Config = _configuration.GetSection("BackblazeB2");
        
        var config = new AmazonS3Config
        {
            ServiceURL = $"https://{b2Config["Endpoint"]}"
        };

        // Initialize S3 Client with Backblaze App Keys
        using var s3Client = new AmazonS3Client(
            b2Config["KeyId"],
            b2Config["ApplicationKey"],
            config
        );

        var request = new GetPreSignedUrlRequest
        {
            BucketName = b2Config["BucketName"],
            Key = objectKey,
            Expires = DateTime.UtcNow.AddMinutes(15) // URL valid for 15 minutes
        };

        return s3Client.GetPreSignedURL(request);
    }
}