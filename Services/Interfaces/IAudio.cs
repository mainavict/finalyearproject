using finalyearproject.Models;
using finalyearproject.Wrappers;

namespace finalyearproject.Services.Interfaces;

public interface IAudio
{
    Task<ApiResponse<AudioFile>> UploadAsync(Guid MeetingId, IFormFile file);
    Task<ApiResponse<AudioFile>> GetAudioByMeetingIdAsync(Guid meetingId);
    Task<ApiResponse<List<AudioFile>>> GetAllAudioFilesAsync();
    Task<ApiResponse<List<AudioFile>>> GetAllAudioFileByUserIdAsync(Guid id);
    Task<ApiResponse<AudioFile>> CreateAudioRecordAsync(AudioFile audioFile);
}