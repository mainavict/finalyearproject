

namespace finalyearproject.Services.Interfaces;

public interface IStrorage
{
    Task<string> SaveFileAsync(IFormFile file, string folder);
}