using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Builder;
using CanDoItAll.Processes.Core;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Processes.Application;

public sealed partial class ProcessLaunchApplicationService {
    private const string PendingContinuationMessage = "The process was accepted. Launch continuation is pending and can be recovered from its admission.";
    private const string AuthorityReconciliationMessage = "The process was accepted, but its saved launch authority requires reconciliation before further launch work.";

    public async Task<ProcessLaunchObservation> GetLaunchStatusAsync(ProcessLaunchAdmissionId admissionId,
        ProcessLaunchAuthority caller, CancellationToken cancellationToken = default) {
        var snapshot = await RequirePreparedStore().GetAsync(admissionId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("The process launch admission was not found.");
        caller.Validate();
        ProcessLaunchIntentFingerprint.RequireSameCaller(snapshot.Preparation, caller);
        var observation = Observation(snapshot, snapshot.AcceptedAtUtc is not null);
        if (observation.AcceptedRunId is { } runId) {
            var state = await stateStore.LoadAsync(runId, cancellationToken).ConfigureAwait(false);
            return observation with { RuntimeStatus = state?.Status };
        }
        return observation;
    }

    public async Task<ProcessLaunchResult> ResumeAcceptedLaunchAsync(ProcessLaunchAdmissionId admissionId,
        CancellationToken cancellationToken = default) {
        var snapshot = await RequirePreparedStore().GetAsync(admissionId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("The process launch admission was not found.");
        if (snapshot.AcceptedAtUtc is null) {
            throw new InvalidOperationException("An unaccepted process preparation cannot be resumed as a background launch.");
        }
        return await ContinueAcceptedLaunchAsync(snapshot, cancellationToken).ConfigureAwait(false);
    }

    private async Task<ProcessLaunchResult> PreviewPreparedLaunchAsync(ProcessLaunchRequest request, CancellationToken cancellationToken) {
        var prepared = await PrepareDurableLaunchAsync(request, cancellationToken).ConfigureAwait(false);
        if (prepared.EarlyResult is not null) {
            return prepared.EarlyResult;
        }
        var snapshot = prepared.Snapshot ?? throw new InvalidOperationException("The durable process preview has no preparation.");
        return PreparedResult(snapshot, snapshot.AcceptedAtUtc is not null);
    }

    private async Task<ProcessLaunchResult> LaunchPreparedAsync(ProcessLaunchRequest request, CancellationToken cancellationToken) {
        var prepared = await PrepareDurableLaunchAsync(request, cancellationToken).ConfigureAwait(false);
        if (prepared.EarlyResult is not null) {
            return prepared.EarlyResult;
        }
        var snapshot = prepared.Snapshot ?? throw new InvalidOperationException("The durable process launch has no preparation.");
        if (snapshot.AcceptedAtUtc is not null) {
            if (snapshot.Execute != request.Execute) {
                throw new ProcessLaunchIntentConflictException(request.CallerIntentId, "The process launch retry changed its accepted execution choice.");
            }
            return await ContinueAcceptedLaunchAsync(snapshot, cancellationToken).ConfigureAwait(false);
        }
        ProcessRuntimeCommitResult committed;
        try {
            committed = await unitOfWork.CommitAsync(snapshot.Preparation.InitialCommit with {
                InitialLaunchAdmission = new(snapshot.Preparation.AdmissionId, snapshot.PreparationFingerprint, request.Execute) {
                    CurrentCallerAuthority = request.Authority
                }
            }, cancellationToken).ConfigureAwait(false);
        } catch (Exception exception) {
            var accepted = await TryObserveAcceptanceAsync(snapshot.Preparation.AdmissionId).ConfigureAwait(false);
            if (accepted is null) {
                throw;
            }
            return PreparedResult(accepted, knownAccepted: true, PendingContinuationMessage, exception);
        }
        if (!committed.Succeeded) {
            return new(snapshot.Preparation.Review.DefinitionId, snapshot.Preparation.Review.PlanId, null,
                ProcessLaunchStage.Failed, string.Empty, snapshot.Preparation.Review,
                committed.Diagnostics.Select(item => item.Message).ToArray()) {
                Observation = Observation(snapshot, knownAccepted: false)
            };
        }
        return await ContinueAcceptedLaunchAsync(snapshot, cancellationToken).ConfigureAwait(false);
    }

    private async Task<(ProcessPreparedLaunchSnapshot? Snapshot, ProcessLaunchResult? EarlyResult)> PrepareDurableLaunchAsync(
        ProcessLaunchRequest request, CancellationToken cancellationToken) {
        ValidatePreparedRequest(request);
        var store = RequirePreparedStore();
        var fingerprint = ProcessLaunchIntentFingerprint.Compute(request);
        var saved = request.PreparedAdmissionId is { } preparedId
            ? await store.GetAsync(preparedId, cancellationToken).ConfigureAwait(false)
            : request.CallerIntentId is { } intentId
                ? await store.FindByIntentAsync(intentId, cancellationToken).ConfigureAwait(false)
                : null;
        if (saved is not null) {
            RequireMatchingRequest(saved, request, fingerprint);
            return (saved, null);
        }
        if (request.PreparedAdmissionId is not null) {
            throw new InvalidOperationException("The reviewed process preparation is missing; a new launch intent is required.");
        }

        var now = NormalizeUtc(clock.GetUtcNow());
        var prepared = await PrepareLaunchAsync(request, now, cancellationToken).ConfigureAwait(false);
        if (prepared.EarlyResult is not null) {
            return (null, prepared.EarlyResult);
        }
        var plan = prepared.Plan ?? throw new InvalidOperationException("Prepared process launch did not include an instance plan.");
        var selected = prepared.Selected ?? throw new InvalidOperationException("Prepared process launch did not include a template selection.");
        var review = prepared.LaunchPlan ?? throw new InvalidOperationException("Prepared process launch did not include a review.");
        var admissionId = new ProcessLaunchAdmissionId(Guid.NewGuid());
        var initialState = BuildInitialState(plan, selected.Definition, prepared.Assignments, request.RootRunIdOverride, now) with {
            ProjectAdmission = request.ProjectAdmission,
            LaunchAdmissionId = admissionId
        };
        var context = CreateContext(request.RequestedBy, now);
        var mutation = CreateAppliedMutation(initialState, context, ProcessRuntimeEventTypes.ProcessRunCreated, plan.PlanHash);
        var parent = ProcessRuntimeLaunchVariables.TryReadParentStep(request.Variables, out var parentStep)
            ? parentStep : (ProcessRuntimeParentStepReference?)null;
        var initial = new ProcessRuntimeCommitRequest(context.CommandId, initialState, mutation, parent, plan) {
            InitialAssignments = prepared.Assignments
        };
        var result = await store.PrepareAsync(new(admissionId, request.CallerIntentId, fingerprint, request.Authority,
            request with { PreparedAdmissionId = null }, initial, review, request.LinkTarget, now), cancellationToken).ConfigureAwait(false);
        if (request.CallerIntentId is not null) {
            RequireMatchingRequest(result, request, fingerprint);
        }
        return (result, null);
    }

    private async Task<ProcessLaunchResult> ContinueAcceptedLaunchAsync(ProcessPreparedLaunchSnapshot known,
        CancellationToken cancellationToken) {
        var store = RequirePreparedStore();
        ProcessLaunchContinuationClaim? claim = null;
        try {
            claim = await store.ClaimContinuationAsync(known.Preparation.AdmissionId, cancellationToken).ConfigureAwait(false);
            if (claim is null) {
                var observed = await store.GetAsync(known.Preparation.AdmissionId, cancellationToken).ConfigureAwait(false)
                    ?? throw new InvalidOperationException("The accepted process launch admission is missing.");
                var observedResult = PreparedResult(observed, knownAccepted: true);
                var state = await stateStore.LoadAsync(observed.Preparation.InitialCommit.Mutation.State.RunId, cancellationToken).ConfigureAwait(false);
                return state is null ? observedResult : new(observedResult.DefinitionId, observedResult.LaunchPlanId, observedResult.RunId,
                    state.Status == ProcessRuntimeStatus.Created ? ProcessLaunchStage.Planned : MapLaunchStage(state.Status),
                    observedResult.Route, observedResult.LaunchPlan, observedResult.Warnings) {
                    Observation = observedResult.Observation! with { RuntimeStatus = state.Status }
                };
            }
            var saved = claim.Snapshot;
            if (saved.Preparation.Authority is { } authority) {
                if (launchAuthorityPolicy is null) {
                    throw new ProcessLaunchAuthorityRejectedException("The saved process launch has no configured current authority policy.");
                }
                await launchAuthorityPolicy.RequireCurrentAsync(authority, cancellationToken).ConfigureAwait(false);
            }
            var result = await ContinueWithLeaseAsync(claim, cancellationToken).ConfigureAwait(false);
            var outcome = result.Stage == ProcessLaunchStage.Failed ? ProcessLaunchContinuationState.Failed : ProcessLaunchContinuationState.Started;
            await store.CompleteContinuationAsync(claim, outcome, publicFailure: null, cancellationToken).ConfigureAwait(false);
            return result with { Observation = Observation(saved with { State = outcome }, knownAccepted: true, result.Observation?.ObservationException) };
        } catch (ProcessLaunchAuthorityRejectedException exception) {
            if (claim is not null) {
                await TrySuspendContinuationAsync(claim).ConfigureAwait(false);
            }
            var observed = await TryObserveAcceptanceAsync(known.Preparation.AdmissionId).ConfigureAwait(false) ?? known;
            return PreparedResult(observed, knownAccepted: true, AuthorityReconciliationMessage, exception);
        } catch (Exception exception) {
            var observed = await TryObserveAcceptanceAsync(known.Preparation.AdmissionId).ConfigureAwait(false) ?? known;
            return PreparedResult(observed, knownAccepted: true, PendingContinuationMessage, exception);
        }
    }

    private async Task<ProcessLaunchResult> ContinueWithLeaseAsync(ProcessLaunchContinuationClaim claim, CancellationToken cancellationToken) {
        var prepared = claim.Snapshot.Preparation;
        var initial = prepared.InitialCommit;
        var request = prepared.Request with {
            Execute = claim.Snapshot.Execute ?? throw new InvalidOperationException("The accepted process launch has no execution choice."),
            ProjectAdmission = initial.Mutation.State.ProjectAdmission,
            Authority = prepared.Authority,
            LinkTarget = prepared.LinkTarget
        };
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var work = ContinueInitialLaunchAsync(initial.InitialPlan!, prepared.Review, request, initial.Mutation.State, lifetime.Token);
        var renewal = RenewContinuationWhileRunningAsync(claim, lifetime.Token);
        try {
            var completed = await Task.WhenAny(work, renewal).ConfigureAwait(false);
            if (completed == renewal) {
                await renewal.ConfigureAwait(false);
            }
            return await work.ConfigureAwait(false);
        } finally {
            await lifetime.CancelAsync().ConfigureAwait(false);
            await Task.WhenAll(work, renewal).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        }
    }

    private async Task RenewContinuationWhileRunningAsync(ProcessLaunchContinuationClaim claim, CancellationToken cancellationToken) {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(20));
        while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false)) {
            if (!await RequirePreparedStore().RenewContinuationAsync(claim, cancellationToken).ConfigureAwait(false)) {
                throw new InvalidOperationException("The process launch continuation lost its durable lease.");
            }
        }
    }

    private async Task TrySuspendContinuationAsync(ProcessLaunchContinuationClaim claim) {
        try {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await RequirePreparedStore().CompleteContinuationAsync(claim, ProcessLaunchContinuationState.ReconciliationRequired,
                AuthorityReconciliationMessage, timeout.Token).ConfigureAwait(false);
        } catch (Exception) {
            // Preserve the original authority failure; the expired claim remains available for explicit recovery.
        }
    }

    private async Task<ProcessPreparedLaunchSnapshot?> TryObserveAcceptanceAsync(ProcessLaunchAdmissionId admissionId) {
        try {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var observed = await RequirePreparedStore().GetAsync(admissionId, timeout.Token).ConfigureAwait(false);
            return observed?.AcceptedAtUtc is not null ? observed : null;
        } catch (Exception) {
            return null;
        }
    }

    private static ProcessLaunchResult PreparedResult(ProcessPreparedLaunchSnapshot snapshot, bool knownAccepted,
        string? pendingMessage = null, Exception? exception = null) {
        var prepared = snapshot.Preparation;
        var runId = knownAccepted ? prepared.InitialCommit.Mutation.State.RunId : (ProcessRunId?)null;
        var stage = snapshot.State switch {
            ProcessLaunchContinuationState.Started => ProcessLaunchStage.Running,
            ProcessLaunchContinuationState.Failed => ProcessLaunchStage.Failed,
            _ => ProcessLaunchStage.Planned
        };
        var warnings = prepared.Review.ReadinessFindings.Where(item => item.Severity != ProcessLaunchReadinessSeverity.Info)
            .Select(item => item.Message).ToList();
        if ((pendingMessage ?? snapshot.PublicFailure) is { } pending) {
            warnings.Add(pending);
        }
        return new(prepared.Review.DefinitionId, prepared.Review.PlanId, runId, stage,
            runId is { } id ? BuildRunRoute(id, prepared.Request.ProjectId) : string.Empty, prepared.Review, warnings) {
            Observation = Observation(snapshot, knownAccepted, exception)
        };
    }

    private static ProcessLaunchObservation Observation(ProcessPreparedLaunchSnapshot snapshot, bool knownAccepted, Exception? exception = null)
        => new(snapshot.Preparation.AdmissionId, knownAccepted ? snapshot.Preparation.InitialCommit.Mutation.State.RunId : null,
            knownAccepted && snapshot.State == ProcessLaunchContinuationState.Prepared ? ProcessLaunchContinuationState.Accepted : snapshot.State,
            snapshot.LinkDeliveryState, snapshot.PublicFailure) { ObservationException = exception };

    private static void ValidatePreparedRequest(ProcessLaunchRequest request) {
        request.Authority?.Validate();
        if (request.ProjectAdmission is { } admission && request.ProjectId != admission.ProjectId ||
                request.Authority is { } authority && request.ProjectAdmission != authority.ProjectAdmission ||
                request.CallerIntentId is not null && request.Authority is null) {
            throw new InvalidOperationException("The process launch's trusted authority, project admission or caller intent is missing or inconsistent.");
        }
        if (request.LinkTarget is { } target &&
                (request.Authority?.ProjectAdmission is not { } project || target.ProjectId != project.ProjectId ||
                    target.ProjectId != request.ProjectId || target.SourceNodeKey != request.ProjectNodeId ||
                    string.IsNullOrWhiteSpace(target.SourceNodeKey) || target.SourceNodeKey.Length > 512 ||
                    string.IsNullOrWhiteSpace(target.SourceBindingFingerprint))) {
            throw new InvalidOperationException("The process launch link does not match its trusted source node and project lifetime.");
        }
    }

    private static void RequireMatchingRequest(ProcessPreparedLaunchSnapshot snapshot, ProcessLaunchRequest request, string fingerprint) {
        ProcessLaunchIntentFingerprint.RequireSameCaller(snapshot.Preparation, request.Authority);
        if (snapshot.Preparation.RequestFingerprint != fingerprint ||
                request.CallerIntentId is { } intent && snapshot.Preparation.CallerIntentId != intent) {
            throw new ProcessLaunchIntentConflictException(request.CallerIntentId, "The process launch retry changed its reviewed content, source or target.");
        }
    }

    private IProcessPreparedLaunchStore RequirePreparedStore()
        => preparedLaunchStore ?? throw new InvalidOperationException("Prepared process launch requires its configured durable owner store.");

    private static bool UsesPreparedPreview(ProcessLaunchRequest request)
        => request.CallerIntentId is not null || request.PreparedAdmissionId is not null || request.Authority is not null || request.LinkTarget is not null;

    private static ProcessRuntimeCommitResult ObservedCommit(ProcessRuntimeStateSnapshot state)
        => new(ProcessRuntimeTransitionOutcome.Duplicate, state, [], [], [], []);

    private async Task<ProcessLaunchResult> ContinueInitialLaunchAsync(ProcessInstancePlan plan, ProcessLaunchPlanView launchPlan,
        ProcessLaunchRequest request, ProcessRuntimeStateSnapshot initialState, CancellationToken cancellationToken) {
        var artifactRoot = BuildManagedProcessArtifactRoot(initialState.RunId);
        var engine = new ProcessRuntimeEngine(unitOfWork);
        var observed = await stateStore.LoadAsync(initialState.RunId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Accepted process run '{initialState.RunId}' is missing.");
        try {
            if (observed.Status == ProcessRuntimeStatus.Created) {
                await artifactInitializer.InitializeAsync(
                    new ProcessLaunchArtifactInitializationRequest(
                        initialState.RunId,
                        plan.Definition.DefinitionId,
                        plan.Header.PlanId,
                        launchPlan.DefinitionKey,
                        request.ProjectId,
                        artifactRoot),
                    cancellationToken).ConfigureAwait(false);
            }
        } catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException) {
            var diagnostics = new List<string> {
                $"Failed to initialize managed process artifact root '{artifactRoot}': {exception.Message}"
            };
            diagnostics.AddRange(await TryCancelFailedLaunchAsync(
                engine,
                initialState.RunId,
                request.RequestedBy,
                cancellationToken).ConfigureAwait(false));
            diagnostics.AddRange(await TryCatchUpFailedLaunchProjectionAsync(cancellationToken).ConfigureAwait(false));
            return new ProcessLaunchResult(
                plan.Definition.DefinitionId,
                plan.Header.PlanId,
                initialState.RunId,
                ProcessLaunchStage.Failed,
                BuildRunRoute(initialState.RunId, request.ProjectId),
                launchPlan,
                diagnostics) {
                Observation = initialState.LaunchAdmissionId is { } admission
                    ? new(admission, initialState.RunId, ProcessLaunchContinuationState.Failed,
                        request.LinkTarget is null ? ProcessLaunchLinkDeliveryState.NotRequested : ProcessLaunchLinkDeliveryState.Pending,
                        "The accepted process could not initialize its managed artifacts.") { ObservationException = exception }
                    : null
            };
        }

        var activeCommit = await ExecuteLaunchLifecycleTransitionWithConcurrencyRetryAsync(
            initialState.RunId,
            reloadedState => reloadedState.Status == ProcessRuntimeStatus.Created
                ? engine.ActivateAsync(reloadedState, CreateContext(request.RequestedBy, NormalizeUtc(clock.GetUtcNow())), cancellationToken)
                : Task.FromResult(ObservedCommit(reloadedState)),
            cancellationToken).ConfigureAwait(false);
        if (!activeCommit.Succeeded) {
            return await BuildFailedLifecycleLaunchResultAsync(
                plan,
                launchPlan,
                request,
                engine,
                activeCommit,
                "activation",
                cancellationToken).ConfigureAwait(false);
        }

        var scheduled = await ExecuteLaunchLifecycleTransitionWithConcurrencyRetryAsync(
            initialState.RunId,
            reloadedState => reloadedState.Status == ProcessRuntimeStatus.Active
                ? engine.ScheduleReadyAsync(reloadedState, CreateContext(request.RequestedBy, NormalizeUtc(clock.GetUtcNow())), cancellationToken)
                : Task.FromResult(ObservedCommit(reloadedState)),
            cancellationToken).ConfigureAwait(false);
        if (!scheduled.Succeeded) {
            return await BuildFailedLifecycleLaunchResultAsync(
                plan,
                launchPlan,
                request,
                engine,
                scheduled,
                "ready-step scheduling",
                cancellationToken).ConfigureAwait(false);
        }

        await projectionCatchupService.CatchUpAsync(cancellationToken).ConfigureAwait(false);

        var activeState = scheduled.State;
        var stage = MapLaunchStage(activeState.Status);
        if (request.Execute &&
            stage == ProcessLaunchStage.Running &&
            activeState.Status == ProcessRuntimeStatus.Active) {
            await dispatchQueue.EnqueueAsync(
                new ProcessRuntimeDispatchQueueRequest(activeState.RunId, request.RequestedBy),
                cancellationToken).ConfigureAwait(false);
        }

        return new ProcessLaunchResult(
            plan.Definition.DefinitionId,
            plan.Header.PlanId,
            activeState.RunId,
            stage,
            BuildRunRoute(activeState.RunId, request.ProjectId),
            launchPlan,
            launchPlan.ReadinessFindings
                .Where(finding => finding.Severity != ProcessLaunchReadinessSeverity.Info)
                .Select(finding => finding.Message)
                .ToArray());
    }

}
