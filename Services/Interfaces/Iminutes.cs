using finalyearproject.Models;
using  finalyearproject.Wrappers;
namespace finalyearproject.Services.Interfaces;

public interface Iminutes
{
    Task<string> GenerateMinutesAsync( Guid transcriptId);
    Task <ApiResponse<Minutes>> GetMinutesByMeetingIdAsync(Guid meetingId);
     Task<ApiResponse<List<Minutes>>> GetAllMinutesAsync(Guid Userid);
     // Task<Minutes> UpdateMinutesAsync(Guid minutesId, string draftJson);
     // Task<Minutes> ApproveMinutesAsync(Guid minutesId, Guid approverId);
     // Task<List<Minutes>> GetMinutesHistoryAsync(Guid meetingId);
}