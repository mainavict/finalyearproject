using System.IO;
using System.Threading;
using System.Threading.Tasks;
using finalyearproject.DTOs;

namespace finalyearproject.Services.Interfaces;

public interface IRecallAiService
{
    Task<RecallBotResponse?> CreateBotAsync(RecallBotRequest request, CancellationToken ct = default);
    Task<RecallRecordingMedia?> GetRecordingMediaAsync(string recordingId, CancellationToken ct = default);
    Task<(Stream Stream, string ContentType, string FileName)> DownloadRecordingAudioAsync(string audioUrl, CancellationToken ct = default);
    
    Task<string> GetTranscriptAsync(string botId, CancellationToken ct = default);
}

