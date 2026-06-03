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
                // IMPORTANT: BackgroundServices run outside of HTTP requests.
                // We MUST create a new Dependency Injection scope to use the DbContext.
                using var scope = _serviceProvider.CreateScope();
                
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var processor = scope.ServiceProvider.GetRequiredService<IAudioProcessingService>();

                // Find ONE pending audio file to process
                var pendingAudio = await context.AudioFiles
                    .FirstOrDefaultAsync(a => a.Status == stutus.pending, stoppingToken);
                

                if (pendingAudio != null)
                {
                    _logger.LogInformation("Worker found pending audio: {AudioId}", pendingAudio.Id);
                    
                    // Hand it over to the Manager Service
                    await processor.ProcessAudioAsync(pendingAudio.Id);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred in TranscriptionWorker.");
            }

            // Wait 5 seconds before checking the database again
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }
}