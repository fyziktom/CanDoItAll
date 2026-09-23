using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public sealed class WorkflowNodeInvocationBinding {
    internal WorkflowNodeInvocationBinding(WorkflowDefinition definition, WorkflowNode node, WorkflowNodeInput input,
        WorkflowExecutionOccurrence? occurrence, WorkflowCompilerContractVersion compilerVersion, WorkflowLaunchOrigin? origin) {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(input);
        WorkflowProviderDisclosureProtocol.RequireSupported(compilerVersion);
        if (!definition.Graph.Nodes.Any(candidate => candidate.Id == node.Id && candidate.Kind == node.Kind &&
                WorkflowProviderDisclosureContent.Settings(candidate) == WorkflowProviderDisclosureContent.Settings(node))) {
            throw new InvalidOperationException("The actual Workflow invocation node is not in its admitted definition.");
        }
        WorkflowId = definition.Id;
        VersionId = definition.VersionId;
        NodeId = node.Id;
        ExecutorId = node.Settings.ExecutorId;
        Occurrence = occurrence;
        CompilerVersion = compilerVersion;
        DefinitionHash = WorkflowProviderDisclosureContent.Definition(definition);
        SettingsHash = WorkflowProviderDisclosureContent.Settings(node);
        InputHash = WorkflowExecutionContentHash.Compute(input.PayloadJson);
        SourceHash = WorkflowProviderDisclosureContent.Source(origin);
    }

    public WorkflowId WorkflowId { get; }
    public WorkflowVersionId VersionId { get; }
    public WorkflowNodeId NodeId { get; }
    public WorkflowExecutorId? ExecutorId { get; }
    public WorkflowExecutionOccurrence? Occurrence { get; }
    public WorkflowCompilerContractVersion CompilerVersion { get; }
    public WorkflowExecutionContentHash DefinitionHash { get; }
    public WorkflowExecutionContentHash SettingsHash { get; }
    public WorkflowExecutionContentHash InputHash { get; }
    public WorkflowExecutionContentHash SourceHash { get; }

    public WorkflowNodeCompletionProof Complete(Guid completionId, string result) {
        if (Occurrence is null) {
            throw new InvalidOperationException("The Workflow completion has no original execution occurrence.");
        }
        return new(completionId, Occurrence, WorkflowId, VersionId, NodeId, ExecutorId, DefinitionHash, SettingsHash,
            InputHash, WorkflowExecutionContentHash.Compute(result), SourceHash, CompilerVersion);
    }
}
