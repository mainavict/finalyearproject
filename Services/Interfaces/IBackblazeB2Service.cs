namespace finalyearproject.Services.Interfaces;

public interface IBackblazeB2Service
{
    Task<Stream> DownloadFileAsync(string b2FilePath);
    Task<string> GetSignedUrlAsync(string b2FilePath, int validDurationSeconds = 3600);
}