using CanDoItAll.AgentFramework.Models;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.AgentFramework;

public sealed partial class PersistentWorkflowCatalogService {
    internal async Task<IReadOnlyDictionary<Guid, Guid>> ReadShareableWorkflowHeadsAsync(Guid[] ids, CancellationToken token) {
        if (ids.Length == 0) {
            return new Dictionary<Guid, Guid>();
        }
        await using var context = await dbContextFactory.CreateDbContextAsync(token);
        return await LatestDefinitionQuery(context).Where(item => ids.Contains(item.WorkflowId) &&
            (item.Status == WorkflowLifecycleStatus.Draft || item.Status == WorkflowLifecycleStatus.Active))
            .Select(item => new { item.WorkflowId, item.VersionId }).ToDictionaryAsync(item => item.WorkflowId, item => item.VersionId, token);
    }

    internal async Task<WorkflowDefinition[]> ReadRemoteDefinitionsAsync(Guid[] ids, CancellationToken token) {
        if (ids.Length == 0) {
            return [];
        }
        await using var context = await dbContextFactory.CreateDbContextAsync(token);
        var snapshots = await LatestDefinitionQuery(context).Where(item => ids.Contains(item.WorkflowId))
            .OrderBy(item => item.Name).ThenBy(item => item.WorkflowId).Select(item => item.DefinitionJson).ToArrayAsync(token);
        return snapshots.Select(Deserialize<WorkflowDefinition>).ToArray();
    }

    internal async Task<IReadOnlyDictionary<WorkflowComponentId, LlmCallComponent>> ReadRemoteComponentsAsync(IReadOnlyCollection<WorkflowDefinition> definitions, CancellationToken token) {
        var ids = definitions.SelectMany(definition => definition.Graph.Nodes).Select(node => node.Settings.ComponentId)
            .OfType<WorkflowComponentId>().Select(id => id.Value).Distinct().ToArray();
        if (ids.Length == 0) {
            return new Dictionary<WorkflowComponentId, LlmCallComponent>();
        }
        await using var context = await dbContextFactory.CreateDbContextAsync(token);
        var rows = await context.Set<WorkflowComponentRecord>().AsNoTracking().Where(item => ids.Contains(item.Id)).ToArrayAsync(token);
        return (await HydrateComponentsAsync(rows, token)).ToDictionary(item => item.Id);
    }
}
