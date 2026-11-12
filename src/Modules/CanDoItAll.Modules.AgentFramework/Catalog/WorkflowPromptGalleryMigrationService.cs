using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.Editing;
using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.AgentFramework;

public sealed class WorkflowPromptGalleryMigrationService(
    IWorkflowComponentLibraryService componentLibrary,
    IDbContextFactory<WorkflowDbContext> dbContextFactory,
    WorkflowChanges changes,
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

        var definitionResult = await BackfillDefinitionLlmNodeSettingsAsync(cancellationToken);
        if (definitionResult.ProcessedCount > 0)
        {
            logger.LogInformation(
                "Inspected {ProcessedWorkflowDefinitionCount} retained workflow versions against LLM node snapshot schema {InstructionSnapshotSchemaVersion}; appended {AppendedWorkflowSnapshotCount} materialized heads without rewriting exact versions.",
                definitionResult.ProcessedCount,
                WorkflowPersistenceSchemaVersions.InstructionSnapshot,
                definitionResult.AppendedCount);
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
                        ExpectedRevision = component.Revision,
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
            .Select(record => new { record.Id, record.Revision, record.ComponentJson })
            .Take(MigrationBatchSize)
            .ToArrayAsync(cancellationToken);

        return records
            .Select(record => DeserializeLegacyComponent(record.Id, record.ComponentJson) with { Revision = record.Revision })
            .ToArray();
    }

    private async Task<DefinitionMigrationResult> BackfillDefinitionLlmNodeSettingsAsync(
        CancellationToken cancellationToken) {
        var processedCount = 0;
        var appendedCount = 0;
        while (true) {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            var definitionRecords = await dbContext.Set<WorkflowDefinitionRecord>()
                .Where(record =>
                    record.InstructionSnapshotSchemaVersion < WorkflowPersistenceSchemaVersions.InstructionSnapshot)
                .OrderBy(record => record.VersionId)
                .Take(MigrationBatchSize)
                .ToArrayAsync(cancellationToken);
            if (definitionRecords.Length == 0) {
                return new DefinitionMigrationResult(processedCount, appendedCount);
            }
            var workflowIds = definitionRecords.Select(record => record.WorkflowId).Distinct().ToArray();
            var heads = await dbContext.Set<WorkflowDefinitionHeadRecord>()
                .Where(head => workflowIds.Contains(head.WorkflowId))
                .ToDictionaryAsync(head => head.WorkflowId, cancellationToken);
            var definitions = definitionRecords
                .Select(CreateDefinitionMigrationItem)
                .ToArray();
            var requiredComponentIds = definitions
                .SelectMany(item => item.Definition.Graph.Nodes)
                .Where(node =>
                    node.Kind == WorkflowNodeKind.LlmCall &&
                    (WorkflowInstructionSnapshotPolicy.RequiresComponentBackfill(node.Settings.Instructions) ||
                     !node.Settings.ProviderProfileId.HasValue ||
                     string.IsNullOrWhiteSpace(node.Settings.Model)) &&
                    node.Settings.ComponentId.HasValue)
                .Select(node => node.Settings.ComponentId!.Value.Value)
                .Distinct()
                .ToArray();
            var components = await LoadComponentsAsync(requiredComponentIds, cancellationToken);
            var appended = new HashSet<WorkflowId>();
            foreach (var item in definitions) {
                cancellationToken.ThrowIfCancellationRequested();
                var changed = false;
                var nodes = new List<WorkflowNode>(item.Definition.Graph.Nodes.Count);
                foreach (var node in item.Definition.Graph.Nodes) {
                    var result = BackfillNode(node, components);
                    nodes.Add(result.Node);
                    changed |= result.Changed;
                    if (result.FailureReason is not null) {
                        logger.LogWarning(
                            "Could not backfill workflow {WorkflowId} version {WorkflowVersionId} LLM node {WorkflowNodeId} from component {WorkflowComponentId}: {BackfillFailure}. The exact legacy version is retained; its inspection marker advances to schema {InstructionSnapshotSchemaVersion}.",
                            item.Definition.Id.Value,
                            item.Definition.VersionId.Value,
                            node.Id.Value,
                            node.Settings.ComponentId?.Value,
                            result.FailureReason,
                            WorkflowPersistenceSchemaVersions.InstructionSnapshot);
                    }
                }

                if (changed && heads.TryGetValue(item.Record.WorkflowId, out var head) && head.VersionId == item.Record.VersionId) {
                    var repaired = item.Definition with {
                        VersionId = WorkflowVersionId.New(),
                        UpdatedAtUtc = DateTimeOffset.UtcNow,
                        Graph = new WorkflowGraph(
                            item.Definition.Graph.StartNodeId,
                            nodes.ToArray(),
                            item.Definition.Graph.Edges.ToArray())
                    };
                    dbContext.Add(WorkflowDefinitionRecord.FromDefinition(repaired, checked(item.Record.Revision + 1)));
                    head.VersionId = repaired.VersionId.Value;
                    appended.Add(repaired.Id);
                    appendedCount++;
                } else if (changed) {
                    logger.LogDebug("Retained exact workflow version {WorkflowId}/{WorkflowVersionId}; it is not the observed current head, so only its schema inspection marker advances.",
                        item.Definition.Id.Value, item.Definition.VersionId.Value);
                }

                item.Record.InstructionSnapshotSchemaVersion = WorkflowPersistenceSchemaVersions.InstructionSnapshot;
            }

            try {
                await dbContext.SaveChangesAsync(cancellationToken);
            } catch (DbUpdateException error) when (error is DbUpdateConcurrencyException ||
                PersistentWorkflowCatalogService.IsWorkflowDefinitionWriteCollision(error)) {
                logger.LogWarning("Workflow schema snapshot batch refused after an original head or schema changed. Retained version count {VersionCount}; failure {FailureType}.",
                    definitionRecords.Length, error.GetType().Name);
                throw new WorkflowDefinitionConcurrencyException("A workflow head or schema changed during legacy snapshot inspection. This batch was not committed; reopen the migration against current observations.", error);
            }
            foreach (var workflowId in appended) {
                changes.Publish(workflowId, EditingChangeKind.EditableContentChanged);
            }
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

    private static NodeBackfillResult BackfillNode(
        WorkflowNode node,
        IReadOnlyDictionary<WorkflowComponentId, LlmCallComponent> components)
    {
        if (node.Kind != WorkflowNodeKind.LlmCall ||
            !WorkflowInstructionSnapshotPolicy.RequiresComponentBackfill(node.Settings.Instructions) &&
            node.Settings.ProviderProfileId.HasValue &&
            !string.IsNullOrWhiteSpace(node.Settings.Model))
        {
            return new NodeBackfillResult(node, Changed: false, FailureReason: null);
        }

        if (node.Settings.ComponentId is not { } componentId)
        {
            return new NodeBackfillResult(
                node,
                Changed: false,
                FailureReason: "the node has no component reference");
        }

        if (!components.TryGetValue(componentId, out var component))
        {
            return new NodeBackfillResult(
                node,
                Changed: false,
                FailureReason: "the referenced component is unavailable");
        }

        if (string.IsNullOrWhiteSpace(component.Instructions))
        {
            return new NodeBackfillResult(
                node,
                Changed: false,
                FailureReason: "the referenced component has no instruction snapshot");
        }

        return new NodeBackfillResult(
            node with
            {
                Settings = node.Settings with
                {
                    ProviderProfileId = node.Settings.ProviderProfileId ?? component.ProviderProfileId,
                    Model = string.IsNullOrWhiteSpace(node.Settings.Model)
                        ? component.Model
                        : node.Settings.Model.Trim(),
                    Instructions = WorkflowInstructionSnapshotPolicy.RequiresComponentBackfill(node.Settings.Instructions)
                        ? component.Instructions
                        : node.Settings.Instructions
                }
            },
            Changed: true,
            FailureReason: null);
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

    private sealed record NodeBackfillResult(
        WorkflowNode Node,
        bool Changed,
        string? FailureReason);

    private sealed record DefinitionMigrationResult(int ProcessedCount, int AppendedCount);
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
