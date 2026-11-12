using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Projects.Pages.Components;

namespace CanDoItAll.Tests.Components;

internal sealed class ProjectLifecyclePendingTestStore : IProjectLifecyclePendingStore {
    private readonly Dictionary<ProjectLifecycleOperation, ProjectLifecyclePendingRecord> records = [];
    private readonly ProjectLifecycleRecoveryCodec codec = new();
    private long revision;
    public Func<Task>? BeforeAccept { get; set; }
    public Exception? AcceptFailure { get; set; }
    public Exception? ClearFailure { get; set; }
    public IReadOnlyCollection<ProjectLifecyclePendingRecord> Records => records.Values;
    public int DispatchClaims { get; private set; }

    public ValueTask InitializeAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

    public ValueTask<ProjectLifecyclePendingChange> PrepareAsync(ProjectLifecycleFrozenSubmission frozen, CancellationToken cancellationToken = default) {
        if (records.TryGetValue(frozen.Submission.Operation, out var current)) {
            RequireOriginal(current, frozen);
            return ValueTask.FromResult(new ProjectLifecyclePendingChange(ProjectPendingStoreStatus.Unchanged, current));
        }
        if (codec.Freeze(frozen.Submission).Payload != frozen.Payload) {
            throw new InvalidDataException("The fixture requires exact canonical lifecycle evidence.");
        }
        return Change(new(frozen, Next(), ProjectPendingStage.Prepared, null, false, null, null));
    }

    public ValueTask<ProjectLifecyclePendingChange> BeginDispatchAsync(ProjectLifecyclePendingRecord original, Guid attemptId,
        bool explicitRetry, CancellationToken cancellationToken = default) {
        if (!Matches(original, out var current)) {
            return ValueTask.FromResult(new ProjectLifecyclePendingChange(ProjectPendingStoreStatus.Changed, current));
        }
        if (attemptId == Guid.Empty || current!.Stage != ProjectPendingStage.Prepared &&
                !(explicitRetry && current.Stage is ProjectPendingStage.Dispatching or ProjectPendingStage.Unconfirmed)) {
            throw new InvalidOperationException("The fixture refuses an inadmissible dispatch claim.");
        }
        DispatchClaims++;
        return Change(current with { Revision = Next(), Stage = ProjectPendingStage.Dispatching, AttemptId = attemptId,
            HadUnconfirmedDispatch = current.HadUnconfirmedDispatch || current.Stage != ProjectPendingStage.Prepared });
    }

    public async ValueTask<ProjectLifecyclePendingChange> ObserveAcceptedAsync(ProjectLifecycleFrozenSubmission original,
        ProjectLifecycleAccepted accepted, CancellationToken cancellationToken = default) {
        var encoded = codec.EncodeAccepted(original.Submission, accepted);
        if (BeforeAccept is not null) {
            await BeforeAccept();
        }
        if (AcceptFailure is not null) {
            throw AcceptFailure;
        }
        var current = records[original.Submission.Operation];
        RequireOriginal(current, original);
        if (current.Accepted is { } retained && retained.Payload != encoded.Payload) {
            throw new InvalidDataException("The fixture refuses contradictory accepted evidence.");
        }
        return await Change(current with { Revision = Next(), Stage = ProjectPendingStage.Accepted, Accepted = encoded, Absence = null });
    }

    public ValueTask<ProjectLifecyclePendingChange> ObserveAttemptAsync(ProjectLifecycleOperation original, Guid attemptId,
        ProjectPendingAttemptOutcome outcome, CancellationToken cancellationToken = default) {
        var current = records[original];
        if (current.Stage == ProjectPendingStage.Accepted || current.AttemptId != attemptId) {
            return ValueTask.FromResult(new ProjectLifecyclePendingChange(ProjectPendingStoreStatus.Unchanged, current));
        }
        return Change(current with { Revision = Next(), Stage = ProjectPendingStage.Unconfirmed, HadUnconfirmedDispatch = true });
    }

