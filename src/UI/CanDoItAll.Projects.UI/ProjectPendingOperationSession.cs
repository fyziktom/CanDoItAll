namespace CanDoItAll.Modules.Projects.Pages.Components;

public sealed class ProjectPendingOperationSession : IDisposable {
    private readonly IProjectPendingSubmissionStore store;
    private readonly bool hadStarterPlan;
    private int busy;
    private bool retired;
    private bool reconciliationStarted;

    public ProjectPendingOperationSession(IProjectPendingSubmissionStore store, ProjectDefinitionFrozenSubmission frozen, bool hadStarterPlan) {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(frozen);
        this.store = store;
        Frozen = frozen;
        this.hadStarterPlan = hadStarterPlan;
    }

    public ProjectPendingOperationSession(IProjectPendingSubmissionStore store, ProjectPendingSubmission restored)
        : this(store, restored.Frozen, restored.HadStarterPlan) => Record = restored;

    public ProjectDefinitionFrozenSubmission Frozen { get; }
    public ProjectPendingSubmission? Record { get; private set; }
    public ProjectDefinitionAcceptedReceipt? KnownAccepted { get; private set; }
    public ProjectDefinitionLookup? LastLookup { get; private set; }
    public ProjectPendingDispatchOutcome? LastOutcome { get; private set; }
    public ProjectPendingProblem Problem { get; private set; }
    public Exception? Failure { get; private set; }
    public bool IsRetired => retired;
    public bool IsBusy => Volatile.Read(ref busy) != 0;
    public bool IsReconciled { get; private set; }
    public bool CanReconcile => !retired && !IsBusy && !reconciliationStarted && Problem == ProjectPendingProblem.None &&
        KnownAccepted is not null && Record?.Stage == ProjectPendingStage.Accepted;

    public async Task PrepareAsync(CancellationToken cancellationToken = default) {
        Enter();
        try {
            Apply(await store.PrepareAsync(Frozen, hadStarterPlan, cancellationToken));
        } catch (Exception failure) {
            FailStorage(failure);
            throw;
        } finally {
            Exit();
        }
    }

    public async Task DispatchAsync(ProjectDefinitionWriteContext context, DateTimeOffset now, bool explicitRetry,
        Func<ProjectDefinitionSubmission, CancellationToken, Task<ProjectPendingDispatchOutcome>> dispatch,
        CancellationToken cancellationToken = default) {
        Enter();
        try {
            RequireContext(context, now);
            if (KnownAccepted is not null || Record is null ||
                Record.Stage != ProjectPendingStage.Prepared && !(explicitRetry && Record.Stage is ProjectPendingStage.Dispatching or ProjectPendingStage.Unconfirmed)) {
                throw new InvalidOperationException("The pending operation is not available for this dispatch.");
            }
            var attempt = Guid.NewGuid();
            ProjectPendingStoreChange claim;
            try {
                claim = await store.BeginDispatchAsync(Record, attempt, explicitRetry, cancellationToken);
                Apply(claim);
            } catch (Exception failure) {
                FailStorage(failure);
                throw;
            }
            if (claim.Status != ProjectPendingStoreStatus.Applied || retired) {
                return;
            }
            if (claim.Record is not { Stage: ProjectPendingStage.Dispatching } || claim.Record.AttemptId != attempt) {
                var failure = new InvalidOperationException("The applied claim does not contain this exact dispatch attempt.");
                FailStorage(failure);
                throw failure;
            }
            cancellationToken.ThrowIfCancellationRequested();
            ProjectPendingDispatchOutcome outcome;
            try {
                outcome = await dispatch(Frozen.Submission, cancellationToken);
            } catch (Exception failure) {
                Failure = failure;
                Problem = ProjectPendingProblem.OutcomeUnconfirmed;
                try {
                    await ObserveAttemptAsync(attempt, ProjectPendingAttemptOutcome.Unconfirmed);
                } catch (Exception storageFailure) {
                    Failure = new AggregateException("The dispatch failed and its uncertain outcome could not be persisted.", failure, storageFailure);
                    throw Failure;
                }
                throw;
            }
            LastOutcome = outcome;
            switch (outcome) {
                case ProjectPendingDispatchOutcome.Full full when full.Outcome is ProjectDefinitionOutcome<ProjectEditorAcknowledgement>.Accepted ||
                    Frozen.Submission.Kind is ProjectDefinitionCommandKind.Create or ProjectDefinitionCommandKind.Replace:
                    await ObserveOutcomeAsync(full.Outcome, attempt, static result => new ProjectDefinitionAcceptedReceipt.Full(result));
                    break;
                case ProjectPendingDispatchOutcome.Scalars scalars when scalars.Outcome is ProjectDefinitionOutcome<ProjectDefinitionScalarsAcknowledgement>.Accepted ||
                    Frozen.Submission.Kind == ProjectDefinitionCommandKind.UpdateScalars:
                    await ObserveOutcomeAsync(scalars.Outcome, attempt, static result => new ProjectDefinitionAcceptedReceipt.Scalars(result));
                    break;
                default:
                    Problem = ProjectPendingProblem.OutcomeUnconfirmed;
                    throw new InvalidOperationException("The dispatch outcome shape does not match the frozen submission.");
            }
        } finally {
            Exit();
        }
    }

