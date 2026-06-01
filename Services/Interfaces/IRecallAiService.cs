using finalyearproject.DTOs;

namespace finalyearproject.Services.Interfaces;

public interface IRecallAiService
{
    /// <summary>Sends a recording bot to a meeting URL and returns the bot object.</summary>
    Task<RecallBotResponse> CreateBotAsync(string meetingUrl, string botName = "Minutes Assistant");

    /// <summary>Retrieves the media download links for a completed recording.</summary>
    Task<RecallRecordingMedia> GetRecordingMediaAsync(string recordingId);

    /// <summary>Downloads the audio stream for a given recording.</summary>
    Task<(Stream stream, string contentType, string fileName)> DownloadRecordingAudioAsync(string audioUrl);
}
