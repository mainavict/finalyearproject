using  finalyearproject.DTOs;
using  finalyearproject.Wrappers;
namespace finalyearproject.Services.Interfaces;

public interface Imeeting
{
    Task<ApiResponse<ResponseMeetingDto>> CreateMeetingAsync(CreateMeetingDto createDto, Guid userId);
    Task<ApiResponse<ResponseMeetingDto>> GetMeetingByIdAsync(Guid meetingId);
    Task<ApiResponse<IEnumerable<ResponseMeetingDto>>> GetMeetingsByUserIdAsync(Guid userId);
    Task<ApiResponse<IEnumerable<ResponseMeetingDto>>> GetAllMeetingsAsync();
    Task<ApiResponse<ResponseMeetingDto>> UpdateMeetingAsync(Guid meetingId, UpdateMeetingDto updateDto, Guid userId);
    Task<ApiResponse<bool>> DeleteMeetingAsync(Guid meetingId, Guid userId);
    Task<ApiResponse<ResponseMeetingDto>> UpdateMeetingStatusAsync(Guid meetingId, UpdateMeetingStatusDto statusDto, Guid userId);
    
    // Task<ApiResponse<ResponseMeetingDto>> ApproveMeetingAsync(Guid meetingId, ApproveMeetingDto approveDto, Guid userId);

    
}