    public async Task ObserveLookupAsync(ProjectDefinitionLookup lookup, CancellationToken cancellationToken = default) {
        Enter();
        try {
            LastLookup = lookup;
            switch (lookup) {
                case ProjectDefinitionLookup.FoundFull found:
                    await ObserveAcceptedAsync(new ProjectDefinitionAcceptedReceipt.Full(found.Result));
                    break;
                case ProjectDefinitionLookup.FoundScalars found:
                    await ObserveAcceptedAsync(new ProjectDefinitionAcceptedReceipt.Scalars(found.Result));
                    break;
                case ProjectDefinitionLookup.ClosedAbsent absence:
                    RequireOperation(absence.Operation);
                    try {
                        Apply(await store.ObserveClosedAbsentAsync(Frozen, absence, cancellationToken));
                    } catch (Exception failure) {
                        FailStorage(failure);
                        throw;
                    }
                    break;
                case ProjectDefinitionLookup.NotObserved observed:
                    RequireOperation(observed.Operation);
                    Problem = KnownAccepted is null ? ProjectPendingProblem.OutcomeUnconfirmed : ProjectPendingProblem.None;
                    break;
                case ProjectDefinitionLookup.DatasetChanged changed:
                    RequireOperation(changed.Operation);
                    Problem = ProjectPendingProblem.ContextChanged;
                    break;
                case ProjectDefinitionLookup.CallerContextChanged changed:
                    RequireOperation(changed.Operation);
                    Problem = ProjectPendingProblem.ContextChanged;
                    break;
                case ProjectDefinitionLookup.Expired expired:
                    RequireOperation(expired.Operation);
                    Problem = ProjectPendingProblem.RecoveryUnavailable;
                    break;
                case ProjectDefinitionLookup.DetailUnavailable unavailable:
                    RequireOperation(unavailable.Operation);
                    Problem = ProjectPendingProblem.RecoveryUnavailable;
                    break;
                case ProjectDefinitionLookup.InvalidContext invalid:
                    RequireOperation(invalid.Operation);
                    Problem = ProjectPendingProblem.ContextChanged;
                    break;
                default:
                    throw new ArgumentException("The recovery lookup is unsupported.", nameof(lookup));
            }
        } finally {
            Exit();
        }
    }

    public void Reconcile(Action<ProjectDefinitionAcceptedReceipt> reconcile) {
        if (!CanReconcile) {
            throw new InvalidOperationException("The accepted operation is not available for reconciliation.");
        }
        reconciliationStarted = true;
        try {
            reconcile(KnownAccepted!);
            IsReconciled = true;
        } catch (Exception failure) {
            Problem = ProjectPendingProblem.ReconciliationFailed;
            Failure = failure;
            throw;
        }
    }

    public async Task ClearReconciledAsync(CancellationToken cancellationToken = default) {
        Enter();
        try {
            if (!IsReconciled || Record?.Stage != ProjectPendingStage.Accepted) {
                throw new InvalidOperationException("Only reconciled acceptance can be cleared by this session.");
            }
            Apply(await store.ClearResolvedAsync(Record, cancellationToken));
        } catch (Exception failure) {
            FailStorage(failure);
            throw;
        } finally {
            Exit();
        }
    }

