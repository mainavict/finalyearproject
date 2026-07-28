using Microsoft.EntityFrameworkCore;
using finalyearproject.Data;
using finalyearproject.DTOs;
using finalyearproject.Models;
using finalyearproject.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Polly;

namespace finalyearproject.Workers;

public class MeetingSchedulerWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<MeetingSchedulerWorker> _logger;
    
    // ✅ CONFIGURATION
    private readonly TimeSpan _checkInterval = TimeSpan.FromSeconds(10); // Check every 10 seconds
    private readonly TimeSpan _deployBeforeMeeting = TimeSpan.FromMinutes(1); // Deploy bot 1 min before
    private readonly TimeSpan _maxLookAhead = TimeSpan.FromHours(24); // Look 24 hours ahead
    private readonly int _maxPastMinutes = 5; // Don't process meetings more than 5 min in the past
    private readonly int _maxRetryAttempts = 3;
    
    // Circuit breaker state
    private int _consecutiveFailures = 0;
    private DateTime _circuitOpenUntil = DateTime.MinValue;
    private const int CircuitBreakThreshold = 5;
    private readonly TimeSpan _circuitResetTime = TimeSpan.FromMinutes(5);

    public MeetingSchedulerWorker(
        IServiceProvider serviceProvider,
        ILogger<MeetingSchedulerWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("📅 Meeting Scheduler Worker started");
        _logger.LogInformation("⚙️ Configuration: Deploy bot {Minutes} min before meeting, check every {Seconds}s", 
            _deployBeforeMeeting.TotalMinutes, _checkInterval.TotalSeconds);

        // Graceful startup delay
        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessMeetingsAsync(stoppingToken);
                await Task.Delay(_checkInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("🛑 Meeting Scheduler Worker shutting down gracefully");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Critical error in scheduler loop. Will retry in {Delay}s", 
                    _checkInterval.TotalSeconds);
                await Task.Delay(_checkInterval, stoppingToken);
            }
        }
    }

    private async Task ProcessMeetingsAsync(CancellationToken stoppingToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var recallService = scope.ServiceProvider.GetRequiredService<IRecallAiService>();

        var now = DateTime.UtcNow;

        // Check circuit breaker
        if (DateTime.UtcNow < _circuitOpenUntil)
        {
            _logger.LogDebug("⚡ Circuit breaker OPEN. Skipping check until {ResetTime}", _circuitOpenUntil);
            return;
        }

        // ✅ SMART TIME WINDOW
        // We want meetings that:
        // 1. Start within the next 24 hours (to prepare)
        // 2. But haven't started more than 5 minutes ago (don't process stale meetings)
        var pastCutoff = now.AddMinutes(-_maxPastMinutes);
        var futureCutoff = now.Add(_maxLookAhead);

        // ✅ FIND ALL UPCOMING MEETINGS (we'll filter for deployment time below)
        var upcomingMeetings = await context.Meetings
            .Where(m =>
                m.ScheduledAt != null &&
                m.ScheduledAt >= pastCutoff &&
                m.ScheduledAt <= futureCutoff &&
                m.Status == MeetingStatus.Scheduled &&
                m.RecallBotId == null &&
                !string.IsNullOrEmpty(m.MeetingUrl) &&
                !m.IsDeleted)
            .OrderBy(m => m.ScheduledAt)
            .Take(20) // Process in batches
            .ToListAsync(stoppingToken);

        if (!upcomingMeetings.Any())
        {
            _logger.LogDebug("🔍 No scheduled meetings found");
            return;
        }

        _logger.LogInformation("📋 Found {Count} scheduled meeting(s)", upcomingMeetings.Count);

        int deployedCount = 0;
        int waitingCount = 0;

        foreach (var meeting in upcomingMeetings)
        {
            if (stoppingToken.IsCancellationRequested) break;

            var timeUntilMeeting = meeting.ScheduledAt!.Value - now;
            var deployTime = meeting.ScheduledAt.Value - _deployBeforeMeeting;
            var timeUntilDeploy = deployTime - now;

            // ✅ DECISION: Should we deploy NOW or WAIT?
            if (timeUntilDeploy <= TimeSpan.Zero)
            {
                // ✅ TIME TO DEPLOY! (Meeting starts in <= 1 minute)
                _logger.LogInformation("🚀 DEPLOYING bot for meeting '{Title}' (ID: {Id}) - starts in {Minutes} min",
                    meeting.Title, meeting.Id, timeUntilMeeting.TotalMinutes.ToString("F1"));

                try
                {
                    var success = await DeployBotAsync(context, recallService, meeting, stoppingToken);
                    
                    if (success)
                    {
                        deployedCount++;
                        _consecutiveFailures = 0;
                    }
                    else
                    {
                        _consecutiveFailures++;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ FAILED to deploy bot for meeting {MeetingId}", meeting.Id);
                    await MarkMeetingAsFailed(context, meeting, ex.Message);
                    _consecutiveFailures++;
                }
            }
            else
            {
                // ⏳ NOT YET - Meeting is too far in the future
                waitingCount++;
                _logger.LogDebug("⏳ Waiting to deploy bot for '{Title}' - deploy in {Minutes} min",
                    meeting.Title, timeUntilDeploy.TotalMinutes.ToString("F1"));
            }

            // Check circuit breaker
            if (_consecutiveFailures >= CircuitBreakThreshold)
            {
                _circuitOpenUntil = DateTime.UtcNow.Add(_circuitResetTime);
                _logger.LogError("⚡ CIRCUIT BREAKER TRIGGERED! Pausing until {ResetTime}", _circuitOpenUntil);
                break;
            }
        }

        if (deployedCount > 0 || waitingCount > 0)
        {
            _logger.LogInformation("📊 Batch summary: {Deployed} deployed, {Waiting} waiting", 
                deployedCount, waitingCount);
        }
    }

    private async Task<bool> DeployBotAsync(
        AppDbContext context, 
        IRecallAiService recallService, 
        Meeting meeting, 
        CancellationToken stoppingToken)
    {
        // ✅ RETRY POLICY with exponential backoff
        var retryPolicy = Policy
            .Handle<HttpRequestException>()
            .Or<TaskCanceledException>()
            .WaitAndRetryAsync(
                _maxRetryAttempts,
                retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                (exception, timeSpan, retryCount, ctx) =>
                {
                    _logger.LogWarning("🔄 Retry {RetryCount}/{MaxRetries} for meeting {MeetingId} after {Delay}s",
                        retryCount, _maxRetryAttempts, meeting.Id, timeSpan.TotalSeconds);
                });

        var botRequest = new RecallBotRequest
        {
            MeetingUrl = meeting.MeetingUrl!,
            BotName = "Mintra",
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

        var response = await retryPolicy.ExecuteAsync(async () =>
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            cts.CancelAfter(TimeSpan.FromSeconds(30)); // 30 second timeout
            return await recallService.CreateBotAsync(botRequest, cts.Token);
        });

        if (response != null && !string.IsNullOrEmpty(response.Id))
        {
            meeting.RecallBotId = response.Id;
            meeting.Status = MeetingStatus.Recording;
            meeting.UpdatedAt = DateTime.UtcNow;
            await context.SaveChangesAsync(stoppingToken);
            
            _logger.LogInformation("✅ SUCCESS! Bot deployed for '{Title}'. BotId: {BotId}", 
                meeting.Title, response.Id);
            
            return true;
        }
        else
        {
            await MarkMeetingAsFailed(context, meeting, "Bot creation returned empty response");
            return false;
        }
    }

    private async Task MarkMeetingAsFailed(AppDbContext context, Meeting meeting, string reason)
    {
        meeting.Status = MeetingStatus.Failed;
        meeting.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();
        _logger.LogWarning("⚠️ Meeting {MeetingId} marked as FAILED: {Reason}", meeting.Id, reason);
    }
}