using Microsoft.EntityFrameworkCore;
using SCRM.API.Services.Data;
using SCRM.Services.Data;

namespace SCRM.API.Services.Maintenance;

public class MessageCleanupService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<MessageCleanupService> _logger;
    private readonly IConfiguration _configuration;

    public MessageCleanupService(
        IServiceProvider serviceProvider, 
        ILogger<MessageCleanupService> logger,
        IConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Message Cleanup Service is starting.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanupOldMessagesAsync(stoppingToken);

                // Calculate time until next run (e.g., next 4:00 AM)
                var now = DateTime.Now;
                var tomorrow4am = now.Date.AddDays(1).AddHours(4);
                var delay = tomorrow4am - now;

                _logger.LogInformation("Next cleanup scheduled at: {Time}", tomorrow4am);
                
                // Wait for 24 hours (or until next 4 AM)
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Graceful shutdown
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during message cleanup.");
                // Retry in 1 hour if failed
                await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
            }
        }
    }

    private async Task CleanupOldMessagesAsync(CancellationToken cancellationToken)
    {
        // Get retention days from config (default 180 days)
        var retentionDays = _configuration.GetValue<int>("Maintenance:MessageFreeDays", 180);
        
        if (retentionDays <= 0)
        {
            _logger.LogWarning("Message retention policy is disabled (Days <= 0). Skipping cleanup.");
            return;
        }

        var thresholdDate = DateTime.UtcNow.AddDays(-retentionDays);
        _logger.LogInformation("Starting cleanup of messages older than {Date} ({Days} days retention)...", thresholdDate, retentionDays);

        using (var scope = _serviceProvider.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            
            // Execute Delete Command
            // Note: We use ExecuteSqlRaw for efficiency to avoid loading entities into memory
            // We use standard SQL parameterization for safety
            
            // 1. Delete Messages
            var deletedCount = await context.Database.ExecuteSqlRawAsync(
                "DELETE FROM \"Messages\" WHERE \"created_at\" < {0}", 
                new object[] { thresholdDate }, 
                cancellationToken);

            _logger.LogInformation("Cleanup completed. Deleted {Count} old messages.", deletedCount);
            
            // 2. Cleanup Large Files
            await CleanupLargeFilesAsync(context, cancellationToken);
        }
    }

    private async Task CleanupLargeFilesAsync(ApplicationDbContext context, CancellationToken cancellationToken)
    {
        // 1. Get Config
        var limitMb = _configuration.GetValue<int>("Maintenance:LargeFileThresholdMb", 10);
        var retentionDays = _configuration.GetValue<int>("Maintenance:LargeFileRetentionDays", 3);

        if (limitMb <= 0 || retentionDays <= 0) return;

        var thresholdDate = DateTime.UtcNow.AddDays(-retentionDays);
        var limitBytes = limitMb * 1024 * 1024L;

        _logger.LogInformation("Scanning for large files > {MB}MB and older than {Date}...", limitMb, thresholdDate);

        // 2. Query Candidates (Batching to avoid memory issues)
        // We look for files that have a local path and exceed size/age
        var candidates = await context.MessageMedias
            .Where(m => !string.IsNullOrEmpty(m.localPath) 
                        && m.fileSize > limitBytes 
                        && m.createdAt < thresholdDate)
            .OrderBy(m => m.createdAt) // Resolve EF Core Warning: Row limiting operation without OrderBy
            .Take(100) // Process in batches of 100
            .ToListAsync(cancellationToken);

        if (!candidates.Any()) return;

        int deletedCount = 0;
        foreach (var media in candidates)
        {
            if (cancellationToken.IsCancellationRequested) break;

            try
            {
                if (File.Exists(media.localPath))
                {
                    File.Delete(media.localPath);
                    _logger.LogDebug("Deleted physical file: {Path}", media.localPath);
                }
                
                // Update DB Record
                media.localPath = ""; // Clear path
                media.uploadStatus = -1; // Mark as expired/deleted
                // media.mediaUrl ? We might keep the URL if it points to a remote server, 
                // but if localPath was the source of truth, it's gone.
                // Assuming mediaUrl might be valid if uploaded to cloud, otherwise it's just local.
                
                deletedCount++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete file: {Path}", media.localPath);
            }
        }

        await context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Large file cleanup: Processed {Count} files.", deletedCount);
    }
}
