using System.Buffers.Binary;
using System.Security.Cryptography;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.AgentFramework.ProviderManagement;
using CanDoItAll.RemoteInstances;

namespace CanDoItAll.Modules.AgentFramework;

public sealed class RemoteWorkflowCatalog(PersistentWorkflowCatalogService workflows,
    RemoteTemplatePublicationStore publications, SharedProviderServiceIdentityStore identities, RemoteTemplateProviderCatalog providers,
    IAgentFrameworkWorkspaceService workspace) {
    public async Task<IReadOnlyList<RemoteTemplateRow>> ListAsync(CancellationToken token) {
        var opening = await workspace.ReadAgentDecisionDataPolicyContextAsync(token);
        var published = await publications.ListLifetimesAsync(RemoteTemplateKind.Workflow, token);
        var publisher = await identities.GetOrCreateAsync(token);
        var definitions = await workflows.ReadRemoteDefinitionsAsync(published.Select(item => item.DefinitionId).Distinct().ToArray(), token);
        var eligible = definitions.Where(definition => CanShare(definition.Status) && published.Contains((definition.Id.Value, definition.CreatedAtUtc.UtcTicks))).ToArray();
        var components = await workflows.ReadRemoteComponentsAsync(eligible, token);
        var current = await workflows.ReadShareableWorkflowHeadsAsync(eligible.Select(definition => definition.Id.Value).ToArray(), token);
        var stillPublished = await publications.ListLifetimesAsync(RemoteTemplateKind.Workflow, token);
        await RequireWorkspaceAsync(opening, token);
        return eligible.Where(definition => current.GetValueOrDefault(definition.Id.Value) == definition.VersionId.Value &&
            stillPublished.Contains((definition.Id.Value, definition.CreatedAtUtc.UtcTicks)))
            .Select(definition => Row(publisher.Value, definition, components)).ToArray();
    }

    public async Task<RemoteWorkflowDocument?> FindAsync(Guid id, CancellationToken token) {
        var opening = await workspace.ReadAgentDecisionDataPolicyContextAsync(token);
        var definition = (await workflows.ReadRemoteDefinitionsAsync([id], token)).SingleOrDefault();
        if (definition is null || !CanShare(definition.Status) ||
            !await publications.IsPublishedAsync(RemoteTemplateKind.Workflow, id, token, definition.CreatedAtUtc)) {
            return null;
        }
        var result = await CreateAsync(definition, token);
        var current = await workflows.ReadShareableWorkflowHeadsAsync([id], token);
        if (current.GetValueOrDefault(id) != definition.VersionId.Value || !await publications.IsPublishedAsync(RemoteTemplateKind.Workflow, id, token, definition.CreatedAtUtc)) {
            return null;
        }
        await RequireWorkspaceAsync(opening, token);
        return result;
    }

    public async Task<bool> IsPublishedAsync(WorkflowCatalogItem selected, CancellationToken token) {
        var definition = await ReadSelectedAsync(selected, token);
        return await publications.IsPublishedAsync(RemoteTemplateKind.Workflow, selected.Id.Value, token, definition.CreatedAtUtc);
    }

    public async Task SetPublishedAsync(WorkflowCatalogItem selected, bool publish, Func<CancellationToken, ValueTask> authorize, CancellationToken token) {
        await authorize(token);
        var opening = await workspace.ReadAgentDecisionDataPolicyContextAsync(token);
        var definition = await ReadSelectedAsync(selected, token);
        if (publish) {
            if (!CanShare(definition.Status)) {
                throw new RemoteInstanceReadException("Choose an active or draft workflow to share.");
            }
            await CreateAsync(definition, token);
        }
        async ValueTask Recheck(CancellationToken current) {
            await RequireWorkspaceAsync(opening, current);
            await ReadSelectedAsync(selected, current);
            await authorize(current);
        }
        await publications.SetAsync(RemoteTemplateKind.Workflow, selected.Id.Value, publish, token, Recheck, definition.CreatedAtUtc);
    }

    private async Task<WorkflowDefinition> ReadSelectedAsync(WorkflowCatalogItem selected, CancellationToken token) {
        var definition = (await workflows.ReadRemoteDefinitionsAsync([selected.Id.Value], token)).SingleOrDefault();
        return definition?.VersionId == selected.VersionId ? definition : throw new RemoteInstanceReadException("The selected workflow changed. Refresh it and reopen sharing.");
    }

    private async Task<RemoteWorkflowDocument> CreateAsync(WorkflowDefinition definition, CancellationToken token) {
        var components = await workflows.ReadRemoteComponentsAsync([definition], token);
        var publisher = await identities.GetOrCreateAsync(token);
        var row = Row(publisher.Value, definition, components);
        return await RemoteWorkflowProjection.ExportAsync(row, definition, components, providers.DescribeAsync, token);
    }

    private static RemoteTemplateRow Row(Guid publisher, WorkflowDefinition definition, IReadOnlyDictionary<WorkflowComponentId, LlmCallComponent> components) {
        using var stamp = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        stamp.AppendData(definition.VersionId.Value.ToByteArray());
        foreach (var id in definition.Graph.Nodes.Select(node => node.Settings.ComponentId).OfType<WorkflowComponentId>().Distinct().OrderBy(id => id.Value)) {
            if (!components.TryGetValue(id, out var component)) {
                continue;
            }
            stamp.AppendData(component.Id.Value.ToByteArray());
            stamp.AppendData(component.Revision.ToByteArray());
            stamp.AppendData(component.PromptVersionId.GetValueOrDefault().ToByteArray());
        }
        var revision = BinaryPrimitives.ReadInt64BigEndian(stamp.GetHashAndReset()) & long.MaxValue;
        return new(definition.Id.Value, publisher, definition.Name, definition.Description, null, definition.UpdatedAtUtc,
            revision, definition.VersionId.Value);
    }

    private async Task RequireWorkspaceAsync(AgentExecutionActivityWorkspaceIdentity original, CancellationToken token) {
        if (await workspace.ReadAgentDecisionDataPolicyContextAsync(token) != original) {
            throw new RemoteInstanceReadException("The publishing workspace changed. Refresh the workflow catalog.");
        }
    }

    internal static bool CanShare(WorkflowLifecycleStatus status) => status is WorkflowLifecycleStatus.Active or WorkflowLifecycleStatus.Draft;
}
