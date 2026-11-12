namespace CanDoItAll.Modules.Projects.Pages.Components;

public enum ProjectLifecyclePendingResultKind { Held, Accepted, Refused, Unconfirmed, ClosedAbsent }
public sealed record ProjectLifecyclePendingResult(ProjectLifecyclePendingResultKind Kind,
    ProjectLifecycleAccepted? Accepted, ProjectPendingProblem Problem);

public sealed class ProjectLifecyclePendingSession : IDisposable {
    private readonly IProjectLifecyclePendingStore store;
    private readonly IProjectLifecycleRecoveryCodec codec;
    private int busy;
    private bool retired;
    private bool reconciled;
    private bool preparationAllowed = true;

    public ProjectLifecyclePendingSession(IProjectLifecyclePendingStore store, IProjectLifecycleRecoveryCodec codec,
        ProjectLifecycleFrozenSubmission frozen) {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(codec);
        ArgumentNullException.ThrowIfNull(frozen);
        if (codec.Freeze(frozen.Submission).Payload != frozen.Payload) {
            throw new InvalidDataException("The lifecycle submission differs from its retained canonical bytes.");
        }
        this.store = store;
        this.codec = codec;
        Frozen = frozen;
    }

    public ProjectLifecycleFrozenSubmission Frozen { get; }
    public ProjectLifecyclePendingRecord? Record { get; private set; }
    public ProjectLifecycleAccepted? KnownAccepted { get; private set; }
    public ProjectLifecycleOutcome? LastOutcome { get; private set; }
    public ProjectLifecycleLookup? LastLookup { get; private set; }
    public ProjectPendingProblem Problem { get; private set; }
    public Exception? Failure { get; private set; }
    public bool NeedsStorageRepair { get; private set; }
    public bool HasOwnerAcknowledgement { get; private set; }
    public bool IsRetired => retired;
    public bool IsBusy => Volatile.Read(ref busy) != 0;
    public bool CanReconcile => !retired && !reconciled && !IsBusy && HasOwnerAcknowledgement && !NeedsStorageRepair && Problem == ProjectPendingProblem.None &&
        KnownAccepted is not null && Record?.Accepted?.Payload == codec.EncodeAccepted(Frozen.Submission, KnownAccepted).Payload;

    public static async Task RequireUnblockedAsync(IProjectLifecyclePendingStore store, ProjectLifecyclePendingScope scope,
        ProjectLifecyclePendingStoreLimits limits, IReadOnlyCollection<Guid> targets, CancellationToken cancellationToken = default) {
        await store.InitializeAsync(cancellationToken);
        var seen = new HashSet<ProjectLifecycleOperation>();
        ProjectLifecycleOperation? after = null;
        do {
            var page = await store.ListAsync(scope, limits.MaximumPageSize, after, cancellationToken);
            foreach (var row in page.Items) {
                if (!scope.Contains(row.Operation) || !seen.Add(row.Operation) || seen.Count > limits.MaximumRecordCount) {
                    throw new InvalidDataException("The retained lifecycle index does not match its bounded original scope.");
                }
                if (row.Stage != ProjectPendingStage.ClosedAbsent && row.TargetProjectIds.Any(targets.Contains)) {
                    throw new InvalidOperationException($"Review retained operation {row.Operation.Id:D} in Projects before another write to this hierarchy.");
                }
            }
            if (page.Next is { } next && (page.Items.Count == 0 || page.Items[^1].Operation != next || !scope.Contains(next))) {
                throw new InvalidDataException("The retained lifecycle index did not advance within its original scope.");
            }
            after = page.Next;
        } while (after is not null);
    }

    public async Task<ProjectLifecyclePendingResult> PrepareAsync(CancellationToken cancellationToken = default) {
        Enter();
        try {
            if (!preparationAllowed) {
                throw new InvalidOperationException("A recovered or entered lifecycle operation cannot be prepared again after evidence loss.");
            }
            await StoreAsync(() => store.PrepareAsync(Frozen, cancellationToken));
            return Result();
        } finally {
            Exit();
        }
    }

    public async Task<ProjectLifecyclePendingResult> RestoreAsync(CancellationToken cancellationToken = default) {
        Enter();
        try {
            preparationAllowed = false;
            try {
                var record = await store.ReadAsync(Frozen.Submission.Operation, cancellationToken);
                Retain(new(record is null ? ProjectPendingStoreStatus.Missing : ProjectPendingStoreStatus.Unchanged, record));
            } catch (Exception failure) {
                StorageFailed(failure);
            }
            return Result();
        } finally {
            Exit();
        }
    }

