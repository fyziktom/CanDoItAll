using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Contracts;
using CanDoItAll.Processes.Core;
using CanDoItAll.Processes.Drivers.Abstractions;

namespace CanDoItAll.Processes.Runtime;

public sealed partial class ProcessRuntimeEngine(IProcessRuntimeUnitOfWork unitOfWork)
{
    private readonly IProcessRuntimeUnitOfWork unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    private readonly ProcessRuntimeScheduler scheduler = new();

    public Task<ProcessRuntimeCommitResult> ActivateAsync(
        ProcessRuntimeStateSnapshot state,
        RuntimeCommandContext context,
        CancellationToken cancellationToken = default)
    {
        var mutation = Activate(state, context);

        return CommitAsync(state, context.CommandId, mutation, cancellationToken);
    }

    public Task<ProcessRuntimeCommitResult> ScheduleReadyAsync(
        ProcessRuntimeStateSnapshot state,
        RuntimeCommandContext context,
        CancellationToken cancellationToken = default)
    {
        var mutation = ScheduleReady(state, context);

        return CommitAsync(state, context.CommandId, mutation, cancellationToken);
    }

    public Task<ProcessRuntimeCommitResult> CreateClaimAsync(
        ProcessRuntimeStateSnapshot state,
        RuntimeCommandContext context,
        CreateDispatchClaimCommand command,
        CancellationToken cancellationToken = default)
    {
        var mutation = CreateClaim(state, context, command);

        return CommitAsync(state, context.CommandId, mutation, cancellationToken);
    }

    public Task<ProcessRuntimeCommitResult> MarkClaimRunningAsync(
        ProcessRuntimeStateSnapshot state,
        RuntimeCommandContext context,
        ProcessStepInstanceId stepId,
        DispatchClaimToken claimToken,
        CancellationToken cancellationToken = default)
    {
        var mutation = MarkClaimRunning(state, context, stepId, claimToken);

        return CommitAsync(state, context.CommandId, mutation, cancellationToken);
    }

    public Task<ProcessRuntimeCommitResult> RenewClaimAsync(
        ProcessRuntimeStateSnapshot state,
        RuntimeCommandContext context,
        RenewDispatchClaimCommand command,
        CancellationToken cancellationToken = default)
    {
        var mutation = RenewClaim(state, context, command);

        return CommitAsync(state, context.CommandId, mutation, cancellationToken);
    }

    public Task<ProcessRuntimeCommitResult> ExpireClaimsAsync(
        ProcessRuntimeStateSnapshot state,
        RuntimeCommandContext context,
        ExpireDispatchClaimsCommand command,
        CancellationToken cancellationToken = default)
    {
        var mutation = ExpireClaims(state, context, command);

        return CommitAsync(state, context.CommandId, mutation, cancellationToken);
    }

    public Task<ProcessRuntimeCommitResult> ReclaimClaimAsync(
        ProcessRuntimeStateSnapshot state,
        RuntimeCommandContext context,
        ReclaimDispatchClaimCommand command,
        CancellationToken cancellationToken = default)
    {
        var mutation = ReclaimClaim(state, context, command);

        return CommitAsync(state, context.CommandId, mutation, cancellationToken);
    }

    public Task<ProcessRuntimeCommitResult> SubmitStrategyResultAsync(
        ProcessRuntimeStateSnapshot state,
        RuntimeCommandContext context,
        SubmitStrategyResultCommand command,
        CancellationToken cancellationToken = default)
    {
        var mutation = SubmitStrategyResult(state, context, command);

        return CommitAsync(state, context.CommandId, mutation, cancellationToken);
    }

    public Task<ProcessRuntimeCommitResult> RequestCancellationAsync(
        ProcessRuntimeStateSnapshot state,
        RuntimeCommandContext context,
        CancellationToken cancellationToken = default)
    {
        var mutation = RequestCancellation(state, context);

        return CommitAsync(state, context.CommandId, mutation, cancellationToken);
    }
}
