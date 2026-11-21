using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Editing;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Prompts;
using CanDoItAll.RemoteInstances;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.AgentFramework;

public sealed partial class PersistentWorkflowCatalogService {
    internal async Task<Guid> CreateRemoteDraftAsync(WorkflowDefinition definition, IReadOnlyList<LlmCallComponent> components,
        Func<CancellationToken, ValueTask> authorize, CancellationToken token) {
        await authorize(token);
        await changes.DemandCommitAsync(token);
        var workflowId = definition.Id;
        var prepared = new List<(LlmCallComponent Component, PromptVersionPreparation Prompt)>();
        try {
            await using var context = await dbContextFactory.CreateDbContextAsync(token);
            await using var mutation = await SerializableMutationScope.BeginAsync(context, $"remote-workflow-import:{workflowId.Value:D}", token);
            using var participation = transactions.Enter(context);
            if (await ReadImportedHeadAsync(context, definition, token)) {
                return workflowId.Value;
            }
            ThrowIfValidationFailed(await ValidateWithComponentsAsync(definition, components, creatingLocalPrompts: true, token), "Workflow draft import failed validation");
            foreach (var candidate in components) {
                var revision = Guid.NewGuid();
                var resolution = await ResolveProviderAsync(candidate, token);
                var request = new LlmCallComponentSaveRequest(candidate.Id, candidate.Name, candidate.ProviderProfileId, candidate.Model,
                    candidate.Modality, candidate.ModelSettings, candidate.Instructions, candidate.InputShape, candidate.ResultShape, candidate.Permissions);
                var prompt = await CreateWorkflowPromptAsync(request, candidate.Id, revision, resolution.Provider, PromptArtifactProvenance.WorkflowCreated, token);
                var component = candidate with {
                    Revision = revision, LifetimeId = revision, Instructions = prompt.Snapshot.Content,
                    PromptArtifactId = prompt.Snapshot.PromptArtifactId, PromptVersionId = prompt.Snapshot.PromptVersionId, PromptUpdatedAtUtc = prompt.Item.UpdatedAtUtc
                };
                EnsurePromptCompatibility(prompt.Item with { Status = PromptArtifactStatus.Final, CurrentVersionNumber = prompt.Snapshot.VersionNumber },
                    component, PromptGalleryCompatibilityPurpose.Execution, resolution.Provider?.Kind.ToString());
                prepared.Add((component, prompt));
                context.Set<WorkflowComponentRecord>().Add(WorkflowComponentRecord.FromComponent(component));
            }
            var byId = prepared.ToDictionary(item => item.Component.Id.Value, item => item.Component);
            var snapshot = definition with { Graph = definition.Graph with { Nodes = definition.Graph.Nodes.Select(node => SnapshotNode(node, byId)).ToArray() } };
            context.Set<WorkflowDefinitionRecord>().Add(WorkflowDefinitionRecord.FromDefinition(snapshot, revision: 1));
            context.Set<WorkflowDefinitionHeadRecord>().Add(new() { WorkflowId = workflowId.Value, VersionId = snapshot.VersionId.Value });
            await authorize(token);
            await changes.DemandCommitAsync(token);
            await context.SaveChangesAsync(token);
            await authorize(token);
            await changes.DemandCommitAsync(token);
            await mutation.CommitAsync(token);
        } catch (Exception conflict) when (IsComponentWriteConflict(conflict)) {
            await authorize(token);
            await using var reread = await dbContextFactory.CreateDbContextAsync(token);
            if (await ReadImportedHeadAsync(reread, definition, token)) {
                return workflowId.Value;
            }
            throw new RemoteInstanceReadException("A concurrent workflow change interrupted the import. Refresh the catalog and retry.");
        }
        Exception? notificationFailure = null;
        foreach (var item in prepared) {
            try {
                changes.PublishComponent(item.Component.Id, EditingChangeKind.Created);
                await promptMutations.CompleteVersionAsync(item.Prompt, token);
            } catch (Exception error) when (error is not OutOfMemoryException) {
                changes.ReportComponentFollowUpFailure(item.Component.Id, item.Component.Revision, error);
                notificationFailure ??= error;
            }
        }
        try {
            changes.Publish(workflowId, EditingChangeKind.Created);
        } catch (Exception error) when (error is not OutOfMemoryException) {
            notificationFailure ??= error;
        }
        if (notificationFailure is not null) {
            throw new RemoteTemplateImportCommittedException(workflowId.Value, "The workflow and its components were imported. Refresh their local catalogs.", notificationFailure);
        }
        return workflowId.Value;
    }

    private static async Task<bool> ReadImportedHeadAsync(WorkflowDbContext context, WorkflowDefinition definition, CancellationToken token) {
        var record = await (from head in context.Set<WorkflowDefinitionHeadRecord>().AsNoTracking()
                            join version in context.Set<WorkflowDefinitionRecord>().AsNoTracking() on
                                new { head.WorkflowId, head.VersionId } equals new { version.WorkflowId, version.VersionId }
                            where head.WorkflowId == definition.Id.Value
                            select new { version.TemplateKey, version.TemplatePackKey }).SingleOrDefaultAsync(token);
        if (record is null) {
            return false;
        }
        if (record.TemplatePackKey != RemoteWorkflowDraftWriter.PackKey || record.TemplateKey != definition.TemplateKey) {
            throw new RemoteInstanceReadException("A different local workflow occupies this import identity. Review the workflow catalog.");
        }
        return true;
    }
}
