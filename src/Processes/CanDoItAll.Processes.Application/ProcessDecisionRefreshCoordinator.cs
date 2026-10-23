using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Runtime;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Processes.Application;

public sealed class ProcessDecisionRefreshCoordinator(IProcessAcceptedExecutionInputReader inputs, IProcessDecisionResultQuery query,
    IProcessDecisionRefreshReceiptReader receipts, IProcessRuntimeUnitOfWork unitOfWork, ILogger<ProcessDecisionRefreshCoordinator> logger) {
    public async Task<ProcessDecisionRefreshAdmissionResult> AdmitAsync(ProcessRuntimeStateSnapshot state, RuntimeCommandContext context,
        ProcessDecisionRefreshRequest request, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(request);
        context.Validate();
        request.Validate();
        if (state.RootRunId != request.Child.RootRunId || state.RunId != request.Child.RunId) {
            throw new InvalidDataException("Refresh admission belongs to another original Process run.");
        }
        if (await receipts.ReadAsync(state.RunId, request.CommandId, cancellationToken).ConfigureAwait(false) is { } replay) {
            replay.RequireOriginal(request, context.Actor);
            return new(replay, false);
        }
        var selection = await query.ReadAsync(state.RunId, request.Child.StepInstanceId,
            new ProcessExecutionRunId(request.Child.ChildId.Value), cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Refresh admission requires its original decision child and budget.");
        var input = await inputs.ReadAsync(state.RunId, request.Child.StepInstanceId, cancellationToken).ConfigureAwait(false);
        var command = ProcessDecisionRefreshPreparation.Create(state, input, selection, request, context.Actor);
        var ticks = Math.Max(context.OccurredAtUtc.Ticks / 10 * 10, checked(state.UpdatedAtUtc.Ticks + 10));
        var mutation = ProcessRuntimeMutation.Applied(state with { UpdatedAtUtc = new(ticks, TimeSpan.Zero) }, [], []);
        var result = await unitOfWork.CommitAsync(new(request.CommandId, state, mutation) {
            Decision = command, DecisionRunScope = ProcessDecisionRefreshReceipt.ScopeCommand(request)
        }, cancellationToken).ConfigureAwait(false);
        if (!result.Succeeded || result.Decision is null) {
            throw new InvalidOperationException("The original Process refused refresh admission; no replacement work was authorized.");
        }
        ProcessDecisionRefreshReceipt receipt = new(result.Decision, result.DecisionRunScope);
        receipt.RequireOriginal(request, context.Actor);
        logger.LogInformation("Process refresh {RefreshActionId} admitted for run {RunId}, step {StepId}, generation {Generation}; committed now: {CommittedNow}.",
            request.ActionId.Value, state.RunId.Value, request.Child.StepInstanceId.Value, request.Child.Reference.Generation, result.CommittedNow);
        return new(receipt, result.CommittedNow);
    }
}
