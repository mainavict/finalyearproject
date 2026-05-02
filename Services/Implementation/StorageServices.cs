using finalyearproject.Services.Interfaces;

namespace finalyearproject.Services.Implementation;

public class LocalFileStorageService:IStrorage
{
    private readonly IWebHostEnvironment _env;

    public LocalFileStorageService(IWebHostEnvironment env)
    {
        _env = env;
    }
    public async Task<string> SaveFileAsync(
        IFormFile file,
        string folder)
    {
        var uploadsFolder =
            Path.Combine(_env.WebRootPath, folder);

        if (!Directory.Exists(uploadsFolder))
            Directory.CreateDirectory(uploadsFolder);

        var uniqueFileName =
            $"{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";

        var fullPath =
            Path.Combine(uploadsFolder, uniqueFileName);

        using var stream =
            new FileStream(fullPath, FileMode.Create);

        await file.CopyToAsync(stream);

        return $"/{folder}/{uniqueFileName}";
    }
    
}