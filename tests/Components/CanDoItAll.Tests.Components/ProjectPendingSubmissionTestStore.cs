using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Projects.Pages.Components;

namespace CanDoItAll.Tests.Components;

internal sealed class ProjectPendingSubmissionTestStore : IProjectPendingSubmissionStore, IProjectPendingReferenceStore {
    private readonly Dictionary<ProjectDefinitionOperation, ProjectPendingSubmission> records = [];
    private long revision;
    public Func<Task>? BeforeAccept { get; set; }
    public Exception? AcceptFailure { get; set; }
    public Exception? ScopedListFailure { get; set; }
    public IReadOnlyCollection<ProjectPendingSubmission> Records => records.Values;
    public int DispatchClaims { get; private set; }

    public ValueTask InitializeAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

    public ValueTask<ProjectPendingStoreChange> PrepareAsync(ProjectDefinitionFrozenSubmission frozen, bool hadStarterPlan, CancellationToken cancellationToken = default) {
        if (records.TryGetValue(frozen.Submission.Operation, out var current)) {
            if (current.Frozen.Payload != frozen.Payload || current.HadStarterPlan != hadStarterPlan) {
                throw new InvalidOperationException("The test store received incompatible preparation.");
            }
            return ValueTask.FromResult(new ProjectPendingStoreChange(ProjectPendingStoreStatus.Unchanged, current));
        }
        return Change(new(frozen, Next(), ProjectPendingStage.Prepared, null, false, hadStarterPlan, null, null));
    }

    public ValueTask<ProjectPendingStoreChange> BeginDispatchAsync(ProjectPendingSubmission record, Guid attemptId, bool explicitRetry, CancellationToken cancellationToken = default) {
        if (!Matches(record, out var current)) {
            return ValueTask.FromResult(new ProjectPendingStoreChange(ProjectPendingStoreStatus.Changed, current));
        }
        if (current!.Stage != ProjectPendingStage.Prepared && !(explicitRetry && current.Stage is ProjectPendingStage.Dispatching or ProjectPendingStage.Unconfirmed)) {
            throw new InvalidOperationException("The test store cannot dispatch this stage.");
        }
        DispatchClaims++;
        return Change(current with { Revision = Next(), Stage = ProjectPendingStage.Dispatching, AttemptId = attemptId,
            HadUnconfirmedDispatch = current.HadUnconfirmedDispatch || current.Stage != ProjectPendingStage.Prepared });
    }

    public async ValueTask<ProjectPendingStoreChange> ObserveAcceptedAsync(ProjectDefinitionFrozenSubmission frozen, ProjectDefinitionAcceptedReceipt receipt, CancellationToken cancellationToken = default) {
        if (BeforeAccept is not null) {
            await BeforeAccept();
        }
        if (AcceptFailure is not null) {
            throw AcceptFailure;
        }
        var current = records[frozen.Submission.Operation];
        return await Change(current with { Revision = Next(), Stage = ProjectPendingStage.Accepted, Accepted = new(receipt, "test-only accepted payload") });
    }

    public ValueTask<ProjectPendingStoreChange> ObserveAttemptAsync(ProjectDefinitionOperation operation, Guid attemptId, ProjectPendingAttemptOutcome outcome, CancellationToken cancellationToken = default) {
        var current = records[operation];
        if (current.Stage == ProjectPendingStage.Accepted || current.AttemptId != attemptId) {
            return ValueTask.FromResult(new ProjectPendingStoreChange(ProjectPendingStoreStatus.Unchanged, current));
        }
        var uncertain = current.HadUnconfirmedDispatch || outcome == ProjectPendingAttemptOutcome.Unconfirmed;
        return Change(current with { Revision = Next(), Stage = uncertain ? ProjectPendingStage.Unconfirmed : ProjectPendingStage.Rejected,
            HadUnconfirmedDispatch = uncertain });
    }

    public ValueTask<ProjectPendingStoreChange> ObserveClosedAbsentAsync(ProjectDefinitionFrozenSubmission frozen, ProjectDefinitionLookup.ClosedAbsent absence, CancellationToken cancellationToken = default) {
        var current = records[frozen.Submission.Operation];
        return current.Stage == ProjectPendingStage.Accepted
            ? ValueTask.FromResult(new ProjectPendingStoreChange(ProjectPendingStoreStatus.Unchanged, current))
            : Change(current with { Revision = Next(), Stage = ProjectPendingStage.ClosedAbsent, Absence = absence });
    }

    public ValueTask<ProjectPendingSubmission?> ReadAsync(ProjectDefinitionOperation operation, CancellationToken cancellationToken = default)
        => ValueTask.FromResult(records.GetValueOrDefault(operation));

