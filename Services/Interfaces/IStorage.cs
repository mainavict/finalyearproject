namespace finalyearproject.Services.Interfaces;

public interface IStorage
{
    Task<string> UploadAsync(IFormFile file, string folder);
    Task<string> GetPresignedUploadUrlAsync(string key, string contentType, int expirationMinutes = 15);
    string GetFileUrl(string fileKey);
    Task DeleteAsync(string fileId);
}