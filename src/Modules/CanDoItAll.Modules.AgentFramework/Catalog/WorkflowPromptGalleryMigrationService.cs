using System.Text.Json;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.AgentFramework;

public sealed class WorkflowPromptGalleryMigrationService(
    IWorkflowComponentLibraryService componentLibrary,
    IDbContextFactory<AppDbContext> dbContextFactory,
    ILogger<WorkflowPromptGalleryMigrationService> logger)
{
    private const int MigrationBatchSize = 100;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task EnsureMigratedAsync(CancellationToken cancellationToken = default)
    {
        var migratedCount = await MigrateComponentsAsync(cancellationToken);

        if (migratedCount > 0)
        {
            logger.LogInformation(
                "Migrated {MigratedWorkflowComponentCount} legacy workflow prompt components to the canonical Prompt Gallery.",
                migratedCount);
        }

        var definitionResult = await BackfillDefinitionInstructionSnapshotsAsync(cancellationToken);
        if (definitionResult.ProcessedCount > 0)
        {
            logger.LogInformation(
                "Verified {ProcessedWorkflowDefinitionCount} workflow definitions against instruction snapshot schema {InstructionSnapshotSchemaVersion}; repaired {RepairedWorkflowDefinitionCount} definitions.",
                definitionResult.ProcessedCount,
                WorkflowPersistenceSchemaVersions.InstructionSnapshot,
                definitionResult.RepairedCount);
        }
    }

    private async Task<int> MigrateComponentsAsync(CancellationToken cancellationToken)
    {
        var migratedCount = 0;
        while (true)
        {
            var components = await LoadOutdatedComponentsAsync(cancellationToken);
            if (components.Count == 0)
            {
                return migratedCount;
            }

            foreach (var component in components)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var migrated = await componentLibrary.SaveComponentAsync(
                    new LlmCallComponentSaveRequest(
                        component.Id,
                        component.Name,
                        component.ProviderProfileId,
                        component.Model,
                        component.Modality,
                        component.ModelSettings,
                        component.Instructions,
                        component.InputShape,
                        component.ResultShape,
                        component.Permissions)
                    {
                        PromptArtifactId = component.PromptArtifactId,
                        PromptVersionId = component.PromptVersionId
                    },
                    cancellationToken);
                migratedCount++;

                logger.LogDebug(
                    "Migrated workflow component {WorkflowComponentId} to Prompt Gallery item {PromptArtifactId} version {PromptVersionId} with {ContentLength} characters.",
                    migrated.Id.Value,
                    migrated.PromptArtifactId,
                    migrated.PromptVersionId,
                    migrated.Instructions.Length);
            }
        }
    }

    private async Task<IReadOnlyList<LlmCallComponent>> LoadOutdatedComponentsAsync(
        CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var records = await dbContext.Set<WorkflowComponentRecord>()
            .AsNoTracking()
            .Where(record =>
                record.PromptGalleryBindingSchemaVersion < WorkflowPersistenceSchemaVersions.PromptGalleryBinding)
            .OrderBy(record => record.Id)
            .Select(record => new { record.Id, record.ComponentJson })
            .Take(MigrationBatchSize)
            .ToArrayAsync(cancellationToken);

        return records
            .Select(record => DeserializeLegacyComponent(record.Id, record.ComponentJson))
            .ToArray();
    }

    private async Task<DefinitionMigrationResult> BackfillDefinitionInstructionSnapshotsAsync(
        CancellationToken cancellationToken)
    {
        var processedCount = 0;
        var repairedCount = 0;
        while (true)
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            var definitionRecords = await dbContext.Set<WorkflowDefinitionRecord>()
                .Where(record =>
                    record.InstructionSnapshotSchemaVersion < WorkflowPersistenceSchemaVersions.InstructionSnapshot)
                .OrderBy(record => record.VersionId)
                .Take(MigrationBatchSize)
                .ToArrayAsync(cancellationToken);
            if (definitionRecords.Length == 0)
            {
                return new DefinitionMigrationResult(processedCount, repairedCount);
            }

            var definitions = definitionRecords
                .Select(CreateDefinitionMigrationItem)
                .ToArray();
            var requiredComponentIds = definitions
                .SelectMany(item => item.Definition.Graph.Nodes)
                .Where(node =>
                    node.Kind == WorkflowNodeKind.LlmCall &&
                    string.IsNullOrWhiteSpace(node.Settings.Instructions) &&
                    node.Settings.ComponentId.HasValue)
                .Select(node => node.Settings.ComponentId!.Value.Value)
                .Distinct()
                .ToArray();
            var components = await LoadComponentsAsync(requiredComponentIds, cancellationToken);

            foreach (var item in definitions)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var changed = false;
                var nodes = new List<WorkflowNode>(item.Definition.Graph.Nodes.Count);
                foreach (var node in item.Definition.Graph.Nodes)
                {
                    var result = BackfillNode(node, components);
                    nodes.Add(result.Node);
                    changed |= result.Changed;
                }

                if (changed)
                {
                    var repaired = item.Definition with
                    {
                        Graph = new WorkflowGraph(
                            item.Definition.Graph.StartNodeId,
                            nodes.ToArray(),
                            item.Definition.Graph.Edges.ToArray())
                    };
                    item.Record.DefinitionJson = JsonSerializer.Serialize(repaired, JsonOptions);
                    repairedCount++;
                }

                item.Record.InstructionSnapshotSchemaVersion = WorkflowPersistenceSchemaVersions.InstructionSnapshot;
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            processedCount += definitionRecords.Length;
        }
    }

    private async Task<IReadOnlyDictionary<WorkflowComponentId, LlmCallComponent>> LoadComponentsAsync(
        IReadOnlyCollection<Guid> componentIds,
        CancellationToken cancellationToken)
    {
        var components = new Dictionary<WorkflowComponentId, LlmCallComponent>(componentIds.Count);
        foreach (var componentId in componentIds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var typedId = new WorkflowComponentId(componentId);
            var component = await componentLibrary.GetComponentAsync(typedId, cancellationToken);
            if (component is not null)
            {
                components.Add(typedId, component);
            }
        }

        return components;
    }

    private static (WorkflowNode Node, bool Changed) BackfillNode(
        WorkflowNode node,
        IReadOnlyDictionary<WorkflowComponentId, LlmCallComponent> components)
    {
        if (node.Kind != WorkflowNodeKind.LlmCall || !string.IsNullOrWhiteSpace(node.Settings.Instructions))
        {
            return (node, false);
        }

        if (node.Settings.ComponentId is not { } componentId ||
            !components.TryGetValue(componentId, out var component) ||
            string.IsNullOrWhiteSpace(component.Instructions))
        {
            throw new InvalidOperationException(
                $"Workflow definition node '{node.Id}' cannot be backfilled because its LLM component snapshot is unavailable.");
        }

        return (
            node with
            {
                Settings = node.Settings with { Instructions = component.Instructions }
            },
            true);
    }

    private static T Deserialize<T>(string json, string resourceName)
        => JsonSerializer.Deserialize<T>(json, JsonOptions)
            ?? throw new InvalidOperationException($"Stored {resourceName} JSON deserialized to null.");

    private static LlmCallComponent DeserializeLegacyComponent(Guid recordId, string json)
    {
        var component = Deserialize<LlmCallComponent>(json, "workflow component");
        if (component.Id.Value != recordId)
        {
            throw new InvalidOperationException(
                $"Stored workflow component '{recordId:D}' contains mismatched component id '{component.Id.Value:D}'.");
        }

        return component;
    }

    private static DefinitionMigrationItem CreateDefinitionMigrationItem(WorkflowDefinitionRecord record)
    {
        var definition = Deserialize<WorkflowDefinition>(record.DefinitionJson, "workflow definition");
        if (definition.Id.Value != record.WorkflowId || definition.VersionId.Value != record.VersionId)
        {
            throw new InvalidOperationException(
                $"Stored workflow definition '{record.WorkflowId:D}' version '{record.VersionId:D}' contains mismatched identity metadata.");
        }

        return new DefinitionMigrationItem(record, definition);
    }

    private sealed record DefinitionMigrationItem(
        WorkflowDefinitionRecord Record,
        WorkflowDefinition Definition);

    private sealed record DefinitionMigrationResult(int ProcessedCount, int RepairedCount);
}

public sealed class WorkflowPromptGalleryMigrationHostedService(
    IServiceScopeFactory scopeFactory,
    ILogger<WorkflowPromptGalleryMigrationHostedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var migration = scope.ServiceProvider.GetRequiredService<WorkflowPromptGalleryMigrationService>();
        await migration.EnsureMigratedAsync(cancellationToken);
        logger.LogInformation("Verified canonical Prompt Gallery bindings for workflow LLM components.");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
