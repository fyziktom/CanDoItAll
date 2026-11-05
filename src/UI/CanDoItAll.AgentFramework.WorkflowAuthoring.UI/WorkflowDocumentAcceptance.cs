using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.WorkflowAuthoring.UI;

internal static class WorkflowDocumentAcceptance {
    public static void Merge(WorkflowCanvasDocument document, WorkflowCanvasSubmission submission, WorkflowDefinition accepted) {
        var submitted = submission.Definition;
        var acceptedNodes = accepted.Graph.Nodes.ToDictionary(node => node.Id);
        var acceptedEdges = accepted.Graph.Edges.ToDictionary(edge => edge.Id);
        T Root<T>(WorkflowDraftField field, T current, T before, T saved)
            => document.Changes.Reconcile(field, submission.Fields, current, before, saved);
        document.DefinitionId = accepted.Id;
        document.VersionId = accepted.VersionId;
        document.Baseline = accepted;
        document.CreatedAtUtc = accepted.CreatedAtUtc;
        document.UpdatedAtUtc = accepted.UpdatedAtUtc;
        document.Name = Root(WorkflowDraftField.Name, document.Name, submitted.Name, accepted.Name);
        document.Description = Root(WorkflowDraftField.Description, document.Description, submitted.Description, accepted.Description);
        document.Status = Root(WorkflowDraftField.Status, document.Status, submitted.Status, accepted.Status);
        document.RuntimePolicy = Root(WorkflowDraftField.RuntimePolicy, document.RuntimePolicy, submitted.RuntimePolicy, accepted.RuntimePolicy);
        document.StartNodeId = Root(WorkflowDraftField.StartNodeId, document.StartNodeId, submitted.Graph.StartNodeId, accepted.Graph.StartNodeId);

        foreach (var node in document.Nodes) {
            if (!submission.Nodes.TryGetValue(node.Id, out var occurrence) || !ReferenceEquals(node, occurrence.Occurrence)) {
                continue;
            }
            var before = occurrence.Definition;
            var saved = acceptedNodes[node.Id];
            T Field<T>(WorkflowDraftField field, T current, T sent, T value)
                => node.Changes.Reconcile(field, occurrence.Fields, current, sent, value);
            node.Baseline = saved;
            node.Kind = Field(WorkflowDraftField.Kind, node.Kind, before.Kind, saved.Kind);
            node.Name = Field(WorkflowDraftField.Name, node.Name, before.Name, saved.Name);
            node.CanvasX = Field(WorkflowDraftField.CanvasX, node.CanvasX, before.CanvasX, saved.CanvasX);
            node.CanvasY = Field(WorkflowDraftField.CanvasY, node.CanvasY, before.CanvasY, saved.CanvasY);
            node.ComponentId = Field(WorkflowDraftField.ComponentId, node.ComponentId, before.Settings.ComponentId, saved.Settings.ComponentId);
            node.ProviderProfileId = Field(WorkflowDraftField.ProviderProfileId, node.ProviderProfileId, before.Settings.ProviderProfileId, saved.Settings.ProviderProfileId);
            node.Model = Field(WorkflowDraftField.Model, node.Model, before.Settings.Model, saved.Settings.Model);
            node.AgentId = Field(WorkflowDraftField.AgentId, node.AgentId, before.Settings.AgentId, saved.Settings.AgentId);
            node.SubworkflowId = Field(WorkflowDraftField.SubworkflowId, node.SubworkflowId, before.Settings.SubworkflowId, saved.Settings.SubworkflowId);
            node.ExternalRequestKind = Field(WorkflowDraftField.ExternalRequestKind, node.ExternalRequestKind, before.Settings.ExternalRequestKind, saved.Settings.ExternalRequestKind);
            node.ExecutorId = Field(WorkflowDraftField.ExecutorId, node.ExecutorId, before.Settings.ExecutorId, saved.Settings.ExecutorId);
            node.ExecutorSettingsJson = Field(WorkflowDraftField.ExecutorSettings, node.ExecutorSettingsJson, before.Settings.ExecutorSettingsJson, saved.Settings.ExecutorSettingsJson);
            node.ExecutionPolicy = Field(WorkflowDraftField.ExecutionPolicy, node.ExecutionPolicy, before.Settings.ExecutionPolicy, saved.Settings.ExecutionPolicy);
            node.Instructions = Field(WorkflowDraftField.Instructions, node.Instructions, before.Settings.Instructions, saved.Settings.Instructions);
            node.InputShape = Field(WorkflowDraftField.InputShape, node.InputShape, before.Settings.InputShape, saved.Settings.InputShape);
            node.ResultShape = Field(WorkflowDraftField.ResultShape, node.ResultShape, before.Settings.ResultShape, saved.Settings.ResultShape);
        }
        foreach (var edge in document.Edges) {
            if (!submission.Edges.TryGetValue(edge.Id, out var occurrence) || !ReferenceEquals(edge, occurrence.Occurrence)) {
                continue;
            }
            var before = occurrence.Definition;
            var saved = acceptedEdges[edge.Id];
            T Field<T>(WorkflowDraftField field, T current, T sent, T value)
                => edge.Changes.Reconcile(field, occurrence.Fields, current, sent, value);
            edge.Baseline = saved;
            edge.SourceNodeId = Field(WorkflowDraftField.SourceNodeId, edge.SourceNodeId, before.SourceNodeId, saved.SourceNodeId);
            edge.TargetNodeId = Field(WorkflowDraftField.TargetNodeId, edge.TargetNodeId, before.TargetNodeId, saved.TargetNodeId);
            edge.SourcePortId = Field(WorkflowDraftField.SourcePortId, edge.SourcePortId, before.SourcePortId, saved.SourcePortId);
            edge.TargetPortId = Field(WorkflowDraftField.TargetPortId, edge.TargetPortId, before.TargetPortId, saved.TargetPortId);
            edge.Kind = Field(WorkflowDraftField.Kind, edge.Kind, before.Kind, saved.Kind);
            edge.ConditionExpression = Field(WorkflowDraftField.ConditionExpression, edge.ConditionExpression, before.ConditionExpression, saved.ConditionExpression);
            edge.Routing = Field(WorkflowDraftField.Routing, edge.Routing, before.Routing, saved.Routing);
        }
    }
}