    public async Task<ProjectLifecyclePendingResult> DispatchAsync(ProjectLifecycleWriteContext context, DateTimeOffset now,
        bool explicitRetry, Func<ProjectLifecycleSubmission, CancellationToken, Task<ProjectLifecycleOutcome>> dispatch,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(dispatch);
        Enter();
        try {
            var operation = Frozen.Submission.Operation;
            if (KnownAccepted is not null) {
                return Result();
            }
            if (context.DatabaseProfileId != operation.DatabaseProfileId || context.Namespace != operation.Namespace ||
                    context.CallerBinding != operation.CallerBinding) {
                Problem = ProjectPendingProblem.ContextChanged;
                return Result();
            }
            var retry = explicitRetry && LastLookup is ProjectLifecycleLookup.NotObserved { State: ProjectOperationObservationState.Open } observed &&
                observed.Original == operation && Record?.Stage is ProjectPendingStage.Dispatching or ProjectPendingStage.Unconfirmed;
            var first = !explicitRetry && Record?.Stage == ProjectPendingStage.Prepared && context.Epoch == operation.Epoch &&
                now.Offset == TimeSpan.Zero && now < context.AdmissionClosesAtUtc;
            if (NeedsStorageRepair || Record is null || !first && !retry) {
                return Result();
            }
            LastLookup = null;
            preparationAllowed = false;
            var attempt = Guid.NewGuid();
            var claim = await StoreAsync(() => store.BeginDispatchAsync(Record, attempt, explicitRetry, cancellationToken));
            if (KnownAccepted is not null || claim?.Status != ProjectPendingStoreStatus.Applied || retired) {
                return Result();
            }
            if (Record is not { Stage: ProjectPendingStage.Dispatching } entered || entered.AttemptId != attempt) {
                StorageFailed(new InvalidDataException("The retained claim does not acknowledge this exact lifecycle dispatch."));
                return Result();
            }
            cancellationToken.ThrowIfCancellationRequested();
            ProjectLifecycleOutcome outcome;
            try {
                outcome = await dispatch(Frozen.Submission, cancellationToken);
            } catch (Exception failure) {
                Failure = failure;
                Problem = ProjectPendingProblem.OutcomeUnconfirmed;
                await StoreAsync(() => store.ObserveAttemptAsync(operation, attempt, ProjectPendingAttemptOutcome.Unconfirmed, CancellationToken.None));
                return Result();
            }
            LastOutcome = outcome;
            switch (outcome) {
                case ProjectLifecycleOutcome.Accepted accepted:
                    await AcceptAsync(accepted.Result);
                    break;
                case ProjectLifecycleOutcome.Refused refused when refused.Original == Frozen.Submission:
                    await StoreAsync(() => store.ObserveAttemptAsync(operation, attempt, ProjectPendingAttemptOutcome.Unconfirmed, CancellationToken.None));
                    break;
                case ProjectLifecycleOutcome.Unconfirmed unconfirmed when unconfirmed.Original == Frozen.Submission:
                    await StoreAsync(() => store.ObserveAttemptAsync(operation, attempt, ProjectPendingAttemptOutcome.Unconfirmed, CancellationToken.None));
                    break;
                default:
                    Failure = new InvalidDataException("The lifecycle outcome does not match the retained original submission.");
                    Problem = ProjectPendingProblem.OutcomeUnconfirmed;
                    await StoreAsync(() => store.ObserveAttemptAsync(operation, attempt, ProjectPendingAttemptOutcome.Unconfirmed, CancellationToken.None));
                    break;
            }
            return Result();
        } finally {
            Exit();
        }
    }

