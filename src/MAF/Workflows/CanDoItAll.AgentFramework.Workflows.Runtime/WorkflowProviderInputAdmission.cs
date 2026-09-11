using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;

namespace CanDoItAll.AgentFramework.Workflows.Runtime;

public sealed class WorkflowProviderInputAdmission(
    IWorkflowRunStore runs,
    IWorkflowExecutorCatalog executors,
    IEnumerable<IWorkflowProviderDisclosurePolicy> policies) : IWorkflowProviderInputAdmission {
    private readonly IReadOnlyDictionary<WorkflowDisclosureOwnerId, IWorkflowProviderDisclosurePolicy> owners =
        policies.ToDictionary(policy => policy.Owner);

    public async ValueTask RequireAsync(WorkflowDefinition definition, WorkflowNode node, WorkflowNodeInput input,
        CancellationToken cancellationToken = default) {
        var actual = WorkflowExecutorExecutionAuditScope.CurrentInvocation
            ?? throw Denied("Provider input has no actual Workflow invocation binding.");
        var runId = WorkflowExecutorExecutionAuditScope.CurrentRunId
            ?? throw Denied("Provider input has no admitted Workflow run.");
        if (node.Kind != WorkflowNodeKind.LlmCall || actual.WorkflowId != definition.Id || actual.VersionId != definition.VersionId || actual.NodeId != node.Id ||
                actual.Occurrence != input.ExecutionOccurrence?.Advance(definition.VersionId, node.Id) ||
                actual.DefinitionHash != WorkflowProviderDisclosureContent.Definition(definition) ||
                actual.SettingsHash != WorkflowProviderDisclosureContent.Settings(node) ||
                actual.InputHash != WorkflowExecutionContentHash.Compute(input.PayloadJson)) {
            throw Denied("Provider input does not match the actual immutable Workflow invocation.");
        }
        var run = await runs.GetRunAsync(runId, cancellationToken)
            ?? throw Denied("Provider input has no retained Workflow run.");
        if (run.WorkflowId != definition.Id || run.VersionId != definition.VersionId ||
                run.State is not (WorkflowRunState.Running or WorkflowRunState.Idle or WorkflowRunState.WaitingForInput) ||
                actual.SourceHash != WorkflowProviderDisclosureContent.Source(run.Origin)) {
            throw Denied("Provider input no longer matches its live original Workflow run and source.");
        }
        var required = ResolveRequiredOwners(definition);
        var history = await runs.ReadProviderDisclosureAsync(runId, cancellationToken);
        if (actual.CompilerVersion == WorkflowProviderDisclosureProtocol.Legacy) {
            if (history.Declaration is not null || required.Count > 0 || history.Completions.Any(read => read.Evidence.Count > 0)) {
                throw Denied("This retained legacy Workflow has no complete protected-read admission. Inspect its history and explicitly start a newly reviewed run; its authority cannot be recreated from display events.");
            }
            return;
        }
        var declaration = history.Declaration
            ?? throw Denied("The Workflow has no retained original disclosure declaration.");
        declaration.Validate();
        if (declaration.RunId != runId || declaration.WorkflowId != definition.Id || declaration.VersionId != definition.VersionId ||
                declaration.DefinitionHash != actual.DefinitionHash || declaration.SourceHash != actual.SourceHash ||
                declaration.CompilerVersion != actual.CompilerVersion || actual.Occurrence?.RunId != runId) {
            throw Denied("The Workflow provider input differs from its original admitted declaration.");
        }
        if (history.UnprovenCompletedNodeIds.Any(required.ContainsKey)) {
            throw Denied("A protected Workflow read is missing its original completion evidence.");
        }
        var nodes = definition.Graph.Nodes.ToDictionary(candidate => candidate.Id);
        foreach (var read in history.Completions) {
            var proof = read.Proof;
            WorkflowProviderDisclosureContent.RequireEvidenceMatches(proof, read.Evidence);
            if (proof.Occurrence.RunId != runId || proof.WorkflowId != definition.Id || proof.VersionId != definition.VersionId ||
                    proof.DefinitionHash != declaration.DefinitionHash || proof.SourceHash != declaration.SourceHash ||
                    proof.CompilerVersion != declaration.CompilerVersion || !nodes.TryGetValue(proof.NodeId, out var completedNode) ||
                    proof.ExecutorId != completedNode.Settings.ExecutorId || proof.SettingsHash != WorkflowProviderDisclosureContent.Settings(completedNode) ||
                    read.Manifest != WorkflowProviderDisclosureContent.Manifest(read.Evidence)) {
                throw Denied("Retained Workflow evidence does not match the original actual node completion.");
            }
            var simulation = declaration.Simulations.SingleOrDefault(item => item.NodeId == proof.NodeId);
            if (simulation?.Hash != proof.SimulationHash) {
                throw Denied("A Workflow completion differs from its original real or simulated execution admission.");
            }
            if (simulation is not null) {
                if (read.Evidence.Count > 0) {
                    throw Denied("A simulated Workflow node cannot attest a real owner read.");
                }
            } else if (required.TryGetValue(proof.NodeId, out var owner)) {
                if (read.Evidence.Count == 0 || read.Evidence.Any(part => part.Owner != owner)) {
                    throw Denied("A protected Workflow read has missing or mismatched owner evidence.");
                }
            } else if (read.Evidence.Count > 0) {
                throw Denied("Workflow read evidence has no matching registered owner for its original operation.");
            }
        }
        foreach (var group in history.Completions.Where(read => read.Evidence.Count > 0)
                .GroupBy(read => required[read.Proof.NodeId])) {
            await owners[group.Key].RequireCurrentAsync(run, definition, group.ToArray(), cancellationToken);
        }
    }

    private Dictionary<WorkflowNodeId, WorkflowDisclosureOwnerId> ResolveRequiredOwners(WorkflowDefinition definition) {
        Dictionary<WorkflowNodeId, WorkflowDisclosureOwnerId> required = [];
        foreach (var node in definition.Graph.Nodes) {
            if (node.Settings.ExecutorId is not { } executorId) {
                continue;
            }
            var descriptor = executors.GetRequiredExecutor(executorId);
            if (descriptor.ProviderReadOwner is not { } ownerId) {
                if (owners.Values.Any(owner => owner.RequiresEvidence(node))) {
                    throw Denied("A protected Workflow executor is missing its registered owner metadata.");
                }
                continue;
            }
            if (!owners.TryGetValue(ownerId, out var owner)) {
                throw Denied("The Workflow executor's disclosure owner is unavailable.");
            }
            if (owner.RequiresEvidence(node)) {
                required.Add(node.Id, ownerId);
            }
        }
        return required;
    }

    private static InvalidOperationException Denied(string message) => new(message);
}
