using System.Text.Json;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Processes.Application;

namespace CanDoItAll.Modules.Workbench;

public sealed class ProjectProcessToolLaunchAdmissionPolicy(
    IProcessExecutionDispatchAuthorityReader executions,
    IProcessExecutionMutationGuard processGuard,
    ProjectProcessLaunchAuthorityService authorities) : IProcessToolLaunchAdmissionPolicy {
    public async Task<IProcessLaunchAuthorityLease> AcquireAsync(ProcessPreparedLaunch preparation,
        CancellationToken cancellationToken = default) {
        var source = preparation.ToolSource ?? throw Denied("The Process tool preparation has no original producer binding.");
        source.RequirePreparation(preparation);
        var found = await executions.ReadAsync(source.Execution.Evidence.ExecutionRunId, cancellationToken);
        if (found.Snapshot is not { ObservedCurrentDispatch: true } current ||
                current.OwnerFingerprint != source.Execution.OwnerFingerprint || current.Evidence != source.Execution.Evidence ||
                current.RootRunId != source.Execution.RootRunId || current.ProjectReference != source.Execution.ProjectReference ||
                current.ProjectId != source.Execution.ProjectId ||
                JsonSerializer.Serialize(current.SourceAuthority) != JsonSerializer.Serialize(preparation.Authority) ||
                !current.AllowedOperations.Contains(ProcessOperationContractNames.ExecuteExternalAction, StringComparer.OrdinalIgnoreCase)) {
            throw Denied("The original Process claim, assignment or source no longer permits this tool launch.");
        }
        var held = await authorities.AcquireAsync(preparation.Authority!, preparation.Authority!, cancellationToken);
        return new ToolAdmissionLease(current, preparation.LinkTarget, processGuard, held);
    }

    private static ProcessExecutionAuthorityMismatchException Denied(string message) => new(message);

    private sealed class ToolAdmissionLease(ProcessExecutionDispatchAuthority expected, ProcessLaunchLinkTarget? expectedTarget,
        IProcessExecutionMutationGuard guard, IProcessLaunchAuthorityLease source) : IProcessLaunchAuthorityLease {
        private int disposed;

        public async Task RequireForMutationAsync(ProcessLaunchLinkTarget? target, CancellationToken cancellationToken = default) {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, this);
            if (target != expectedTarget) {
                throw Denied("The Process tool launch cannot change its original native target.");
            }
            await guard.RequireForMutationAsync(expected, cancellationToken);
            await source.RequireForMutationAsync(target, cancellationToken);
        }

        public async ValueTask DisposeAsync() {
            if (Interlocked.Exchange(ref disposed, 1) == 0) {
                await source.DisposeAsync();
            }
        }
    }
}
