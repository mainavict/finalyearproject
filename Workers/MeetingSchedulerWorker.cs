using Microsoft.EntityFrameworkCore;
using finalyearproject.Data;
using finalyearproject.DTOs;
using finalyearproject.Models;
using finalyearproject.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace finalyearproject.Workers;

public class MeetingSchedulerWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<MeetingSchedulerWorker> _logger;

    public MeetingSchedulerWorker(
        IServiceProvider serviceProvider,
        ILogger<MeetingSchedulerWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("📅 Meeting Scheduler Worker started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var recallService = scope.ServiceProvider.GetRequiredService<IRecallAiService>();

                var now = DateTime.UtcNow;

                // Find meetings scheduled in the next 2 minutes that are in "Scheduled" status
                // and have a MeetingUrl (Zoom/Teams/Meet link)
                var upcomingMeetings = await context.Meetings
                    .Where(m =>
                        m.ScheduledAt != null &&
                        m.ScheduledAt <= now.AddMinutes(2) &&
                        m.ScheduledAt >= now.AddMinutes(-1) &&
                        m.Status == MeetingStatus.Scheduled &&
                        m.RecallBotId == null && // Bot not yet deployed
                        !string.IsNullOrEmpty(m.AudioUrl)) // Has a meeting URL
                    .ToListAsync(stoppingToken);

                if (upcomingMeetings.Any())
                {
                    _logger.LogInformation("🔔 Found {Count} meetings to trigger bots for.", upcomingMeetings.Count);

                    foreach (var meeting in upcomingMeetings)
                    {
                        _logger.LogInformation("🤖 Triggering bot for meeting: {Title} at {Time}",
                            meeting.Title, meeting.ScheduledAt);

                        try
                        {
                            var botRequest = new RecallBotRequest
                            {
                                MeetingUrl = meeting.MeetingUrl!, // This is the Zoom/Teams link
                                BotName = "MeetingMind",
                                RecordingConfig = new RecallRecordingConfig
                                {
                                    Transcript = new RecallTranscriptConfig
                                    {
                                        Provider = new RecallTranscriptProvider
                                        {
                                            RecallAiStreaming = new RecallAiStreamingConfig()
                                        }
                                    }
                                }
                            };

                            var response = await recallService.CreateBotAsync(botRequest, stoppingToken);

                            if (response != null && !string.IsNullOrEmpty(response.Id))
                            {
                                meeting.RecallBotId = response.Id;
                                meeting.Status = MeetingStatus.Recording;
                                _logger.LogInformation("✅ Bot joined! BotId: {BotId}", response.Id);
                            }
                            else
                            {
                                meeting.Status = MeetingStatus.Failed;
                                _logger.LogWarning("⚠️ Bot creation returned empty response.");
                            }
                        }
                        catch (Exception ex)
                        {
                            meeting.Status = MeetingStatus.Failed;
                            _logger.LogError(ex, "❌ Failed to trigger bot for meeting {MeetingId}", meeting.Id);
                        }
                    }

                    await context.SaveChangesAsync(stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Meeting Scheduler Worker loop.");
            }

            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }
}