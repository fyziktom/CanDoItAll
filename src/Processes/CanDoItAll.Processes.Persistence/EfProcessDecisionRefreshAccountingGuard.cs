using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Processes.Persistence;

internal static class EfProcessDecisionRefreshAccountingGuard {
    internal static async Task<ProcessDecisionRefreshIntent?> RequireFreshAsync(ProcessPersistenceDbContext context,
        ICanonicalRuntimeDatabase? database, ProcessRuntimeCommitRequest request, CancellationToken cancellationToken) {
        if (request.RefreshAccounting is not { } accounting) {
            return null;
        }
        var state = request.OriginalState;
        var admission = await EfProcessDecisionRefreshReceiptReader.ReadCoreAsync(context, state.RunId,
            accounting.AdmissionCommandId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Refresh accounting lost its original durable admission.");
        var intent = ((ProcessDecisionBeginRefresh)admission.Decision.Command).Intent;
        var address = intent.Request.Child;
        var selection = await EfProcessDecisionResultQuery.ReadCoreAsync(context, state.RunId, address.StepInstanceId,
            new ProcessExecutionRunId(address.ChildId.Value), cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidDataException("Refresh accounting lost its original persisted decision.");
        var input = await EfProcessAcceptedExecutionInputReader.ReadCoreAsync(context,
            database?.Profile.Profile.Id ?? throw new InvalidOperationException("Refresh accounting requires the canonical database owner."),
            state.RunId, address.StepInstanceId, cancellationToken).ConfigureAwait(false);
        _ = ProcessDecisionRefreshAccountingPreparation.RequireOriginal(state, input, selection, admission);
        ProcessDecisionRefreshAccountingPreparation.RequirePending(state, selection, admission);
        await EfProcessDecisionRefreshAuthorityReader.RequireCurrentAsync(context, selection.Child!.Original, admission,
            cancellationToken).ConfigureAwait(false);
        accounting.RequireMutation(request, intent);
        if (request.Decision is not ProcessDecisionClosure closure || closure.Expectation != admission.Decision.Current.Expectation ||
                request.DecisionBudget is not ProcessDecisionBudgetSettlement settlement ||
                settlement.AllocationId != selection.Child.Original.Child.AllocationId) {
            throw new InvalidOperationException("Refresh accounting changed the admitted generation or original allocation.");
        }
        return intent;
    }
}
