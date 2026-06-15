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
        _logger.LogInformation("📅 Meeting Scheduler Worker started. Checking every 30 seconds...");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var recallService = scope.ServiceProvider.GetRequiredService<IRecallAiService>();

                var now = DateTime.UtcNow;

                // ✅ EXPANDED WINDOW: Looks 24 hours in the past and 24 hours in the future
                // This ensures we don't miss meetings if the server was asleep or restarted.
                var upcomingMeetings = await context.Meetings
                    .Where(m =>
                        m.ScheduledAt != null &&
                        m.ScheduledAt <= now.AddHours(24) &&
                        m.ScheduledAt >= now.AddHours(-24) && 
                        m.Status == MeetingStatus.Scheduled &&
                        m.RecallBotId == null && // Bot not yet deployed
                        !string.IsNullOrEmpty(m.MeetingUrl)) // Has a meeting URL
                    .OrderBy(m => m.ScheduledAt) // Process oldest first
                    .ToListAsync(stoppingToken);

                if (upcomingMeetings.Any())
                {
                    _logger.LogInformation("Found {Count} meetings ready for bot deployment.", upcomingMeetings.Count);

                    foreach (var meeting in upcomingMeetings)
                    {
                        _logger.LogInformation("Triggering bot for meeting: '{Title}' (ID: {Id}) at {Time}",
                            meeting.Title, meeting.Id, meeting.ScheduledAt);

                        try
                        {
                            var botRequest = new RecallBotRequest
                            {
                                MeetingUrl = meeting.MeetingUrl!,
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

                            _logger.LogInformation(" Calling Recall.ai API for meeting {MeetingId}...", meeting.Id);
                            
                            var response = await recallService.CreateBotAsync(botRequest, stoppingToken);

                            if (response != null && !string.IsNullOrEmpty(response.Id))
                            {
                                meeting.RecallBotId = response.Id;
                                meeting.Status = MeetingStatus.Recording;
                                meeting.UpdatedAt = DateTime.UtcNow;
                                await context.SaveChangesAsync(stoppingToken);
                                _logger.LogInformation("✅ SUCCESS! Bot joined. BotId: {BotId}", response.Id);
                            }
                            else
                            {
                                meeting.Status = MeetingStatus.Failed;
                                meeting.UpdatedAt = DateTime.UtcNow;
                                await context.SaveChangesAsync(stoppingToken);
                                _logger.LogWarning("⚠️ Bot creation returned empty response for meeting {Id}.", meeting.Id);
                            }
                        }
                        catch (Exception ex)
                        {
                            meeting.Status = MeetingStatus.Failed;
                            meeting.UpdatedAt = DateTime.UtcNow;
                            await context.SaveChangesAsync(stoppingToken);
                            _logger.LogError(ex, "❌ FAILED to trigger bot for meeting {Id}. Error: {Message}", meeting.Id, ex.Message);
                        }
                    }
                }
                else
                {
                    _logger.LogDebug("No scheduled meetings found in the time window.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Critical error in Meeting Scheduler Worker loop.");
            }

            // Wait 30 seconds before checking again
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }
}