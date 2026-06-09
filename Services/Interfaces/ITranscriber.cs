namespace finalyearproject.Services.Interfaces;

public interface ITranscriber
{
    Task<string> TranscribeAsync(string filePath);                          // existing - local file
    Task<string> TranscribeFromStreamAsync(Stream audioStream, string fileName); // new - B2 stream
}