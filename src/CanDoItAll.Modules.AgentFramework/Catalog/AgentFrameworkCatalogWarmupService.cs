using System.Diagnostics;
using CanDoItAll.Modules.AgentFramework.Hosting;
using CanDoItAll.Modules.CrmHr;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.AgentFramework;

internal sealed class AgentFrameworkCatalogWarmupService(
    IAgentFrameworkOrganizationCatalogRepairService organizationCatalogRepairService,
    ProcessMockAgentCatalogService processMockAgentCatalogService,
    IAiTechnicalAgentBridge technicalAgentBridge,
    ILogger<AgentFrameworkCatalogWarmupService> logger)
{
    public async Task WarmupAsync(CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        await organizationCatalogRepairService.EnsureCurrentOrganizationCatalogAsync(cancellationToken);
        await processMockAgentCatalogService.EnsureCatalogAsync(cancellationToken);
        await technicalAgentBridge.SynchronizeDirectoryProjectionAsync(cancellationToken);
        stopwatch.Stop();

        logger.LogInformation(
            "Completed AgentFramework catalog repair and projection warmup in {ElapsedMilliseconds} ms.",
            stopwatch.ElapsedMilliseconds);
    }
}

internal sealed class AgentFrameworkCatalogWarmupWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<AgentFrameworkCatalogWarmupWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var warmupService = scope.ServiceProvider.GetRequiredService<AgentFrameworkCatalogWarmupService>();
            await warmupService.WarmupAsync(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "AgentFramework catalog warmup failed during startup.");
        }
    }
}
