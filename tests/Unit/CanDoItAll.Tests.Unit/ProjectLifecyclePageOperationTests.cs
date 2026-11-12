using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Projects.Pages;
using CanDoItAll.Modules.Projects.Pages.Components;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Tests.Unit;

public sealed class ProjectLifecyclePageOperationTests {
    private static readonly ProjectLifecycleRecoveryCodec Codec = new();
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Accepted_deletion_can_close_only_the_original_displayed_editor_once() {
        var (draft, frozen, accepted) = Original();
        var store = new Store(frozen);
        using var operation = new ProjectLifecyclePageOperation(new(store, Codec, frozen), draft, 7);
        await operation.Pending.PrepareAsync();
        await operation.Pending.LookupAsync((_, _) => Task.FromResult<ProjectLifecycleLookup>(new ProjectLifecycleLookup.Found(accepted)));
        var successor = new ProjectEditorDraft(new() { Id = draft.Model.Id, ExpectedDefinition = draft.Model.ExpectedDefinition, Name = "New editor text" });
        var applies = 0;
        void Apply(ProjectEditorDraft target, long intent, ProjectLifecycleAccepted result) {
            applies++;
            Assert.Same(draft, target);
            Assert.Equal(7, intent);
            Assert.Equal(Codec.EncodeAccepted(frozen.Submission, accepted).Payload, Codec.EncodeAccepted(frozen.Submission, result).Payload);
        }
        Assert.False(operation.TryApply(successor, 7, true, Apply));
        Assert.False(operation.TryApply(draft, 8, true, Apply));
        Assert.False(operation.TryApply(draft, 7, false, Apply));
        Assert.Equal("New editor text", successor.Model.Name);
        Assert.Equal(0, applies);
        Assert.True(operation.TryApply(draft, 7, true, Apply));
        Assert.False(operation.TryApply(draft, 7, true, Apply));
        Assert.Equal(1, applies);
        Assert.Same(accepted, operation.Pending.KnownAccepted);
    }

    [Fact]
    public async Task Editor_retirement_keeps_late_acceptance_and_later_typing_without_retiring_the_successor() {
        var (draft, frozen, accepted) = Original();
        var store = new Store(frozen);
        using var operation = new ProjectLifecyclePageOperation(new(store, Codec, frozen), draft, 11);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finish = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<ProjectLifecyclePendingResult>? pending = null;
        Exception? primary = null;
        var ownerCalls = 0;
        try {
            await operation.Pending.PrepareAsync();
            pending = operation.Pending.DispatchAsync(Context(frozen), Now, false, async (original, _) => {
                ownerCalls++;
                Assert.Equal(frozen.Submission, original);
                entered.SetResult();
                await finish.Task;
                return new ProjectLifecycleOutcome.Accepted(accepted, false, null);
            }, operation.DispatchLifetime.Token);
            await entered.Task;
            draft.Model.Name = "Later text retained in original draft";
            operation.RetireEditor();
            var successor = new ProjectEditorDraft(new() { Id = draft.Model.Id, ExpectedDefinition = draft.Model.ExpectedDefinition, Name = "Successor draft" });
            finish.SetResult();
            var outcome = await pending;
            Assert.Equal(ProjectLifecyclePendingResultKind.Accepted, outcome.Kind);
            Assert.Same(accepted, outcome.Accepted);
            Assert.False(operation.TryApply(successor, 12, true, (_, _, _) => throw new InvalidOperationException("A successor must never be retired.")));
            Assert.False(operation.TryApply(draft, 11, true, (_, _, _) => throw new InvalidOperationException("The retired editor must not reopen.")));
            Assert.Equal("Successor draft", successor.Model.Name);
            Assert.Equal("Later text retained in original draft", draft.Model.Name);
            Assert.True(operation.DispatchLifetime.IsCancellationRequested);
            Assert.Equal(1, ownerCalls);
            Assert.Equal(1, store.Acceptances);
            Assert.Equal(ProjectPendingStage.Accepted, store.Record!.Stage);
        } catch (Exception failure) {
            primary = failure;
            throw;
        } finally {
            finish.TrySetResult();
            if (pending is not null) {
                try {
                    await pending;
                } catch (Exception terminal) when (!ReferenceEquals(primary, terminal)) {
                    if (primary is not null) {
                        throw new AggregateException("The lifecycle lifetime proof and terminal owner task both failed.", primary, terminal);
                    }
                    throw;
                } catch (Exception terminal) when (ReferenceEquals(primary, terminal)) {
                }
            }
        }
    }

    [Fact]
    public async Task Known_acceptance_requires_exact_storage_repair_and_reconciliation_failure_never_replays_it() {
        var (draft, frozen, accepted) = Original();
        var store = new Store(frozen) { FailAcceptance = true };
        using var operation = new ProjectLifecyclePageOperation(new(store, Codec, frozen), draft, 3);
        await operation.Pending.PrepareAsync();
        Task<ProjectLifecycleLookup> Lookup(ProjectLifecycleOperation original, CancellationToken _) {
            Assert.Equal(frozen.Submission.Operation, original);
            return Task.FromResult<ProjectLifecycleLookup>(new ProjectLifecycleLookup.Found(accepted));
        }
        await operation.Pending.LookupAsync(Lookup);
        Assert.Same(accepted, operation.Pending.KnownAccepted);
        Assert.True(operation.Pending.NeedsStorageRepair);
        Assert.False(operation.CanApply(draft, 3, true));
        store.FailAcceptance = false;
        await operation.Pending.LookupAsync(Lookup);
        Assert.False(operation.Pending.NeedsStorageRepair);
        var originalExpectation = draft.Model.ExpectedDefinition!;
        draft.Model.ExpectedDefinition = new(originalExpectation.Project, Guid.NewGuid());
        Assert.False(operation.CanApply(draft, 3, true));
        draft.Model.ExpectedDefinition = originalExpectation;
        var applies = 0;
        var secondary = new InvalidOperationException("Original editor reconciliation failed.");
        Assert.Same(secondary, Assert.Throws<InvalidOperationException>(() => operation.TryApply(draft, 3, true, (_, _, _) => {
            applies++;
            throw secondary;
        })));
        Assert.True(operation.Applied);
        Assert.Same(accepted, operation.Pending.KnownAccepted);
        Assert.Equal(ProjectPendingProblem.ReconciliationFailed, operation.Pending.Problem);
        Assert.False(operation.TryApply(draft, 3, true, (_, _, _) => applies++));
        Assert.Equal(1, applies);
        Assert.Equal(0, store.Dispatches);
    }

