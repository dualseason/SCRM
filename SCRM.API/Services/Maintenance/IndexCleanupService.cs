using Microsoft.EntityFrameworkCore;
using SCRM.API.Services.Data;
using SCRM.Services.Data;

namespace SCRM.API.Services.Maintenance
{
    public class IndexCleanupService : IHostedService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<IndexCleanupService> _logger;

        public IndexCleanupService(IServiceProvider serviceProvider, ILogger<IndexCleanupService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            using (var scope = _serviceProvider.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                try
                {
                    _logger.LogInformation("Checking for zombie temporary indexes...");
                    // Force drop the known problematic temp index
                    await context.Database.ExecuteSqlRawAsync(
                        "DROP INDEX IF EXISTS \"tempUniqueIndex_public_Contacts_WechatAccountId_Wxid\";", cancellationToken);
                    _logger.LogInformation("Successfully dropped zombie index 'tempUniqueIndex_public_Contacts_WechatAccountId_Wxid' if it existed.");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to clean up temporary indexes.");
                }
            }
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
