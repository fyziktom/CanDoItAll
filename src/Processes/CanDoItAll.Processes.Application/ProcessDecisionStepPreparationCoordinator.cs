using System.Runtime.ExceptionServices;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Core;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Processes.Application;

public sealed class ProcessDecisionStepPreparationCoordinator(IProcessDecisionStepQuery query,
    IProcessDecisionResultQuery outcomes, IProcessDecisionChildReader children,
    IEnumerable<IProcessDecisionMaterialCapturer> capturers, IProcessRuntimeStateStore states,
    IProcessIdempotencyStore receipts, IProcessRuntimeUnitOfWork unitOfWork, TimeProvider clock,
    ILogger<ProcessDecisionStepPreparationCoordinator> logger) {
    private const int MaximumAttempts = 3;

    public async Task<ProcessDecisionStepPreparationResult> PrepareAsync(ProcessRuntimeStateSnapshot state,
        ProcessRuntimeDispatchPrecondition claim, TimeSpan maximumChildWork, CancellationToken cancellationToken = default) {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(maximumChildWork, TimeSpan.Zero);
        for (var retry = 0; retry < MaximumAttempts; retry++) {
            ProcessDecisionStepSelection selected;
            try {
                selected = await query.ReadAsync(state, claim, cancellationToken).ConfigureAwait(false);
            } catch (ProcessDecisionSourceProjectionRequiredException) {
                return new(ProcessDecisionStepPreparationStatus.SourceProjectionRequired, state);
            } catch (ProcessRuntimeDispatchPreconditionException) {
                return new(ProcessDecisionStepPreparationStatus.Stopped, state);
            }
            if (selected.Policy is not { MaterialProtocol: ProcessDecisionMaterialProtocol.SharedContentV1,
                    ContentContract: not null, ExecutionContract: not null }) {
                throw new InvalidDataException("Selected preparation requires its registered shared-content and execution contracts.");
            }
            if (selected.Budget.OverrunDetected || selected.RootBudget.OverrunDetected ||
                    selected.SharedScope is { Phase: not ProcessDecisionRunScopePhase.Active }) {
                return new(ProcessDecisionStepPreparationStatus.ReviewRequired, state);
            }
            try {
                if (selected.Head is not null) {
                    return await EnrollAsync(state, claim, selected, maximumChildWork, cancellationToken).ConfigureAwait(false);
                }
                var identity = new ProcessDecisionStepIdentity(state.RunId, claim.StepInstanceId, selected.Budget.BudgetId, claim.ClaimToken);
                if (await receipts.FindCompletedCommandAsync(identity.Abort, cancellationToken).ConfigureAwait(false) is { } aborted) {
                    RequireCaptureReceipt(aborted, identity, null, null);
                    return new(ProcessDecisionStepPreparationStatus.ReviewRequired, state);
                }
                if (selected.HasUnclosedWork ||
                        await receipts.FindCompletedCommandAsync(identity.Reserve, cancellationToken).ConfigureAwait(false) is not null) {
                    return new(ProcessDecisionStepPreparationStatus.Pending, state);
                }
                var grant = Math.Min(TimeSpan.FromSeconds(10).Ticks, Math.Min(ProcessDecisionStepIdentity.Remaining(selected.Budget),
                    ProcessDecisionStepIdentity.Remaining(selected.RootBudget)));
                if (grant <= 0) {
                    return new(ProcessDecisionStepPreparationStatus.ReviewRequired, state);
                }
                var reserved = await unitOfWork.CommitAsync(Request(state, identity.Reserve,
                    new ProcessDecisionBudgetReservation(new(claim.StepInstanceId, selected.Budget.BudgetId, selected.Budget.Revision),
                        identity.CaptureAllocation, new(grant, 0))) with {
                    DispatchPrecondition = claim with { DecisionContract = selected.Policy.ExecutionContract }
                }, cancellationToken).ConfigureAwait(false);
                RequireReservation(reserved, identity, grant);
                if (!reserved.CommittedNow) {
                    return new(ProcessDecisionStepPreparationStatus.Pending, reserved.State);
                }
                var captured = await CaptureAsync(reserved.State, claim, selected, identity, grant, cancellationToken).ConfigureAwait(false);
                if (captured.Status != ProcessDecisionStepPreparationStatus.Ready) {
                    return captured;
                }
                state = captured.State;
            } catch (Exception failure) when (IsStale(failure)) {
                state = await ReloadAsync(state.RunId, cancellationToken).ConfigureAwait(false);
            } catch (DecisionBudgetException) {
                return new(ProcessDecisionStepPreparationStatus.ReviewRequired, state);
            }
        }
        return new(ProcessDecisionStepPreparationStatus.Pending, state);
    }

    private async Task<ProcessDecisionStepPreparationResult> EnrollAsync(ProcessRuntimeStateSnapshot state,
        ProcessRuntimeDispatchPrecondition claim, ProcessDecisionStepSelection selected, TimeSpan maximumChildWork, CancellationToken cancellationToken) {
        var head = selected.Head!;
        var material = selected.Material ?? throw new InvalidDataException("The original step generation has no material.");
        var now = clock.GetUtcNow();
        if (head.Phase != ProcessDecisionPhase.Active || head.ChildCount > 1 ||
                now < material.CapturedAtUtc || now - material.CapturedAtUtc > selected.Policy.Policy.Budget.MaximumBasisAge ||
                selected.SharedScope is { } shared && shared.Material?.Stamp != material.Stamp) {
            return new(ProcessDecisionStepPreparationStatus.ReviewRequired, state);
        }
        var contract = selected.Policy.ExecutionContract!;
        var childId = ProcessDecisionChildIdentity.Create(head.Reference, contract.Executor, 1);
        ProcessDecisionChildAddress address = new(state.RootRunId, state.RunId, claim.StepInstanceId, head.Reference, childId);
        var allocation = ProcessDecisionStepIdentity.ChildAllocation(childId);
        if (await children.ReadAsync(address, cancellationToken).ConfigureAwait(false) is { } existing) {
            if (existing.Admission.BudgetId != head.BudgetId || existing.Admission.AllocationId != allocation ||
                    existing.Admission.MaterialId != material.Stamp.Reference.MaterialId || existing.Admission.Owner != contract.Executor) {
                throw new InvalidDataException("The deterministic child differs from its original enrollment.");
            }
            return existing.IsCurrent && existing.Closure is null && existing.Settlement is null
                ? new(ProcessDecisionStepPreparationStatus.Ready, state, new(existing.Admission, material.Scope, material.Stamp, contract))
                : new(ProcessDecisionStepPreparationStatus.ReviewRequired, state);
        }
        if (selected.HasUnclosedWork || head.ChildCount != 0) {
            return new(ProcessDecisionStepPreparationStatus.Pending, state);
        }
        var grant = Math.Min(maximumChildWork.Ticks, Math.Min(ProcessDecisionStepIdentity.Remaining(selected.Budget),
            ProcessDecisionStepIdentity.Remaining(selected.RootBudget)));
        if (grant <= 0) {
            return new(ProcessDecisionStepPreparationStatus.ReviewRequired, state);
        }
        var child = new ProcessDecisionChild(head.Reference, childId, contract.Executor, allocation);
        var committed = await unitOfWork.CommitAsync(Request(state, ProcessDecisionStepIdentity.Enrollment(childId),
            new ProcessDecisionBudgetReservation(new(claim.StepInstanceId, head.BudgetId, selected.Budget.Revision), allocation, new(grant, 0)))
            with { DispatchPrecondition = claim with { DecisionContract = contract },
                Decision = new ProcessDecisionEnrollment(claim.StepInstanceId, head.Expectation, child) },
            cancellationToken).ConfigureAwait(false);
        if (!committed.Succeeded || committed.State.RunId != state.RunId || committed.Decision?.Child != child ||
                committed.DecisionBudget is not { Command: ProcessDecisionBudgetReservation } accounting ||
                accounting.Command.AllocationId != allocation || accounting.Change.Allocation.Settlement is not null ||
                accounting.Change.Balance.BudgetId != head.BudgetId) {
            throw new InvalidDataException("The enrollment receipt differs from its original child or work allocation.");
        }
        ProcessDecisionChildAdmission admitted = new(address, head.MaterialId, contract.Executor, head.BudgetId,
            allocation, accounting.Change.Allocation.Grant);
        return new(ProcessDecisionStepPreparationStatus.Ready, committed.State,
            new(admitted, material.Scope, material.Stamp, contract), committed.CommittedNow);
    }

    private async Task<ProcessDecisionStepPreparationResult> CaptureAsync(ProcessRuntimeStateSnapshot state,
        ProcessRuntimeDispatchPrecondition claim, ProcessDecisionStepSelection selected, ProcessDecisionStepIdentity identity,
        long grant, CancellationToken cancellationToken) {
        ProcessDecisionMaterial? captured = null;
        ProcessDecisionMaterialStamp? stamp = null;
        Exception? failure = null;
        using var deadline = new CancellationTokenSource(TimeSpan.FromTicks(grant), clock);
        using var captureCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
        var started = clock.GetTimestamp();
        try {
            if (selected.SharedScope is { Material: { } shared }) {
                var now = clock.GetUtcNow();
                if (now >= shared.CapturedAtUtc && now - shared.CapturedAtUtc <= selected.Policy.Policy.Budget.MaximumBasisAge) {
                    stamp = shared.Stamp;
                }
            } else {
                if (selected.Policy.Policy.ReadBasisMode != ReadBasisMode.StepSnapshot) {
                    throw new InvalidDataException("RunSnapshot preparation requires its original shared root material.");
                }
                var capturer = capturers.SingleOrDefault(item => item.ContentContract == selected.Policy.ContentContract)
                    ?? throw new InvalidOperationException("The original step content contract has no registered capturer.");
                ProcessDecisionMaterialScope scope = new(identity.Scope, selected.Source.DatabaseProfileId,
                    selected.Policy.WorkspaceNamespaceId, state.RootRunId, state.RunId, claim.StepInstanceId);
                ProcessDecisionMaterialReference reference = new(scope.Id, 1, identity.Material);
                var result = await capturer.CaptureAsync(new(selected.Source, scope, selected.Policy.ContentContract!, reference),
                    captureCancellation.Token).ConfigureAwait(false);
                switch (result) {
                    case ProcessDecisionMaterialCaptureResult.Captured value:
                        captured = value.Material ?? throw new InvalidDataException("The step capturer returned no material.");
                        if (captured.Scope != scope || captured.Reference != reference || captured.ContentContract != selected.Policy.ContentContract) {
                            throw new InvalidDataException("The step capturer changed its original scope, identity or content contract.");
                        }
                        captured.RequireFresh(clock.GetUtcNow(), selected.Policy.Policy.Budget.MaximumBasisAge);
                        stamp = captured.Stamp;
                        break;
                    case ProcessDecisionMaterialCaptureResult.Refused refused when Enum.IsDefined(refused.Reason):
                        logger.LogWarning("Process step capture for run {RunId}, step {StepId} was refused: {Reason}.",
                            state.RunId, claim.StepInstanceId, refused.Reason);
                        break;
                    default:
                        throw new InvalidDataException("The step capturer returned an unsupported result.");
                }
            }
        } catch (Exception exception) {
            failure = exception;
            stamp = null;
            logger.LogWarning("Process step capture for run {RunId}, step {StepId} ended with {FailureKind}.",
                state.RunId, claim.StepInstanceId, exception.GetType().Name);
        }
        var elapsed = clock.GetElapsedTime(started);
        if (elapsed < TimeSpan.Zero) {
            throw new InvalidDataException("Step capture returned a negative monotonic work duration.");
        }
        if (elapsed.Ticks > grant || captureCancellation.IsCancellationRequested) {
            stamp = null;
        }
        using var settlement = new CancellationTokenSource(TimeSpan.FromSeconds(5), clock);
        var finished = await FinishCaptureAsync(state.RunId, claim, selected, identity, stamp, captured,
            new(1, new(elapsed.Ticks, 0)), settlement.Token).ConfigureAwait(false);
        if (failure is not null) {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
        cancellationToken.ThrowIfCancellationRequested();
        return finished;
    }

    private async Task<ProcessDecisionStepPreparationResult> FinishCaptureAsync(ProcessRunId runId,
        ProcessRuntimeDispatchPrecondition claim, ProcessDecisionStepSelection original, ProcessDecisionStepIdentity identity,
        ProcessDecisionMaterialStamp? stamp, ProcessDecisionMaterial? material, DecisionBudgetSettlement work, CancellationToken cancellationToken) {
        for (var retry = 0; retry < MaximumAttempts; retry++) {
            foreach (var command in new[] { identity.Complete, identity.Abort }) {
                if (await receipts.FindCompletedCommandAsync(command, cancellationToken).ConfigureAwait(false) is { } replay) {
                    RequireCaptureReceipt(replay, identity, command == identity.Complete ? stamp : null, work);
                    return new(command == identity.Complete ? ProcessDecisionStepPreparationStatus.Ready
                        : ProcessDecisionStepPreparationStatus.ReviewRequired, await ReloadAsync(runId, cancellationToken).ConfigureAwait(false));
                }
            }
            var state = await ReloadAsync(runId, cancellationToken).ConfigureAwait(false);
            ProcessDecisionStepSelection? current = null;
            if (stamp is not null) {
                try {
                    current = await query.ReadAsync(state, claim, cancellationToken).ConfigureAwait(false);
                } catch (Exception failure) when (failure is ProcessRuntimeDispatchPreconditionException or ProcessExecutionAuthorityMismatchException) {
                    logger.LogWarning("Step material attachment for run {RunId}, step {StepId} lost its original authority: {FailureKind}.",
                        runId, claim.StepInstanceId, failure.GetType().Name);
                }
            }
            var account = await outcomes.ReadAsync(runId, claim.StepInstanceId, null, cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidDataException("The original step capture budget disappeared.");
            account.RequireOriginal(state, claim.StepInstanceId, null);
            if (account.Budget.BudgetId != identity.BudgetId) {
                throw new InvalidDataException("Step capture cannot settle a replacement budget.");
            }
            var attach = stamp is not null && current is { Head: null } && !current.Budget.OverrunDetected &&
                ProcessDecisionDataPolicyAdmissionCodec.Serialize(current.Policy) == ProcessDecisionDataPolicyAdmissionCodec.Serialize(original.Policy) &&
                (current.SharedScope is null ||
                    current.SharedScope is { Phase: ProcessDecisionRunScopePhase.Active } shared && shared.Material?.Stamp == stamp);
            var request = Request(state, attach ? identity.Complete : identity.Abort,
                new ProcessDecisionBudgetSettlement(new(claim.StepInstanceId, identity.BudgetId, account.Budget.Revision),
                    identity.CaptureAllocation, work));
            if (attach) {
                request = request with {
                    DispatchPrecondition = claim with { DecisionContract = original.Policy.ExecutionContract },
                    Decision = new ProcessDecisionStart(claim.StepInstanceId, identity.Decision, stamp!.Reference.MaterialId) {
                        Material = stamp, InheritedScope = current!.SharedScope?.Expectation
                    },
                    DecisionMaterial = current!.SharedScope is null ? material : null
                };
            }
            try {
                var committed = await unitOfWork.CommitAsync(request, cancellationToken).ConfigureAwait(false);
                RequireCaptureReceipt(committed, identity, attach ? stamp : null, work);
                return new(attach ? ProcessDecisionStepPreparationStatus.Ready : ProcessDecisionStepPreparationStatus.ReviewRequired, committed.State);
            } catch (Exception failure) when (IsStale(failure) ||
                    failure is IOException or ProcessRuntimeDispatchPreconditionException or ProcessExecutionAuthorityMismatchException) {
                logger.LogWarning("Step capture settlement for run {RunId}, allocation {AllocationId} requires reconciliation after {FailureKind}.",
                    runId, identity.CaptureAllocation, failure.GetType().Name);
            }
        }
        throw new IOException("Step capture closed locally but its original accounting receipt could not be reconciled.");
    }

    private static void RequireReservation(ProcessRuntimeCommitResult receipt, ProcessDecisionStepIdentity identity, long grant) {
        if (!receipt.Succeeded || receipt.State.RunId != identity.RunId ||
                receipt.DecisionBudget is not { Command: ProcessDecisionBudgetReservation } accounting ||
                accounting.Command.Expectation.StepInstanceId != identity.StepId || accounting.Command.Expectation.BudgetId != identity.BudgetId ||
                accounting.Command.AllocationId != identity.CaptureAllocation || accounting.Change.Allocation.Settlement is not null ||
                accounting.Change.Allocation.Grant != new DecisionBudgetAmount(grant, 0)) {
            throw new InvalidDataException("The original step capture reservation receipt changed.");
        }
    }

    private static void RequireCaptureReceipt(ProcessRuntimeCommitResult receipt, ProcessDecisionStepIdentity identity,
        ProcessDecisionMaterialStamp? stamp, DecisionBudgetSettlement? work) {
        if (!receipt.Succeeded || receipt.State.RunId != identity.RunId ||
                receipt.DecisionBudget is not { Command: ProcessDecisionBudgetSettlement } accounting ||
                accounting.Command.Expectation.StepInstanceId != identity.StepId || accounting.Command.Expectation.BudgetId != identity.BudgetId ||
                accounting.Command.AllocationId != identity.CaptureAllocation ||
                accounting.Change.Allocation.Settlement is not { } settlement || work is not null && settlement != work ||
                stamp is null && receipt.Decision is not null ||
                stamp is not null && (receipt.Decision?.Command is not ProcessDecisionStart start ||
                    start.DecisionId != identity.Decision || start.Material != stamp)) {
            throw new InvalidDataException("The original step capture settlement receipt changed.");
        }
    }

    private ProcessRuntimeCommitRequest Request(ProcessRuntimeStateSnapshot state, RuntimeCommandId command, ProcessDecisionBudgetCommand budget) {
        var now = clock.GetUtcNow();
        var ticks = Math.Max(now.UtcTicks - now.UtcTicks % 10, checked(state.UpdatedAtUtc.UtcTicks + 10));
        return new(command, state, new(ProcessRuntimeTransitionOutcome.Applied, state with {
            UpdatedAtUtc = new(ticks, TimeSpan.Zero)
        }, [], [], [], [])) { DecisionBudget = budget };
    }

    private async Task<ProcessRuntimeStateSnapshot> ReloadAsync(ProcessRunId runId, CancellationToken cancellationToken)
        => await states.LoadAsync(runId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidDataException("The original Process runtime disappeared during step preparation.");

    private static bool IsStale(Exception failure) => failure is ProcessRuntimeOptimisticConcurrencyException or
        ProcessDecisionException { Reason: ProcessDecisionRefusal.StaleExpectation } or
        ProcessDecisionRunScopeException { Reason: ProcessDecisionRunScopeRefusal.StaleExpectation };
}