    private static (ProjectEditorDraft Draft, ProjectLifecycleFrozenSubmission Frozen, ProjectLifecycleAccepted Accepted) Original() {
        var profile = Guid.NewGuid();
        var space = new DatasetNamespaceId(Guid.NewGuid());
        var admission = new ProjectWriteAdmission(profile, Guid.NewGuid(), Guid.NewGuid());
        var expected = new ProjectDefinitionExpectation(admission, Guid.NewGuid());
        var caller = ProjectMutationCallerScope.LocalWorkspace.Bind(space);
        var submission = new ProjectLifecycleSubmission(new(profile, space, new(31), caller, Guid.NewGuid()),
            ProjectLifecycleInterpretation.VersionOne, new ProjectLifecycleCommand.Delete(expected));
        var frozen = Codec.Freeze(submission);
        var draft = new ProjectEditorDraft(new() { Id = admission.ProjectId, ExpectedDefinition = expected, Name = "Original draft" }, new(space, caller));
        var accepted = new ProjectLifecycleAccepted(submission, 1, Now, Now.AddDays(7), new ProjectLifecycleAcknowledgement.DeletionCommitted(
            new(new(new(profile, space, new(Guid.NewGuid())), admission), [])));
        return (draft, frozen, accepted);
    }

    private static ProjectLifecycleWriteContext Context(ProjectLifecycleFrozenSubmission original) => new(
        original.Submission.Operation.DatabaseProfileId, original.Submission.Operation.Namespace, original.Submission.Operation.CallerBinding,
        original.Submission.Operation.Epoch, Now.AddHours(1), Now.AddDays(7));

    private sealed class Store(ProjectLifecycleFrozenSubmission original) : IProjectLifecyclePendingStore {
        internal ProjectLifecyclePendingRecord? Record;
        internal bool FailAcceptance;
        internal int Acceptances;
        internal int Dispatches;
        private long revision;

        public ValueTask InitializeAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public ValueTask<ProjectLifecyclePendingChange> PrepareAsync(ProjectLifecycleFrozenSubmission frozen, CancellationToken cancellationToken = default) {
            Assert.Equal(original, frozen);
            Assert.Null(Record);
            Record = new(frozen, new(++revision), ProjectPendingStage.Prepared, null, false, null, null);
            return Applied();
        }
        public ValueTask<ProjectLifecyclePendingChange> BeginDispatchAsync(ProjectLifecyclePendingRecord before, Guid attemptId,
            bool explicitRetry, CancellationToken cancellationToken = default) {
            Assert.Equal(Record, before);
            Assert.False(explicitRetry);
            Dispatches++;
            Record = before with { Revision = new(++revision), Stage = ProjectPendingStage.Dispatching, AttemptId = attemptId };
            return Applied();
        }
        public ValueTask<ProjectLifecyclePendingChange> ObserveAcceptedAsync(ProjectLifecycleFrozenSubmission frozen,
            ProjectLifecycleAccepted accepted, CancellationToken cancellationToken = default) {
            Assert.Equal(original, frozen);
            if (FailAcceptance) {
                return ValueTask.FromException<ProjectLifecyclePendingChange>(new IOException("Owned evidence storage refusal."));
            }
            Acceptances++;
            Record = Record! with { Revision = new(++revision), Stage = ProjectPendingStage.Accepted, Accepted = Codec.EncodeAccepted(frozen.Submission, accepted) };
            return Applied();
        }
        public ValueTask<ProjectLifecyclePendingRecord?> ReadAsync(ProjectLifecycleOperation operation, CancellationToken cancellationToken = default) {
            Assert.Equal(original.Submission.Operation, operation);
            return ValueTask.FromResult(Record);
        }
        private ValueTask<ProjectLifecyclePendingChange> Applied() => ValueTask.FromResult(new ProjectLifecyclePendingChange(ProjectPendingStoreStatus.Applied, Record));
        public ValueTask<ProjectLifecyclePendingChange> ObserveAttemptAsync(ProjectLifecycleOperation operation, Guid attemptId, ProjectPendingAttemptOutcome outcome, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<ProjectLifecyclePendingChange> ObserveClosedAbsentAsync(ProjectLifecycleFrozenSubmission frozen, ProjectLifecycleLookup.ClosedAbsent absence, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<ProjectLifecyclePendingPage> ListAsync(ProjectLifecyclePendingScope scope, int take, ProjectLifecycleOperation? after = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<ProjectLifecyclePendingChange> CancelPreparedAsync(ProjectLifecyclePendingRecord record, CancellationToken cancellationToken = default) => throw new InvalidOperationException("No lifetime proof clears evidence.");
        public ValueTask<ProjectLifecyclePendingChange> ClearResolvedAsync(ProjectLifecyclePendingRecord record, CancellationToken cancellationToken = default) => throw new InvalidOperationException("No lifetime proof clears evidence.");
    }
}
