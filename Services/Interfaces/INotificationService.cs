namespace finalyearproject.Services.Interfaces;

public interface INotificationService
{
    Task SendTranscriptionUpdateAsync(Guid userId, Guid meetingId, string message, string status);
    Task SendMeetingUpdateAsync(Guid userId, Guid meetingId, string message, string status);
    Task SendGeneralNotificationAsync(Guid userId, string title, string message, string type);
}