    private async Task ObserveOutcomeAsync<T>(ProjectDefinitionOutcome<T> outcome, Guid attempt,
        Func<ProjectDefinitionAccepted<T>, ProjectDefinitionAcceptedReceipt> receipt) where T : class {
        switch (outcome) {
            case ProjectDefinitionOutcome<T>.Accepted accepted:
                await ObserveAcceptedAsync(receipt(accepted.Result));
                break;
            case ProjectDefinitionOutcome<T>.Rejected rejected:
                if (rejected.Operation is not null) {
                    RequireOperation(rejected.Operation);
                }
                await ObserveAttemptAsync(attempt, ProjectPendingAttemptOutcome.Rejected);
                break;
            case ProjectDefinitionOutcome<T>.Unconfirmed unconfirmed:
                RequireOperation(unconfirmed.Operation);
                await ObserveAttemptAsync(attempt, ProjectPendingAttemptOutcome.Unconfirmed);
                break;
            default:
                throw new InvalidOperationException("The definition outcome is unsupported.");
        }
    }

    private async Task ObserveAcceptedAsync(ProjectDefinitionAcceptedReceipt receipt) {
        RequireOperation(receipt.Operation);
        KnownAccepted ??= receipt;
        try {
            Apply(await store.ObserveAcceptedAsync(Frozen, receipt, CancellationToken.None));
        } catch (Exception failure) {
            FailStorage(failure);
            throw;
        }
    }

    private async Task ObserveAttemptAsync(Guid attempt, ProjectPendingAttemptOutcome outcome) {
        try {
            Apply(await store.ObserveAttemptAsync(Frozen.Submission.Operation, attempt, outcome, CancellationToken.None));
        } catch (Exception failure) {
            FailStorage(failure);
            throw;
        }
    }

    private void Apply(ProjectPendingStoreChange change) {
        if (change.Record is { } record) {
            RequireOperation(record.Operation);
            if (record.Frozen.Payload != Frozen.Payload) {
                throw new InvalidOperationException("The stored pending command differs from this session's frozen submission.");
            }
            if (Record is null || record.Revision.Value >= Record.Revision.Value) {
                Record = record;
            }
        } else if (change.Status == ProjectPendingStoreStatus.Applied) {
            Record = null;
        }
        if (reconciliationStarted && !IsReconciled) {
            return;
        }
        Problem = change.Status is ProjectPendingStoreStatus.Changed or ProjectPendingStoreStatus.Missing
            ? ProjectPendingProblem.RecordChanged
            : Record?.Stage == ProjectPendingStage.Unconfirmed ? ProjectPendingProblem.OutcomeUnconfirmed : ProjectPendingProblem.None;
    }

    private void RequireContext(ProjectDefinitionWriteContext context, DateTimeOffset now) {
        var operation = Frozen.Submission.Operation;
        if (context.Namespace != operation.Namespace || context.CallerBinding != operation.CallerBinding ||
            context.Epoch != operation.Epoch || now >= context.AdmissionDeadlineUtc ||
            !context.RequestVersions.Contains(Frozen.Submission.Version)) {
            Problem = ProjectPendingProblem.ContextChanged;
            throw new InvalidOperationException("The original pending operation is not admissible in the current write context.");
        }
    }

    private void RequireOperation(ProjectDefinitionOperation operation) {
        if (operation != Frozen.Submission.Operation) {
            throw new InvalidOperationException("The observation belongs to another pending operation.");
        }
    }

    private void FailStorage(Exception failure) {
        Problem = ProjectPendingProblem.StorageFailed;
        Failure = failure;
    }

    private void Enter() {
        ObjectDisposedException.ThrowIf(retired, this);
        if (Interlocked.CompareExchange(ref busy, 1, 0) != 0) {
            throw new InvalidOperationException("The pending operation already has an active action.");
        }
    }

    private void Exit() => Volatile.Write(ref busy, 0);
    public void Dispose() => retired = true;
}
