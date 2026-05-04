

namespace finalyearproject.Services.Interfaces;

public interface IStorage
{
    Task<string> UploadAsync(IFormFile file, string folder);
    string GetFileUrl(string fileId);
    Task DeleteAsync(string fileId);
}