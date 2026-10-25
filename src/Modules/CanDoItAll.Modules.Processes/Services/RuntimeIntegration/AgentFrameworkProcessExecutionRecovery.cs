using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.Modules.AgentFramework.Hosting;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Modules.Processes;

public sealed class AgentFrameworkProcessExecutionRecovery(ISandboxWorkspaceProcessRunPageStore pages,
    IProcessExecutionStopper stopper, IProcessDecisionChildReader children, IWorkflowProcessCancellationRecovery workflows,
    IProcessRootCancellationBarrierReader roots, IProcessRuntimeStateStore states) : IProcessExecutionRecovery {
    public async Task<ProcessCancellationExecutionProgress> InspectNextAsync(ProcessRunId rootRunId, ProcessRunId runId,
        ProcessExecutionRunId? after, CancellationToken cancellationToken) {
        ArgumentOutOfRangeException.ThrowIfEqual(rootRunId.Value, Guid.Empty);
        ArgumentOutOfRangeException.ThrowIfEqual(runId.Value, Guid.Empty);
        if (after?.Value == Guid.Empty) {
            throw new ArgumentOutOfRangeException(nameof(after));
        }
        var page = await pages.ReadProcessExecutionRunPageAsync(new(runId.Value, after?.Value, 1), cancellationToken).ConfigureAwait(false);
        if (!Enum.IsDefined(page.Status)) {
            throw new InvalidDataException("Execution selection returned an unsupported index status.");
        }
        if (page.Status != ProcessExecutionRunPageStatus.Ready) {
            if (page.Candidates.Count != 0 || page.LastExecutionRunId is not null || page.Exhausted) {
                throw new InvalidDataException("Unavailable execution selection cannot assert scan progress.");
            }
            return new(after, false, page.Status == ProcessExecutionRunPageStatus.PublicationPending
                ? ProcessCancellationRecoveryPending.CompletionPending : ProcessCancellationRecoveryPending.MissingJournal);
        }
        if (page.Candidates.Count > 1 || page.Candidates.Count == 0 && (!page.Exhausted || page.LastExecutionRunId != after?.Value)) {
            throw new InvalidDataException("Execution recovery requires a single bounded candidate or a verified exhausted page.");
        }
        var workflow = await workflows.FindNextAsync(new(runId.Value), after is { } cursor ? new(cursor.Value) : null,
            cancellationToken).ConfigureAwait(false);
        if (workflow?.Value == Guid.Empty || workflow is { } selected && after is { } previous && selected.Value.CompareTo(previous.Value) <= 0) {
            throw new InvalidDataException("Workflow recovery selection lost its forward cursor.");
        }
        if (page.Candidates.Count == 0 && workflow is null) {
            return new(after, true);
        }
        var run = page.Candidates.SingleOrDefault();
        if (run is not null && (run.Id == Guid.Empty || page.LastExecutionRunId != run.Id || after is { } agentAfter && run.Id.CompareTo(agentAfter.Value) <= 0)) {
            throw new InvalidDataException("Execution selection lost its original identity or forward cursor.");
        }
        ProcessExecutionRunId next = new(run is not null && (workflow is null || run.Id.CompareTo(workflow.Value.Value) <= 0)
            ? run.Id : workflow!.Value.Value);
        var pending = run?.Id == next.Value
            ? await StopAgentAsync(rootRunId, runId, run, cancellationToken).ConfigureAwait(false)
            : ProcessCancellationRecoveryPending.None;
        if (workflow?.Value == next.Value) {
            if (!await roots.IsStoppedAsync(rootRunId, cancellationToken).ConfigureAwait(false) ||
                    (await states.LoadAsync(runId, cancellationToken).ConfigureAwait(false))?.RootRunId != rootRunId) {
                throw new InvalidDataException("Workflow recovery requires the original selected run and its durable stopped root.");
            }
            var stopped = await workflows.StopAsync(new(runId.Value), workflow.Value, cancellationToken).ConfigureAwait(false);
            pending = stopped switch {
                WorkflowProcessCancellationStatus.Closed => pending,
                WorkflowProcessCancellationStatus.Pending => pending == ProcessCancellationRecoveryPending.None ? ProcessCancellationRecoveryPending.ActiveWork : pending,
                WorkflowProcessCancellationStatus.ReconciliationRequired => ProcessCancellationRecoveryPending.InvalidEvidence,
                _ => throw new InvalidDataException("Workflow recovery returned an unsupported owner outcome.")
            };
        }
        return new(next, false, pending);
    }

    private async Task<ProcessCancellationRecoveryPending> StopAgentAsync(ProcessRunId rootRunId, ProcessRunId runId,
        ExecutionRunRecord run, CancellationToken cancellationToken) {
        if (!Guid.TryParse(run.ProcessRunId, out var process) || process == Guid.Empty) {
            return run.SourceKind == ProcessMockAgentCatalog.ProcessSourceKind
                ? ProcessCancellationRecoveryPending.InvalidEvidence : ProcessCancellationRecoveryPending.None;
        }
        if (process != runId.Value) {
            return ProcessCancellationRecoveryPending.None;
        }
        if (run.ToolAdmission?.PlanningDecision?.InheritedOrigin is { } origin) {
            run.ToolAdmission.Validate();
            var envelope = origin.ParentBinding;
            if (envelope.Format != ProcessDecisionChildMaterialBindingCodec.Format || envelope.Version != ProcessDecisionChildMaterialBindingCodec.Version) {
                throw new InvalidDataException("Inherited execution recovery requires its original Process child binding.");
            }
            var binding = ProcessDecisionChildMaterialBindingCodec.Deserialize(envelope.PayloadJson);
            var child = binding.Child;
            if (child.Address.RootRunId != rootRunId || child.Address.RunId != runId || child.Address.ChildId.Value != run.Id ||
                    (await children.ReadAsync(child.Address, cancellationToken).ConfigureAwait(false))?.Admission != child) {
                throw new InvalidDataException("Inherited execution recovery cannot delegate a child absent from its original Process owner.");
            }
            return ProcessCancellationRecoveryPending.None;
        }
        var pending = await stopper.StopAsync(rootRunId, runId, new(run.Id), cancellationToken).ConfigureAwait(false);
        if (!Enum.IsDefined(pending)) {
            throw new InvalidDataException("The original Agent stop owner returned an unsupported pending reason.");
        }
        return pending;
    }
}
