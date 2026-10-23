using System.Runtime.ExceptionServices;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Processes.Application;

public enum ProcessDecisionRefreshCaptureStatus { Advanced, Pending, ReviewRequired, Stopped, SourceProjectionRequired }

public sealed record ProcessDecisionRefreshCaptureResult(ProcessDecisionRefreshCaptureStatus Status, ProcessRuntimeStateSnapshot State);

public sealed class ProcessDecisionRefreshCaptureCoordinator(IProcessDecisionRefreshCaptureQuery query,
    IProcessDecisionRootQuery roots, IProcessDecisionResultQuery budgets, IEnumerable<IProcessDecisionMaterialCapturer> capturers,
    IProcessRuntimeStateStore states, IProcessRuntimeUnitOfWork unitOfWork, TimeProvider clock,
    ILogger<ProcessDecisionRefreshCaptureCoordinator> logger) {
    private const int MaximumAttempts = 3;

    public async Task<ProcessDecisionRefreshCaptureResult> PrepareAsync(ProcessRuntimeStateSnapshot state, RuntimeCommandContext context,
        RuntimeCommandId admissionId, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(context);
        context.Validate();
        ProcessDecisionRefreshCaptureIdentity identity = new(admissionId);
        for (var retry = 0; retry < MaximumAttempts; retry++) {
            state = await ReloadAsync(state.RunId, cancellationToken).ConfigureAwait(false);
            var progress = await ProgressAsync(state, identity, cancellationToken).ConfigureAwait(false);
            if (progress.Successor is not null) {
                return new(ProcessDecisionRefreshCaptureStatus.Advanced, state);
            }
            if (progress.RootAbort is not null || progress.LocalAbort is not null) {
                return new(Stopped(state.Status) ? ProcessDecisionRefreshCaptureStatus.Stopped : ProcessDecisionRefreshCaptureStatus.ReviewRequired, state);
            }
            if (progress.LocalReservation is not null) {
                return new(ProcessDecisionRefreshCaptureStatus.Pending, state);
            }
            var captureInvoked = false;
            try {
                var selected = await query.ReadAsync(state, admissionId, cancellationToken).ConfigureAwait(false);
                if (Stopped(state.Status) || Stopped(selected.RootStatus)) {
                    return new(ProcessDecisionRefreshCaptureStatus.Stopped, state);
                }
                if (selected.Original.OtherWorkPending || selected.SharedScope is not null && selected.RootWorkPending) {
                    return new(ProcessDecisionRefreshCaptureStatus.Pending, state);
                }
                var root = selected.SharedScope is not null && progress.RootCompletion is null;
                if (root && (progress.RootReservation is not null || selected.SharedScope!.Phase != ProcessDecisionRunScopePhase.Reconciling)) {
                    return new(ProcessDecisionRefreshCaptureStatus.Pending, state);
                }
                if (selected.SharedScope is not null && !root &&
                        (selected.SharedScope.Phase != ProcessDecisionRunScopePhase.Active ||
                            selected.SharedScope.Material?.Stamp != progress.RootCompletion!.Scope!.State.Material?.Stamp ||
                            selected.SharedScope.Expectation != progress.RootCompletion.Scope.State.Expectation)) {
                    return new(ProcessDecisionRefreshCaptureStatus.ReviewRequired, state);
                }
                var attempt = ProcessDecisionRefreshCaptureAttempt.Create(identity, selected, root);
                if (ProcessDecisionReviewCompletion.ValidatePending(state, attempt.Intent.Request.Child,
                        attempt.Intent.Request.StrategyId, attempt.Intent.Request.WaitingResultKey) is not null) {
                    return new(ProcessDecisionRefreshCaptureStatus.ReviewRequired, state);
                }
                var capturer = root || selected.SharedScope is null
                    ? capturers.SingleOrDefault(item => item.ContentContract == attempt.CaptureRequest.ContentContract)
                        ?? throw new InvalidOperationException("The original refresh content contract has no registered capturer.")
                    : null;
                var reserved = await unitOfWork.CommitAsync(attempt.Reservation(state, clock.GetUtcNow()), cancellationToken).ConfigureAwait(false);
                attempt.RequireReceipt(reserved, attempt.Reserve);
                if (!reserved.CommittedNow) {
                    return new(ProcessDecisionRefreshCaptureStatus.Pending, reserved.State);
                }
                captureInvoked = true;
                var result = await CaptureAsync(reserved.State, context, attempt, capturer, cancellationToken).ConfigureAwait(false);
                if (!root || result.Status != ProcessDecisionRefreshCaptureStatus.Advanced) {
                    return result;
                }
                state = result.State;
            } catch (Exception failure) when (!captureInvoked && (IsStale(failure) || failure is IOException)) {
                logger.LogWarning("Refresh {AdmissionId} in run {RunId} requires progress reconciliation after {FailureKind}.",
                    admissionId.Value, state.RunId.Value, failure.GetType().Name);
            } catch (Exception failure) when (!captureInvoked && failure is (DecisionBudgetException or
                    ProcessDecisionException { Reason: ProcessDecisionRefusal.GenerationCapacity } or
                    ProcessDecisionRunScopeException { Reason: ProcessDecisionRunScopeRefusal.MembershipCapacity })) {
                return new(ProcessDecisionRefreshCaptureStatus.ReviewRequired, state);
            } catch (Exception failure) when (!captureInvoked && failure is (ProcessDecisionSourceProjectionRequiredException or ProcessAcceptedExecutionInputRequiredException)) {
                return new(ProcessDecisionRefreshCaptureStatus.SourceProjectionRequired, state);
            }
        }
        return new(ProcessDecisionRefreshCaptureStatus.Pending, state);
    }

    private async Task<ProcessDecisionRefreshCaptureResult> CaptureAsync(ProcessRuntimeStateSnapshot state, RuntimeCommandContext context,
        ProcessDecisionRefreshCaptureAttempt attempt, IProcessDecisionMaterialCapturer? capturer, CancellationToken cancellationToken) {
        ProcessDecisionMaterial? material = null;
        ProcessDecisionRunScopeMaterial? basis = null;
        Exception? failure = null;
        using var deadline = new CancellationTokenSource(TimeSpan.FromTicks(attempt.Grant.WorkTicks), clock);
        using var captureCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
        var started = clock.GetTimestamp();
        try {
            if (capturer is not null) {
                var request = attempt.CaptureRequest;
                switch (await capturer.CaptureAsync(request, captureCancellation.Token).ConfigureAwait(false)) {
                    case ProcessDecisionMaterialCaptureResult.Captured captured:
                        material = captured.Material ?? throw new InvalidDataException("Refresh capture returned no material.");
                        if (material.Scope != request.Scope || material.Reference != request.Material || material.ContentContract != request.ContentContract) {
                            throw new InvalidDataException("Refresh capture changed its original scope, material identity or content contract.");
                        }
                        basis = ProcessDecisionRunScopeMaterial.From(material);
                        break;
                    case ProcessDecisionMaterialCaptureResult.Refused refused when Enum.IsDefined(refused.Reason):
                        logger.LogWarning("Refresh {AdmissionId} in run {RunId} was refused by its content owner: {Reason}.",
                            attempt.Identity.AdmissionId.Value, state.RunId.Value, refused.Reason);
                        break;
                    default:
                        throw new InvalidDataException("Refresh capture returned an unsupported content outcome.");
                }
            } else {
                var shared = await roots.ReadAsync(state, captureCancellation.Token).ConfigureAwait(false)
                    ?? throw new InvalidDataException("Refresh inheritance lost its original root scope.");
                if (shared.Scope.Expectation != attempt.Original.SharedScope!.Expectation ||
                        shared.Scope.Phase != ProcessDecisionRunScopePhase.Active || shared.Scope.Material != attempt.Original.SharedScope.Material) {
                    throw new ProcessDecisionRunScopeException(ProcessDecisionRunScopeRefusal.StaleExpectation);
                }
                basis = shared.Scope.Material;
            }
            if (basis is not null && !Fresh(basis, attempt)) {
                basis = null;
                material = null;
                logger.LogWarning("Refresh {AdmissionId} in run {RunId} returned expired material.", attempt.Identity.AdmissionId.Value, state.RunId.Value);
            }
        } catch (OperationCanceledException) when (deadline.IsCancellationRequested && !cancellationToken.IsCancellationRequested) {
            material = null;
            basis = null;
        } catch (Exception exception) {
            failure = exception;
            material = null;
            basis = null;
            logger.LogWarning("Refresh {AdmissionId} in run {RunId} ended capture with {FailureKind}.",
                attempt.Identity.AdmissionId.Value, state.RunId.Value, exception.GetType().Name);
        }
        var elapsed = clock.GetElapsedTime(started);
        if (elapsed < TimeSpan.Zero) {
            throw new InvalidDataException("Refresh capture returned a negative monotonic work duration.");
        }
        if (elapsed.Ticks > attempt.Grant.WorkTicks || captureCancellation.IsCancellationRequested) {
            material = null;
            basis = null;
        }
        using var accountingDeadline = new CancellationTokenSource(TimeSpan.FromSeconds(5), clock);
        var result = await FinishAsync(state.RunId, context, attempt, material, basis,
            new(1, new(elapsed.Ticks, 0)), accountingDeadline.Token).ConfigureAwait(false);
        if (failure is not null) {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
        cancellationToken.ThrowIfCancellationRequested();
        return result;
    }

    private async Task<ProcessDecisionRefreshCaptureResult> FinishAsync(ProcessRunId runId, RuntimeCommandContext context,
        ProcessDecisionRefreshCaptureAttempt attempt, ProcessDecisionMaterial? material, ProcessDecisionRunScopeMaterial? basis,
        DecisionBudgetSettlement work, CancellationToken cancellationToken) {
        for (var retry = 0; retry < MaximumAttempts; retry++) {
            var state = await ReloadAsync(runId, cancellationToken).ConfigureAwait(false);
            var progress = await ProgressAsync(state, attempt.Identity, cancellationToken).ConfigureAwait(false);
            var completed = attempt.IsRoot ? progress.RootCompletion : progress.Successor;
            var aborted = attempt.IsRoot ? progress.RootAbort : progress.LocalAbort;
            if (completed is not null || aborted is not null) {
                attempt.RequireReceipt(completed ?? aborted!, completed is not null ? attempt.Complete : attempt.Abort,
                    work, completed is not null ? basis?.Stamp : null);
                return new(completed is not null ? ProcessDecisionRefreshCaptureStatus.Advanced
                    : Stopped(state.Status) ? ProcessDecisionRefreshCaptureStatus.Stopped : ProcessDecisionRefreshCaptureStatus.ReviewRequired, state);
            }
            try {
                ProcessDecisionRefreshCaptureSelection? current = null;
                if (basis is not null && !Stopped(state.Status)) {
                    try {
                        current = await query.ReadAsync(state, attempt.Identity.AdmissionId, cancellationToken).ConfigureAwait(false);
                    } catch (ProcessExecutionAuthorityMismatchException) {
                        logger.LogWarning("Refresh {AdmissionId} in run {RunId} lost original source eligibility before attachment.",
                            attempt.Identity.AdmissionId.Value, runId.Value);
                    }
                }
                var publish = basis is not null && current is not null && !Stopped(current.RootStatus) &&
                    !current.Original.Budget.OverrunDetected && !current.RootBudget.OverrunDetected &&
                    work.Consumed.WorkTicks <= attempt.Grant.WorkTicks && Fresh(basis, attempt) &&
                    current.Input.InputFingerprint == attempt.Original.Input.InputFingerprint &&
                    (attempt.IsRoot || current.SharedScope is null || current.SharedScope.Material == basis);
                var request = await CompletionAsync(state, context, attempt, current, publish ? material : null,
                    publish ? basis : null, work, cancellationToken).ConfigureAwait(false);
                var result = await unitOfWork.CommitAsync(request, cancellationToken).ConfigureAwait(false);
                attempt.RequireReceipt(result, request.CommandId, work, publish ? basis!.Stamp : null);
                return new(publish ? ProcessDecisionRefreshCaptureStatus.Advanced : Stopped(result.State.Status)
                    ? ProcessDecisionRefreshCaptureStatus.Stopped : ProcessDecisionRefreshCaptureStatus.ReviewRequired, result.State);
            } catch (ProcessDecisionException failure) when (failure.Reason == ProcessDecisionRefusal.UnclosedWork) {
                material = null;
                basis = null;
                logger.LogWarning("Refresh {AdmissionId} in run {RunId} cannot advance while other work is open; settling its closed capture.",
                    attempt.Identity.AdmissionId.Value, runId.Value);
            } catch (Exception failure) when (IsStale(failure) || failure is IOException or ProcessExecutionAuthorityMismatchException) {
                logger.LogWarning("Refresh {AdmissionId} in run {RunId} requires capture settlement reconciliation after {FailureKind}.",
                    attempt.Identity.AdmissionId.Value, runId.Value, failure.GetType().Name);
            }
        }
        throw new IOException("Refresh capture closed locally but its original settlement receipt could not be reconciled.");
    }

    private async Task<ProcessRuntimeCommitRequest> CompletionAsync(ProcessRuntimeStateSnapshot state, RuntimeCommandContext context,
        ProcessDecisionRefreshCaptureAttempt attempt, ProcessDecisionRefreshCaptureSelection? current, ProcessDecisionMaterial? material,
        ProcessDecisionRunScopeMaterial? basis, DecisionBudgetSettlement work, CancellationToken cancellationToken) {
        DecisionBudgetBalance balance;
        ProcessDecisionRunScopeState? scope = null;
        if (attempt.IsRoot) {
            var root = await roots.ReadAsync(state, cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidDataException("Refresh settlement lost its original root budget.");
            balance = root.Budget;
            scope = root.Scope;
            if (scope.Admission != attempt.Original.SharedScope!.Admission || scope.Capture != attempt.RootCapture) {
                throw new ProcessDecisionRunScopeException(ProcessDecisionRunScopeRefusal.StaleExpectation);
            }
        } else {
            var selected = await budgets.ReadAsync(state.RunId, attempt.Original.Original.StepInstanceId, null, cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidDataException("Refresh settlement lost its original local budget.");
            selected.RequireOriginal(state, attempt.Original.Original.StepInstanceId, null);
            balance = selected.Budget;
        }
        if (balance.BudgetId != attempt.Budget.BudgetId) {
            throw new InvalidDataException("Refresh capture cannot settle a replacement budget.");
        }
        var request = ProcessDecisionRefreshCaptureAttempt.TimestampOnly(state, basis is not null ? attempt.Complete : attempt.Abort, clock.GetUtcNow()) with {
            DecisionBudget = new ProcessDecisionBudgetSettlement(new(attempt.IsRoot ? null : attempt.Original.Original.StepInstanceId,
                balance.BudgetId, balance.Revision), attempt.Allocation, work)
        };
        if (attempt.IsRoot) {
            return request with { DecisionRunScope = new(scope!.Expectation, basis is not null
                ? ProcessDecisionRunScopeCommandKind.CompleteCapture : ProcessDecisionRunScopeCommandKind.AbortCapture, Material: basis?.Stamp),
                DecisionMaterial = material };
        }
        if (basis is null) {
            return request;
        }
        ProcessDecisionRefresh command = new(attempt.Original.Original.StepInstanceId, current!.Original.Head!.Expectation, attempt.Identity.Material,
            current.SharedScope is null ? ProcessDecisionRefreshKind.ManagerRefresh : ProcessDecisionRefreshKind.ParentRefresh) {
            AdmissionCommandId = attempt.Identity.AdmissionId, Material = basis.Stamp, InheritedScope = current.SharedScope?.Expectation
        };
        return request with {
            Mutation = new ProcessDecisionRefreshSuccessor(attempt.Identity.AdmissionId).Prepare(state,
                context with { OccurredAtUtc = clock.GetUtcNow() }, attempt.Intent, command),
            Decision = command, DecisionMaterial = material
        };
    }

    private async Task<ProcessDecisionRefreshCaptureProgress> ProgressAsync(ProcessRuntimeStateSnapshot state,
        ProcessDecisionRefreshCaptureIdentity identity, CancellationToken cancellationToken) {
        var progress = await query.ReadProgressAsync(state.RunId, identity.AdmissionId, cancellationToken).ConfigureAwait(false);
        ProcessDecisionRefreshCaptureReceipts.Require(progress, state.RootRunId, identity);
        return progress;
    }

    private bool Fresh(ProcessDecisionRunScopeMaterial material, ProcessDecisionRefreshCaptureAttempt attempt) {
        var now = clock.GetUtcNow();
        return now >= material.CapturedAtUtc && now - material.CapturedAtUtc <= attempt.Original.Input.Assignment.DecisionDataPolicy!.Policy.Budget.MaximumBasisAge;
    }

    private async Task<ProcessRuntimeStateSnapshot> ReloadAsync(ProcessRunId runId, CancellationToken cancellationToken)
        => await states.LoadAsync(runId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidDataException("The original Process runtime disappeared during refresh capture.");

    private static bool Stopped(ProcessRuntimeStatus status)
        => status == ProcessRuntimeStatus.CancelRequested || ProcessRuntimeTerminalStates.IsRunTerminal(status);

    private static bool IsStale(Exception failure) => failure is ProcessRuntimeOptimisticConcurrencyException or
        ProcessDecisionException { Reason: ProcessDecisionRefusal.StaleExpectation } or
        ProcessDecisionRunScopeException { Reason: ProcessDecisionRunScopeRefusal.StaleExpectation };
}
