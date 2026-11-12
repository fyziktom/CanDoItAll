using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public enum AgentParentStopOutcome {
    Stopped, AlreadyClosed, Signalled, LeaseBusy, MissingJournal, OpenWork, UnresolvedEffects, CompletionPending
}

internal sealed class AgentParentStopProtocol(ISandboxWorkspaceExecutionRunStore reader,
    ISandboxWorkspaceExecutionRunMutationStore writer, ISandboxWorkspaceExecutionRunLeaseStore leases,
    IAgentExecutionCancellationRegistry cancellation, TimeProvider clock) {
    internal async Task<AgentParentStopOutcome> StopAsync(Guid executionRunId,
        Action<ExecutionRunRecord> requireOriginal, Func<ExecutionRunRecord, CancellationToken, Task> requireCurrent,
        Func<ExecutionRunRecord, AgentParentStopOutcome?> observe, Func<ExecutionRunRecord, DateTimeOffset> lastObserved,
        CancellationToken cancellationToken) {
        if (executionRunId == Guid.Empty) {
            throw new ArgumentException("Parent stopping requires the original execution identity.", nameof(executionRunId));
        }
        cancellationToken.ThrowIfCancellationRequested();
        if (await reader.GetExecutionRunDetailAsync(executionRunId, cancellationToken).ConfigureAwait(false) is not { Run.ToolAdmission: not null } original) {
            return AgentParentStopOutcome.MissingJournal;
        }
        await requireCurrent(original.Run, cancellationToken).ConfigureAwait(false);
        if (cancellation.RequestCancellation(original.Run.ToolAdmission.Session)) {
            return AgentParentStopOutcome.Signalled;
        }
        using var acquisition = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        acquisition.CancelAfter(TimeSpan.FromMilliseconds(250));
        IAsyncDisposable handle;
        try {
            handle = await leases.AcquireToolDispatchLeaseAsync(executionRunId, acquisition.Token).ConfigureAwait(false);
        } catch (OperationCanceledException) when (acquisition.IsCancellationRequested && !cancellationToken.IsCancellationRequested) {
            return AgentParentStopOutcome.LeaseBusy;
        }
        await using var held = handle;
        var observed = (await reader.GetExecutionRunDetailAsync(executionRunId, cancellationToken).ConfigureAwait(false))?.Run
            ?? throw new InvalidDataException("The original execution disappeared while acquiring its stop lease.");
        await requireCurrent(observed, cancellationToken).ConfigureAwait(false);
        if (observe(observed) is { } pending) {
            return pending;
        }
        var outcome = AgentParentStopOutcome.Stopped;
        await writer.UpdateExecutionRunDetailAsync(executionRunId, current => {
            requireOriginal(current.Run);
            observed = current.Run;
            if (observe(current.Run) is { } changed) {
                outcome = changed;
                return current;
            }
            var now = clock.GetUtcNow();
            if (now < current.Run.UpdatedAtUtc || now < lastObserved(current.Run)) {
                throw new InvalidDataException("Parent cancellation cannot precede the original execution's observed work.");
            }
            return AgentSavedEffectCancellation.ApplyParentStop(current, now);
        }, token => requireCurrent(observed, token), cancellationToken).ConfigureAwait(false);
        return outcome;
    }
}
