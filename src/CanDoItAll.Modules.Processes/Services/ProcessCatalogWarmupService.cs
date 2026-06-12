using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.Processes;

public sealed class ProcessCatalogWarmupService(
    ProcessesService processesService,
    ProcessTemplatePackLoader packLoader,
    ProcessTemplateProjectionService projectionService,
    ILogger<ProcessCatalogWarmupService> logger)
{
    public Task WarmupAsync(CancellationToken cancellationToken = default)
    {
        return WarmupAsync(synchronizeExistingDefinitions: false, cancellationToken);
    }

    public async Task WarmupAsync(
        bool synchronizeExistingDefinitions,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var pack = packLoader.Load();
        var existingGlobalDefinitions = (await processesService.ListDefinitionsAsync(null, cancellationToken))
            .Where(item => item.ProjectId == null)
            .ToList();
        var importedCount = 0;
        var synchronizedCount = 0;
        var publishedCount = 0;

        foreach (var processKey in ProcessCatalogDefaultTemplates.Keys)
        {
            if (!pack.Processes.TryGetValue(processKey, out var template))
            {
                logger.LogWarning("Skipping default process warmup because template '{ProcessKey}' was not found.", processKey);
                continue;
            }

            var definitions = existingGlobalDefinitions
                .Where(item => string.Equals(item.Name, template.DisplayName, StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (definitions.Count == 0)
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

                var importedDefinition = (await processesService.ListDefinitionsAsync(null, cancellationToken))
                    .Single(item => item.Id == importResult.Value);
                existingGlobalDefinitions.Add(importedDefinition);
                definitions.Add(importedDefinition);
                importedCount++;
            }

            foreach (var definition in definitions)
            {
                var synchronized = false;
                if (synchronizeExistingDefinitions)
                {
                    var synchronizeResult = await processesService.SynchronizeImportedDefinitionAsync(
                        definition.Id,
                        projectionService.GetProjectedEnvelope(processKey, projectId: null),
                        cancellationToken);
                    if (synchronizeResult.IsFailure)
                    {
                        logger.LogWarning(
                            "Failed to synchronize default process template '{ProcessKey}' into definition '{DefinitionId}': {Errors}",
                            processKey,
                            definition.Id,
                            string.Join(" | ", synchronizeResult.Errors.Select(item => item.Message)));
                        continue;
                    }

                    synchronized = synchronizeResult.Value;
                    if (synchronized)
                    {
                        synchronizedCount++;
                    }
                }

                if (definition.HasPublishedVersion && !synchronized)
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
        }

        stopwatch.Stop();
        logger.LogInformation(
            "Completed process catalog warmup in {ElapsedMilliseconds} ms. Imported {ImportedCount} definitions, synchronized {SynchronizedCount} definitions, and published {PublishedCount} definitions.",
            stopwatch.ElapsedMilliseconds,
            importedCount,
            synchronizedCount,
            publishedCount);
    }
}

internal static class ProcessCatalogDefaultTemplates
{
    public static IReadOnlyList<string> Keys { get; } =
    [
        "dotnet-solution-setup",
        "dotnet-feature-function-implementation",
        "dotnet-development-slice",
        "dotnet-architecture-design-review",
        "dotnet-runtime-command-writeback",
        "dotnet-ui-screenshot-writeback",
        "blazor-app-delivery",
        "blazor-app-repair-fix",
        "blazor-backend-feature",
        "blazor-frontend-feature",
        "blazor-fullstack-feature",
        "software-delivery",
        "ai-assisted-change-delivery",
        "branching-code-review",
        "architecture-decision-governance",
        "release-readiness-and-deployment"
    ];
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
            await warmupService.WarmupAsync(cancellationToken: stoppingToken);
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
