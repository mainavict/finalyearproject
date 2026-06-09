using Microsoft.AspNetCore.SignalR;
using finalyearproject.Hubs;
using finalyearproject.Services.Interfaces;

namespace finalyearproject.Services.Implementation;

public class NotificationService : INotificationService
{
    private readonly IHubContext<NotificationHub> _hubContext;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        IHubContext<NotificationHub> hubContext,
        ILogger<NotificationService> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task SendTranscriptionUpdateAsync(Guid userId, Guid meetingId, string message, string status)
    {
        var notification = new
        {
            meetingId = meetingId.ToString(),
            message,
            status,
            type = "transcription",
            timestamp = DateTime.UtcNow
        };

        _logger.LogInformation(
            "📤 Sending transcription update to user {UserId} for meeting {MeetingId}: {Status}",
            userId, meetingId, status);

        await _hubContext.Clients.Group(userId.ToString())
            .SendAsync("ReceiveTranscriptionUpdate", notification);
    }

    public async Task SendMeetingUpdateAsync(Guid userId, Guid meetingId, string message, string status)
    {
        var notification = new
        {
            meetingId = meetingId.ToString(),
            message,
            status,
            type = "meeting",
            timestamp = DateTime.UtcNow
        };

        await _hubContext.Clients.Group(userId.ToString())
            .SendAsync("ReceiveMeetingUpdate", notification);
    }

    public async Task SendGeneralNotificationAsync(Guid userId, string title, string message, string type)
    {
        var notification = new
        {
            title,
            message,
            type,
            timestamp = DateTime.UtcNow
        };

        await _hubContext.Clients.Group(userId.ToString())
            .SendAsync("ReceiveNotification", notification);
    }
}