    public ValueTask<ProjectPendingSubmissionPage> ListAsync(ProjectPendingScope scope, int take, ProjectDefinitionOperation? after = null, CancellationToken cancellationToken = default) {
        if (ScopedListFailure is not null) {
            throw ScopedListFailure;
        }
        var ordered = records.Values.Where(row => scope.Contains(row.Operation)).OrderBy(row => row.Operation.Id).ToArray();
        var offset = after is null ? 0 : Array.FindIndex(ordered, row => row.Operation == after) + 1;
        var page = ordered.Skip(offset).Take(take).ToArray();
        return ValueTask.FromResult(new ProjectPendingSubmissionPage(page.Select(row => new ProjectPendingSubmissionMetadata(row.Operation,
            row.Revision, row.Stage, row.Frozen.Submission switch {
                ProjectDefinitionSubmission.Replace replace => replace.Request.Command.Expected.Project.ProjectId,
                ProjectDefinitionSubmission.Scalars scalar => scalar.Request.Command.Expected.Project.ProjectId,
                _ => null
            }, row.HadUnconfirmedDispatch, row.HadStarterPlan)).ToArray(), offset + page.Length < ordered.Length ? page[^1].Operation : null));
    }

    public ValueTask<ProjectPendingStoreChange> CancelPreparedAsync(ProjectPendingSubmission record, CancellationToken cancellationToken = default)
        => Clear(record, current => current.Stage == ProjectPendingStage.Prepared);

    public ValueTask<ProjectPendingStoreChange> ClearResolvedAsync(ProjectPendingSubmission record, CancellationToken cancellationToken = default)
        => Clear(record, current => current.Stage is ProjectPendingStage.Accepted or ProjectPendingStage.Rejected or ProjectPendingStage.ClosedAbsent);

    public ValueTask<ProjectPendingReferencePage> ListReferencesAsync(int take, ProjectDefinitionOperation? after = null, CancellationToken cancellationToken = default) {
        var ordered = records.Values.OrderBy(row => row.Operation.Id).ToArray();
        var offset = after is null ? 0 : Array.FindIndex(ordered, row => row.Operation == after) + 1;
        var items = ordered.Skip(offset).Take(take).Select(Reference).ToArray();
        return ValueTask.FromResult(new ProjectPendingReferencePage(items, offset + items.Length < ordered.Length ? items[^1].Operation : null,
            ordered.Length, ordered.Sum(row => Reference(row).StoredBytes)));
    }

    public ValueTask<ProjectPendingReferenceChange> DiscardEvidenceAsync(ProjectPendingReference reference, CancellationToken cancellationToken = default) {
        if (!records.TryGetValue(reference.Operation, out var current)) {
            return ValueTask.FromResult(new ProjectPendingReferenceChange(ProjectPendingStoreStatus.Missing, null));
        }
        if (current.Revision != reference.Revision) {
            return ValueTask.FromResult(new ProjectPendingReferenceChange(ProjectPendingStoreStatus.Changed, Reference(current)));
        }
        records.Remove(reference.Operation);
        return ValueTask.FromResult(new ProjectPendingReferenceChange(ProjectPendingStoreStatus.Applied, null));
    }

    private static ProjectPendingReference Reference(ProjectPendingSubmission row) => new(row.Operation, row.Revision, row.Stage,
        row.Frozen.Submission.TargetProjectId, row.HadUnconfirmedDispatch, row.HadStarterPlan,
        System.Text.Encoding.UTF8.GetByteCount(row.Frozen.Payload) + System.Text.Encoding.UTF8.GetByteCount(row.Accepted?.Payload ?? string.Empty));

    private ValueTask<ProjectPendingStoreChange> Clear(ProjectPendingSubmission record, Func<ProjectPendingSubmission, bool> allowed) {
        if (!Matches(record, out var current)) {
            return ValueTask.FromResult(new ProjectPendingStoreChange(ProjectPendingStoreStatus.Changed, current));
        }
        if (!allowed(current!)) {
            throw new InvalidOperationException("Unresolved evidence cannot be cleared.");
        }
        records.Remove(record.Operation);
        return ValueTask.FromResult(new ProjectPendingStoreChange(ProjectPendingStoreStatus.Applied, null));
    }

    private bool Matches(ProjectPendingSubmission record, out ProjectPendingSubmission? current)
        => records.TryGetValue(record.Operation, out current) && current.Revision == record.Revision;

    private ProjectPendingRevision Next() => new(++revision);

    private ValueTask<ProjectPendingStoreChange> Change(ProjectPendingSubmission record) {
        records[record.Operation] = record;
        return ValueTask.FromResult(new ProjectPendingStoreChange(ProjectPendingStoreStatus.Applied, record));
    }
}