    public async Task<ProjectLifecyclePendingResult> LookupAsync(
        Func<ProjectLifecycleOperation, CancellationToken, Task<ProjectLifecycleLookup>> lookup, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(lookup);
        Enter();
        try {
            ProjectLifecycleLookup observed;
            try {
                observed = await lookup(Frozen.Submission.Operation, cancellationToken);
            } catch (Exception failure) {
                RetainFailure(failure);
                Problem = NeedsStorageRepair ? ProjectPendingProblem.StorageFailed : ProjectPendingProblem.RecoveryUnavailable;
                return Result();
            }
            var operation = observed switch {
                ProjectLifecycleLookup.Found found => found.Accepted.Original.Operation,
                ProjectLifecycleLookup.NotObserved value => value.Original,
                ProjectLifecycleLookup.ClosedAbsent value => value.Original,
                ProjectLifecycleLookup.Expired value => value.Original,
                ProjectLifecycleLookup.UnknownFutureEpoch value => value.Original,
                ProjectLifecycleLookup.ContextChanged value => value.Original,
                ProjectLifecycleLookup.Unavailable value => value.Original,
                _ => throw new InvalidDataException("The lifecycle lookup variant is unsupported.")
            };
            if (operation != Frozen.Submission.Operation) {
                throw new InvalidDataException("The lifecycle lookup belongs to another original operation.");
            }
            LastLookup = observed;
            switch (observed) {
                case ProjectLifecycleLookup.Found found:
                    await AcceptAsync(found.Accepted);
                    break;
                case ProjectLifecycleLookup.ClosedAbsent absence:
                    codec.EncodeClosedAbsent(Frozen.Submission, absence);
                    await StoreAsync(() => store.ObserveClosedAbsentAsync(Frozen, absence, cancellationToken));
                    break;
                case ProjectLifecycleLookup.NotObserved:
                    if (!NeedsStorageRepair) {
                        Problem = KnownAccepted is null ? ProjectPendingProblem.OutcomeUnconfirmed : ProjectPendingProblem.None;
                    }
                    break;
                default:
                    if (!NeedsStorageRepair) {
                        Problem = observed is ProjectLifecycleLookup.ContextChanged ? ProjectPendingProblem.ContextChanged : ProjectPendingProblem.RecoveryUnavailable;
                    }
                    break;
            }
            return Result();
        } finally {
            Exit();
        }
    }

    private async Task AcceptAsync(ProjectLifecycleAccepted accepted) {
        var encoded = codec.EncodeAccepted(Frozen.Submission, accepted);
        if (KnownAccepted is not null && codec.EncodeAccepted(Frozen.Submission, KnownAccepted).Payload != encoded.Payload) {
            throw new InvalidDataException("Conflicting canonical lifecycle acknowledgements cannot replace retained acceptance.");
        }
        KnownAccepted ??= accepted;
        HasOwnerAcknowledgement = true;
        await StoreAsync(() => store.ObserveAcceptedAsync(Frozen, accepted, CancellationToken.None));
    }

    public void Reconcile(Action<ProjectLifecycleAccepted> applyToOriginalTarget) {
        ArgumentNullException.ThrowIfNull(applyToOriginalTarget);
        if (!CanReconcile) {
            throw new InvalidOperationException("This lifecycle acknowledgement cannot reconcile an active original target.");
        }
        reconciled = true;
        try {
            applyToOriginalTarget(KnownAccepted!);
        } catch (Exception failure) {
            Failure = failure;
            Problem = ProjectPendingProblem.ReconciliationFailed;
            throw;
        }
    }

    private async Task<ProjectLifecyclePendingChange?> StoreAsync(Func<ValueTask<ProjectLifecyclePendingChange>> action) {
        try {
            var change = await action();
            Retain(change);
            return change;
        } catch (Exception failure) {
            StorageFailed(failure);
            return null;
        }
    }

