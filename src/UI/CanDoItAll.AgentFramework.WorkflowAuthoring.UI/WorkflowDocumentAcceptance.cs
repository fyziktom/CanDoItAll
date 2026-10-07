using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.WorkflowAuthoring.UI;

internal static class WorkflowDocumentAcceptance {
    public static void Merge(WorkflowCanvasDocument document, WorkflowDefinition submitted,
        IReadOnlyDictionary<WorkflowNodeId, WorkflowCanvasNodeDraft> submittedNodes, WorkflowDefinition accepted,
        bool hasLaterEdits) {
        document.DefinitionId = accepted.Id;
        document.VersionId = accepted.VersionId;
        document.Baseline = accepted;
        document.CreatedAtUtc = accepted.CreatedAtUtc;
        document.UpdatedAtUtc = accepted.UpdatedAtUtc;
        if (hasLaterEdits) {
            return;
        }
        document.Name = KeepLater(document.Name, submitted.Name, accepted.Name);
        document.Description = KeepLater(document.Description, submitted.Description, accepted.Description);
        document.Status = KeepLater(document.Status, submitted.Status, accepted.Status);
        document.RuntimePolicy = KeepLater(document.RuntimePolicy, submitted.RuntimePolicy, accepted.RuntimePolicy);
        document.StartNodeId = KeepLater(document.StartNodeId, submitted.Graph.StartNodeId, accepted.Graph.StartNodeId);

        foreach (var node in document.Nodes) {
            if (!submittedNodes.TryGetValue(node.Id, out var occurrence) || !ReferenceEquals(node, occurrence)) {
                continue;
            }
            var before = submitted.Graph.Nodes.Single(item => item.Id == node.Id);
            var saved = accepted.Graph.Nodes.Single(item => item.Id == node.Id);
            node.Baseline = saved;
            node.Kind = KeepLater(node.Kind, before.Kind, saved.Kind);
            node.Name = KeepLater(node.Name, before.Name, saved.Name);
            node.CanvasX = KeepLater(node.CanvasX, before.CanvasX, saved.CanvasX);
            node.CanvasY = KeepLater(node.CanvasY, before.CanvasY, saved.CanvasY);
            node.ComponentId = KeepLater(node.ComponentId, before.Settings.ComponentId, saved.Settings.ComponentId);
            node.ProviderProfileId = KeepLater(node.ProviderProfileId, before.Settings.ProviderProfileId, saved.Settings.ProviderProfileId);
            node.Model = KeepLater(node.Model, before.Settings.Model, saved.Settings.Model);
            node.AgentId = KeepLater(node.AgentId, before.Settings.AgentId, saved.Settings.AgentId);
            node.SubworkflowId = KeepLater(node.SubworkflowId, before.Settings.SubworkflowId, saved.Settings.SubworkflowId);
            node.ExternalRequestKind = KeepLater(node.ExternalRequestKind, before.Settings.ExternalRequestKind, saved.Settings.ExternalRequestKind);
            node.ExecutorId = KeepLater(node.ExecutorId, before.Settings.ExecutorId, saved.Settings.ExecutorId);
            node.ExecutorSettingsJson = KeepLater(node.ExecutorSettingsJson, before.Settings.ExecutorSettingsJson, saved.Settings.ExecutorSettingsJson);
            node.ExecutionPolicy = KeepLater(node.ExecutionPolicy, before.Settings.ExecutionPolicy, saved.Settings.ExecutionPolicy);
            node.Instructions = KeepLater(node.Instructions, before.Settings.Instructions, saved.Settings.Instructions);
            node.InputShape = KeepLater(node.InputShape, before.Settings.InputShape, saved.Settings.InputShape);
            node.ResultShape = KeepLater(node.ResultShape, before.Settings.ResultShape, saved.Settings.ResultShape);
        }
    }

    private static T KeepLater<T>(T current, T submitted, T accepted)
        => EqualityComparer<T>.Default.Equals(current, submitted) ? accepted : current;
}
