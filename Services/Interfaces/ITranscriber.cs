namespace finalyearproject.Services.Interfaces;

public interface ITranscriber
{
    Task<string> TranscribeAsync(string filePath);
}