using finalyearproject.Models;
using finalyearproject.Wrappers;

namespace finalyearproject.Services.Interfaces;

public interface IParticipantService
{
    Task<ApiResponse<List<Participant>>> GetParticipantsByMeetingIdAsync(Guid meetingId);
    Task<ApiResponse<Participant>> AddParticipantAsync(Guid meetingId, Participant participant);
    Task<ApiResponse<bool>> AddParticipantsFromRecallAsync(Guid meetingId, List<RecallParticipant> recallParticipants);
    Task<ApiResponse<bool>> UpdateParticipantAsync(Guid participantId, Participant participant);
    Task<ApiResponse<bool>> DeleteParticipantAsync(Guid participantId);
}

public class RecallParticipant
{
    public string Name { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Role { get; set; }
    public bool IsHost { get; set; }
    public DateTime JoinedAt { get; set; }
    public DateTime? LeftAt { get; set; }
    public int DurationSeconds { get; set; }
}