    private void Retain(ProjectLifecyclePendingChange change) {
        if (change.Record is not { } record) {
            StorageFailed(new InvalidDataException("The original lifecycle operation is missing from browser storage."));
            return;
        }
        if (record.Operation != Frozen.Submission.Operation || record.Frozen.Payload != Frozen.Payload || record.Frozen.Submission != Frozen.Submission) {
            throw new InvalidDataException("The retained lifecycle record does not match its original command and address.");
        }
        ProjectLifecycleAccepted? retainedAccepted = null;
        if (record.Accepted is { } accepted) {
            var verified = codec.ReadAccepted(Frozen.Submission, accepted.Payload);
            if (codec.EncodeAccepted(Frozen.Submission, accepted.Accepted).Payload != verified.Payload || KnownAccepted is not null &&
                    codec.EncodeAccepted(Frozen.Submission, KnownAccepted).Payload != verified.Payload) {
                throw new InvalidDataException("The retained lifecycle acknowledgement is inconsistent.");
            }
            retainedAccepted = verified.Accepted;
        }
        if (record.Absence is { } absence && codec.ReadClosedAbsent(Frozen.Submission, absence.Payload).Payload !=
                codec.EncodeClosedAbsent(Frozen.Submission, absence.Absence).Payload) {
            throw new InvalidDataException("The retained lifecycle absence evidence is inconsistent.");
        }
        if (!Enum.IsDefined(change.Status) || !Enum.IsDefined(record.Stage) ||
                (record.Stage == ProjectPendingStage.Accepted) != (record.Accepted is not null) ||
                (record.Stage == ProjectPendingStage.ClosedAbsent) != (record.Absence is not null) ||
                record.Stage == ProjectPendingStage.Prepared && (record.AttemptId is not null || record.HadUnconfirmedDispatch) ||
                record.Stage is ProjectPendingStage.Dispatching or ProjectPendingStage.Unconfirmed or ProjectPendingStage.Rejected && record.AttemptId is null ||
                record.AttemptId == Guid.Empty || record.Stage == ProjectPendingStage.Unconfirmed && !record.HadUnconfirmedDispatch ||
                record.Stage == ProjectPendingStage.Rejected && record.HadUnconfirmedDispatch) {
            throw new InvalidDataException("The retained lifecycle record has inconsistent protocol state.");
        }
        if (Record is { } previous && previous.Revision == record.Revision && !SameState(previous, record)) {
            throw new InvalidDataException("A lifecycle record revision cannot describe two different states.");
        }
        KnownAccepted ??= retainedAccepted;
        if (Record is null || record.Revision.Value > Record.Revision.Value) {
            Record = record;
        }
        if (change.Status is ProjectPendingStoreStatus.Applied or ProjectPendingStoreStatus.Unchanged && Record is { } current &&
                current.Revision == record.Revision && SameState(current, record) &&
                (KnownAccepted is null || current.Accepted?.Payload == codec.EncodeAccepted(Frozen.Submission, KnownAccepted).Payload)) {
            NeedsStorageRepair = false;
        }
        if (KnownAccepted is not null && Record?.Accepted?.Payload != codec.EncodeAccepted(Frozen.Submission, KnownAccepted).Payload) {
            NeedsStorageRepair = true;
        }
        if (NeedsStorageRepair) {
            Problem = ProjectPendingProblem.StorageFailed;
            return;
        }
        Problem = change.Status is ProjectPendingStoreStatus.Missing or ProjectPendingStoreStatus.Changed
            ? ProjectPendingProblem.RecordChanged
            : KnownAccepted is not null && Record?.Accepted is null ? ProjectPendingProblem.StorageFailed
            : Record?.Stage is ProjectPendingStage.Dispatching or ProjectPendingStage.Unconfirmed
                ? ProjectPendingProblem.OutcomeUnconfirmed : ProjectPendingProblem.None;
    }

    private static bool SameState(ProjectLifecyclePendingRecord first, ProjectLifecyclePendingRecord second)
        => first.Stage == second.Stage && first.AttemptId == second.AttemptId && first.HadUnconfirmedDispatch == second.HadUnconfirmedDispatch &&
            first.Accepted?.Payload == second.Accepted?.Payload && first.Absence?.Payload == second.Absence?.Payload;

    private ProjectLifecyclePendingResult Result() => new(KnownAccepted is not null ? ProjectLifecyclePendingResultKind.Accepted
        : Record?.Stage == ProjectPendingStage.ClosedAbsent ? ProjectLifecyclePendingResultKind.ClosedAbsent
        : Record?.Stage is ProjectPendingStage.Dispatching or ProjectPendingStage.Unconfirmed or ProjectPendingStage.Rejected ? ProjectLifecyclePendingResultKind.Unconfirmed
        : ProjectLifecyclePendingResultKind.Held, KnownAccepted, Problem);

    private void StorageFailed(Exception failure) {
        RetainFailure(failure);
        NeedsStorageRepair = true;
        Problem = ProjectPendingProblem.StorageFailed;
    }

    private void RetainFailure(Exception failure) {
        Failure = Failure is null || ReferenceEquals(Failure, failure) ? failure
            : new AggregateException("Lifecycle operation observation and evidence retention encountered failures.", Failure, failure);
    }

    private void Enter() {
        ObjectDisposedException.ThrowIf(retired, this);
        if (Interlocked.CompareExchange(ref busy, 1, 0) != 0) {
            throw new InvalidOperationException("This original lifecycle operation already has an active action.");
        }
    }

    private void Exit() => Volatile.Write(ref busy, 0);
    public void Dispose() => retired = true;
}
