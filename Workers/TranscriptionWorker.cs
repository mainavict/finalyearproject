using Microsoft.EntityFrameworkCore;
using finalyearproject.Data;
using finalyearproject.Services.Implementation;
using finalyearproject.Services.Interfaces;
using finalyearproject.Models;

namespace finalyearproject.Workers;

public class TranscriptionWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<TranscriptionWorker> _logger;
    
    public TranscriptionWorker(IServiceProvider serviceProvider, ILogger<TranscriptionWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Transcription Background Worker started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var processor = scope.ServiceProvider.GetRequiredService<IAudioProcessingService>();
                var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();
                
                var pendingAudio = await context.AudioFiles
                    .Include(a => a.Meeting)
                    .FirstOrDefaultAsync(a => a.Status == stutus.pending, stoppingToken);

                if (pendingAudio != null)
                {
                    _logger.LogInformation("Worker found pending audio: {AudioId}", pendingAudio.Id);
                    
                    var userId = pendingAudio.Meeting?.CreatorId ?? Guid.Empty;
               
                    // Send "processing" notification
                    await notificationService.SendTranscriptionUpdateAsync(
                        userId,
                        pendingAudio.MeetingId,
                        $"Processing audio: {pendingAudio.FileName}",
                        "prossesing"
                    );

                    try
                    {
                        await processor.ProcessAudioAsync(pendingAudio.Id);

                        // Send "completed" notification
                        await notificationService.SendTranscriptionUpdateAsync(
                            userId,
                            pendingAudio.MeetingId,
                            "✅ Transcription complete! Meeting minutes are ready.",
                            "transcribed"
                        );
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to process audio {AudioId}", pendingAudio.Id);
                        
                        // Send "failed" notification
                        await notificationService.SendTranscriptionUpdateAsync(
                            userId,
                            pendingAudio.MeetingId,
                            $"❌ Transcription failed: {ex.Message}",
                            "failed"
                        );
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred in TranscriptionWorker.");
            }
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }
}