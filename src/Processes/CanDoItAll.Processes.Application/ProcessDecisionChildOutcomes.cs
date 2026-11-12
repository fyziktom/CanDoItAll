using CanDoItAll.Processes.Drivers.Abstractions;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Processes.Application;

public sealed class ProcessDecisionChildOutcomes(IEnumerable<IProcessDecisionChildOutcomeReader> readers, ILogger logger) {
    public async Task<ProcessDecisionChildResult> ReadAsync(ProcessDecisionResultChild selected,
        ProcessExecutionOwnerEvidence? evidence, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(selected);
        var original = selected.Original;
        original.Validate();
        var reader = readers.SingleOrDefault(item => item.Contract == original.ExecutionContract)
            ?? throw new InvalidOperationException("The original Process execution contract has no registered outcome reader.");
        ProcessDecisionChildResult result;
        try {
            result = evidence is null ? await reader.ReadAsync(original, cancellationToken).ConfigureAwait(false)
                : reader is IProcessDecisionLiveOutcomeReader live ? await live.ReadAsync(selected, evidence, cancellationToken).ConfigureAwait(false)
                : throw new InvalidDataException("The original Process owner does not support live outcome evidence.");
        } catch (IOException failure) {
            logger.LogWarning("Process result reconciliation could not read child {ChildId} in run {RunId}; failure kind {FailureKind}.",
                original.Child.Address.ChildId, original.Child.Address.RunId, failure.GetType().Name);
            return new ProcessDecisionChildResult.Pending(ProcessDecisionChildPendingReason.UnresolvedEffects);
        }
        if (result is ProcessDecisionChildResult.Closed closed) {
            ArgumentNullException.ThrowIfNull(closed.Closure);
            ArgumentNullException.ThrowIfNull(closed.Work);
            closed.Closure.Validate();
            closed.Work.Validate();
            if (!Enum.IsDefined(closed.Status) || closed.Closure.OwnerRevision != closed.Work.OwnerWorkRevision ||
                    selected.Observation.Closure is { } retained && retained != closed.Closure ||
                    selected.Observation.Settlement is { } work && work != closed.Work) {
                throw new InvalidDataException("The original child outcome disagrees with its retained closure or work.");
            }
            return closed;
        }
        return result is ProcessDecisionChildResult.Pending pending && Enum.IsDefined(pending.Reason) ? pending
            : throw new InvalidDataException("The original Process outcome reader returned no supported evidence.");
    }
}
