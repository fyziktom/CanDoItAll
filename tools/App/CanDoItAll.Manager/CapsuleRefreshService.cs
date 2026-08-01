namespace CanDoItAll.Manager;

public sealed class CapsuleRefreshService(CapsuleCatalogService capsuleCatalogService, ILogger<CapsuleRefreshService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await capsuleCatalogService.RefreshAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Capsule refresh failed.");
            }

            await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
        }
    }
}
