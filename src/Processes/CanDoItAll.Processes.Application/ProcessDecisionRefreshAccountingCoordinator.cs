using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Runtime;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Processes.Application;

public abstract record ProcessDecisionRefreshAccountingResult {
    private protected ProcessDecisionRefreshAccountingResult() { }
    public sealed record Pending(ProcessDecisionChildPendingReason Reason) : ProcessDecisionRefreshAccountingResult;
    public sealed record Reconciled(ProcessRuntimeCommitResult Commit) : ProcessDecisionRefreshAccountingResult;
    public sealed record CompletedOriginal(ProcessRuntimeCommitResult Commit) : ProcessDecisionRefreshAccountingResult;
}

public sealed class ProcessDecisionRefreshAccountingCoordinator(IProcessAcceptedExecutionInputReader inputs,
    IProcessDecisionResultQuery query, IProcessDecisionRefreshReceiptReader admissions,
    IEnumerable<IProcessDecisionChildOutcomeReader> readers, IProcessIdempotencyStore receipts,
    IProcessRuntimeUnitOfWork unitOfWork, ILogger<ProcessDecisionRefreshAccountingCoordinator> logger) {
    private readonly ProcessDecisionChildOutcomes outcomes = new(readers, logger);

    public async Task<ProcessDecisionRefreshAccountingResult> ReconcileAsync(ProcessRuntimeStateSnapshot state,
        RuntimeCommandContext context, RuntimeCommandId admissionId, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(context);
        context.Validate();
        ProcessDecisionRefreshAccounting accounting = new(admissionId);
        var commandId = accounting.CommandId;
        var admission = await admissions.ReadAsync(state.RunId, admissionId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Refresh accounting requires its retained original admission.");
        var intent = ((ProcessDecisionBeginRefresh)admission.Decision.Command).Intent;
        if (intent.Request.CommandId != admissionId) {
            throw new InvalidDataException("Refresh accounting received another admission identity.");
        }
        var address = intent.Request.Child;
        var selection = await query.ReadAsync(state.RunId, address.StepInstanceId,
            new ProcessExecutionRunId(address.ChildId.Value), cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidDataException("Refresh accounting requires its original decision and budget.");
        var input = await inputs.ReadAsync(state.RunId, address.StepInstanceId, cancellationToken).ConfigureAwait(false);
        _ = ProcessDecisionRefreshAccountingPreparation.RequireOriginal(state, input, selection, admission);
        if (await receipts.FindCompletedCommandAsync(commandId, cancellationToken).ConfigureAwait(false) is { } replay) {
            return ProcessDecisionRefreshAccountingReceipt.Read(replay with {
                Outcome = ProcessRuntimeTransitionOutcome.Duplicate, CommittedNow = false
            }, admission, selection.Child!);
        }
        ProcessDecisionRefreshAccountingPreparation.RequirePending(state, selection, admission);
        var outcome = await outcomes.ReadAsync(selection.Child!, evidence: null, cancellationToken).ConfigureAwait(false);
        if (outcome is ProcessDecisionChildResult.Pending pending) {
            return new ProcessDecisionRefreshAccountingResult.Pending(pending.Reason);
        }
        var closed = (ProcessDecisionChildResult.Closed)outcome;
        var request = ProcessDecisionRefreshAccountingPreparation.Create(state, context, admission, selection.Child!, closed);
        var result = await unitOfWork.CommitAsync(request, cancellationToken).ConfigureAwait(false);
        var accounted = ProcessDecisionRefreshAccountingReceipt.Read(result, admission, selection.Child!);
        logger.LogInformation("Process refresh {AdmissionId} accounted for original child {ChildId} in run {RunId}; outcome {Outcome}, committed now: {CommittedNow}.",
            admissionId.Value, address.ChildId.Value, state.RunId.Value, closed.Closure.Outcome, result.CommittedNow);
        return accounted;
    }
}
