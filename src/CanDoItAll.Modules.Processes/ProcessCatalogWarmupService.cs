using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.Processes;

internal sealed class ProcessCatalogWarmupService(
    ProcessesService processesService,
    ProcessTemplatePackLoader packLoader,
    ProcessTemplateProjectionService projectionService,
    ILogger<ProcessCatalogWarmupService> logger)
{
    private static readonly string[] DefaultProcessKeys =
    [
        "software-delivery",
        "ai-assisted-change-delivery",
        "branching-code-review",
        "architecture-decision-governance",
        "release-readiness-and-deployment"
    ];

    public async Task WarmupAsync(CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var pack = packLoader.Load();
        var existingGlobalDefinitions = (await processesService.ListDefinitionsAsync(null, cancellationToken))
            .Where(item => item.ProjectId == null)
            .ToList();
        var importedCount = 0;
        var publishedCount = 0;

        foreach (var processKey in DefaultProcessKeys)
        {
            if (!pack.Processes.TryGetValue(processKey, out var template))
            {
                logger.LogWarning("Skipping default process warmup because template '{ProcessKey}' was not found.", processKey);
                continue;
            }

            var definition = existingGlobalDefinitions.FirstOrDefault(item =>
                string.Equals(item.Name, template.DisplayName, StringComparison.OrdinalIgnoreCase));
            if (definition is null)
            {
                var importResult = await processesService.ImportAsync(
                    projectionService.GetProjectedEnvelope(processKey, projectId: null),
                    cancellationToken);
                if (importResult.IsFailure)
                {
                    logger.LogWarning(
                        "Failed to import default process template '{ProcessKey}': {Errors}",
                        processKey,
                        string.Join(" | ", importResult.Errors.Select(item => item.Message)));
                    continue;
                }

                definition = (await processesService.ListDefinitionsAsync(null, cancellationToken))
                    .Single(item => item.Id == importResult.Value);
                existingGlobalDefinitions.Add(definition);
                importedCount++;
            }

            if (definition.HasPublishedVersion)
            {
                continue;
            }

            var publishResult = await processesService.PublishAsync(definition.Id, cancellationToken);
            if (publishResult.IsFailure)
            {
                logger.LogWarning(
                    "Failed to publish warmed process definition '{DefinitionId}' from template '{ProcessKey}': {Errors}",
                    definition.Id,
                    processKey,
                    string.Join(" | ", publishResult.Errors.Select(item => item.Message)));
                continue;
            }

            publishedCount++;
        }

        stopwatch.Stop();
        logger.LogInformation(
            "Completed process catalog warmup in {ElapsedMilliseconds} ms. Imported {ImportedCount} definitions and published {PublishedCount} definitions.",
            stopwatch.ElapsedMilliseconds,
            importedCount,
            publishedCount);
    }
}

internal sealed class ProcessCatalogWarmupWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<ProcessCatalogWarmupWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var warmupService = scope.ServiceProvider.GetRequiredService<ProcessCatalogWarmupService>();
            await warmupService.WarmupAsync(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Process catalog warmup failed during startup.");
        }
    }
}