    public ValueTask<ProjectLifecyclePendingChange> ObserveClosedAbsentAsync(ProjectLifecycleFrozenSubmission original,
        ProjectLifecycleLookup.ClosedAbsent absence, CancellationToken cancellationToken = default) {
        var encoded = codec.EncodeClosedAbsent(original.Submission, absence);
        var current = records[original.Submission.Operation];
        RequireOriginal(current, original);
        return current.Stage == ProjectPendingStage.Accepted
            ? ValueTask.FromResult(new ProjectLifecyclePendingChange(ProjectPendingStoreStatus.Unchanged, current))
            : Change(current with { Revision = Next(), Stage = ProjectPendingStage.ClosedAbsent, Absence = encoded });
    }

    public ValueTask<ProjectLifecyclePendingRecord?> ReadAsync(ProjectLifecycleOperation original, CancellationToken cancellationToken = default)
        => ValueTask.FromResult(records.GetValueOrDefault(original));

    public ValueTask<ProjectLifecyclePendingPage> ListAsync(ProjectLifecyclePendingScope scope, int take,
        ProjectLifecycleOperation? after = null, CancellationToken cancellationToken = default) {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(take);
        var ordered = records.Values.Where(row => scope.Contains(row.Operation)).OrderBy(row => row.Operation.Id).ToArray();
        var offset = after is null ? 0 : Array.FindIndex(ordered, row => row.Operation == after) + 1;
        if (after is not null && offset == 0) {
            throw new InvalidOperationException("The fixture pagination cursor is missing.");
        }
        var page = ordered.Skip(offset).Take(take).ToArray();
        return ValueTask.FromResult(new ProjectLifecyclePendingPage(page.Select(row => new ProjectLifecyclePendingMetadata(row.Operation,
            row.Revision, row.Stage, row.Frozen.Submission.Command.Kind, ProjectLifecyclePendingTargets.Capture(row.Frozen.Submission.Command),
            row.HadUnconfirmedDispatch)).ToArray(), offset + page.Length < ordered.Length ? page[^1].Operation : null));
    }

    public ValueTask<ProjectLifecyclePendingChange> CancelPreparedAsync(ProjectLifecyclePendingRecord original, CancellationToken cancellationToken = default)
        => Clear(original, current => current.Stage == ProjectPendingStage.Prepared);

    public ValueTask<ProjectLifecyclePendingChange> ClearResolvedAsync(ProjectLifecyclePendingRecord original, CancellationToken cancellationToken = default)
        => ClearFailure is { } failure ? throw failure : Clear(original, current => current.Stage is ProjectPendingStage.Accepted or ProjectPendingStage.ClosedAbsent);

    private ValueTask<ProjectLifecyclePendingChange> Clear(ProjectLifecyclePendingRecord original, Func<ProjectLifecyclePendingRecord, bool> allowed) {
        if (!Matches(original, out var current)) {
            return ValueTask.FromResult(new ProjectLifecyclePendingChange(ProjectPendingStoreStatus.Changed, current));
        }
        if (!allowed(current!)) {
            throw new InvalidOperationException("Unresolved lifecycle evidence cannot be cleared.");
        }
        records.Remove(original.Operation);
        return ValueTask.FromResult(new ProjectLifecyclePendingChange(ProjectPendingStoreStatus.Applied, null));
    }

    private bool Matches(ProjectLifecyclePendingRecord original, out ProjectLifecyclePendingRecord? current)
        => records.TryGetValue(original.Operation, out current) && current.Revision == original.Revision && current.Frozen.Payload == original.Frozen.Payload;

    private static void RequireOriginal(ProjectLifecyclePendingRecord current, ProjectLifecycleFrozenSubmission original) {
        if (current.Frozen.Payload != original.Payload) {
            throw new InvalidDataException("The fixture cannot replace an original lifecycle submission.");
        }
    }

    private ProjectPendingRevision Next() => new(++revision);

    private ValueTask<ProjectLifecyclePendingChange> Change(ProjectLifecyclePendingRecord record) {
        records[record.Operation] = record;
        return ValueTask.FromResult(new ProjectLifecyclePendingChange(ProjectPendingStoreStatus.Applied, record));
    }
}
