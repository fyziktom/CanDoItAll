using CanDoItAll.AgentFramework.Core;
using CanDoItAll.Modules.AgentFramework.Hosting;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;

namespace CanDoItAll.Modules.Processes;

public sealed class AgentFrameworkProcessExecutionRecovery(ISandboxWorkspaceProcessRunPageStore pages,
    IProcessExecutionStopper stopper, IProcessDecisionChildReader children) : IProcessExecutionRecovery {
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
        if (page.Candidates.Count == 0) {
            return new(after, true);
        }
        var run = page.Candidates[0];
        if (run.Id == Guid.Empty || page.LastExecutionRunId != run.Id || after is { } cursor && run.Id.CompareTo(cursor.Value) <= 0) {
            throw new InvalidDataException("Execution selection lost its original identity or forward cursor.");
        }
        ProcessExecutionRunId next = new(run.Id);
        if (!Guid.TryParse(run.ProcessRunId, out var process) || process == Guid.Empty) {
            return new(next, page.Exhausted, run.SourceKind == ProcessMockAgentCatalog.ProcessSourceKind
                ? ProcessCancellationRecoveryPending.InvalidEvidence : ProcessCancellationRecoveryPending.None);
        }
        if (process != runId.Value) {
            return new(next, page.Exhausted);
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
            return new(next, page.Exhausted);
        }
        var pending = await stopper.StopAsync(rootRunId, runId, next, cancellationToken).ConfigureAwait(false);
        if (!Enum.IsDefined(pending)) {
            throw new InvalidDataException("The original Agent stop owner returned an unsupported pending reason.");
        }
        return new(next, page.Exhausted, pending);
    }
}
