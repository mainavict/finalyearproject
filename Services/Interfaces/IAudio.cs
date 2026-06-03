using finalyearproject.DTOs;
using finalyearproject.Models;
using  finalyearproject.Wrappers;

namespace finalyearproject.Services.Interfaces;

public interface IAudio
{
//     Task<AudioUploadResult> UploadAsync(IFormFile file);

    Task<ApiResponse<AudioFile>> UploadAsync(Guid MeetindId ,IFormFile file);
    Task<ApiResponse<AudioFile>> GetAudioByMeetingIdAsync(Guid meetingId);
    Task <ApiResponse<List<AudioFile>>> GetAllAudioFilesAsync();

    Task<ApiResponse<List<AudioFile>>> GetAllAudioFileByUserIdAsync(Guid id);
}