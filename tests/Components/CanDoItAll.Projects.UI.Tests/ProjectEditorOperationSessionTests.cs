using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Projects.Pages.Components;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Tests.Components.ProjectsUi;

public sealed class ProjectEditorOperationSessionTests {
    private static readonly DateTimeOffset Now = new(2026, 10, 16, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Accepted_submission_preserves_post_dispatch_typing_and_maps_child_identity() {
        var (draft, frozen, accepted) = Values();
        var submitted = draft.Capture();
        var store = new ProjectPendingSubmissionTestStore();
        using var session = new ProjectEditorOperationSession(draft, submitted, new(store, frozen, true));
        await session.PrepareAsync();
        var result = new TaskCompletionSource<ProjectPendingDispatchOutcome>(TaskCreationOptions.RunContinuationsAsynchronously);
        var dispatch = session.Pending.DispatchAsync(Context(frozen), Now, false, (_, _) => result.Task);
        draft.Model.Name = "Typed after dispatch";
        draft.EditContext.NotifyFieldChanged(new(draft.Model, nameof(draft.Model.Name)));
        draft.Model.Phases[0].Name = "New phase typing";
        draft.EditContext.NotifyFieldChanged(new(draft.Model.Phases[0], nameof(ProjectPhaseEditorModel.Name)));
        result.SetResult(new ProjectPendingDispatchOutcome.Full(new ProjectDefinitionOutcome<ProjectEditorAcknowledgement>.Accepted(accepted.Result, false)));
        await dispatch;
        session.Reconcile();
        Assert.Equal("Typed after dispatch", draft.Model.Name);
        Assert.Equal("New phase typing", draft.Model.Phases[0].Name);
        Assert.Equal(accepted.Result.Acknowledgement.Phases[0].Id, draft.Model.Phases[0].Id);
        Assert.Same(accepted.Result.Acknowledgement.Definition, draft.Model.ExpectedDefinition);
        Assert.Same(accepted.Result, Assert.IsType<ProjectDefinitionAcceptedReceipt.Full>(session.Pending.KnownAccepted).Result);
        Assert.Throws<InvalidOperationException>(session.Reconcile);
    }

    [Fact]
    public async Task Retiring_editor_before_late_acceptance_retains_evidence_without_acknowledgement_or_seed() {
        var (draft, frozen, accepted) = Values();
        var store = new ProjectPendingSubmissionTestStore();
        using var session = new ProjectEditorOperationSession(draft, draft.Capture(), new(store, frozen, true));
        await session.PrepareAsync();
        var result = new TaskCompletionSource<ProjectPendingDispatchOutcome>(TaskCreationOptions.RunContinuationsAsynchronously);
        var dispatch = session.Pending.DispatchAsync(Context(frozen), Now, false, (_, _) => result.Task);
        session.Dispose();
        result.SetResult(new ProjectPendingDispatchOutcome.Full(new ProjectDefinitionOutcome<ProjectEditorAcknowledgement>.Accepted(accepted.Result, false)));
        await dispatch;
        session.RememberAcceptance();
        Assert.Equal(ProjectPendingStage.Accepted, Assert.Single(store.Records).Stage);
        Assert.Null(draft.Acknowledgement);
        Assert.Same(accepted.Result.Acknowledgement, draft.AcceptedOutcome!.Accepted);
        Assert.False(session.CanSeed);
        Assert.Throws<ObjectDisposedException>(session.Reconcile);
    }

    [Fact]
    public async Task Restored_historical_receipt_requires_lookup_and_does_not_adopt_a_separate_current_read() {
        var (original, frozen, accepted) = Values();
        var store = new ProjectPendingSubmissionTestStore();
        await store.PrepareAsync(frozen, true);
        await store.ObserveAcceptedAsync(frozen, accepted);
        var restored = ProjectEditorDraft.Restore(frozen.Submission);
        using var session = new ProjectEditorOperationSession(restored, restored.Capture(), new(store, Assert.Single(store.Records)), true);
        Assert.False(session.Pending.CanReconcile);
        await session.Pending.ObserveLookupAsync(new ProjectDefinitionLookup.FoundFull(accepted.Result));
        session.Reconcile();
        var historical = restored.Model.ExpectedDefinition;
        var current = new ProjectDefinitionSnapshot(new(historical!.Project, Guid.NewGuid()),
            new("Later saved name", "", "", ProjectStatus.Active, "", null), null, [], []);
        restored.InspectCurrent(current);
        Assert.Same(historical, restored.Model.ExpectedDefinition);
        Assert.Equal(original.Model.Name.Trim(), restored.Model.Name);
        Assert.Equal("Later saved name", restored.ComparisonModel!.Name);
        Assert.False(session.CanSeed);
        Assert.Empty(restored.StarterObjects);
        var reconciled = restored.StartFromCurrent(true);
        Assert.Same(current.Expectation, reconciled.Model.ExpectedDefinition);
        Assert.Same(restored, reconciled.PreviousDraft);
        Assert.Equal(restored.Scope, reconciled.Scope);
    }

    [Fact]
    public async Task Starter_attempt_is_marked_before_effect_and_excluded_from_future_definition_submissions() {
        var (draft, frozen, accepted) = Values();
        var store = new ProjectPendingSubmissionTestStore();
        using var session = new ProjectEditorOperationSession(draft, draft.Capture(), new(store, frozen, true));
        await session.PrepareAsync();
        await session.Pending.DispatchAsync(Context(frozen), Now, false,
            (_, _) => Task.FromResult<ProjectPendingDispatchOutcome>(new ProjectPendingDispatchOutcome.Full(new ProjectDefinitionOutcome<ProjectEditorAcknowledgement>.Accepted(accepted.Result, false))));
        session.Reconcile();
        Assert.True(session.CanSeed);
        session.BeginSeedAttempt();
        var row = Assert.Single(draft.StarterObjects);
        Assert.True(row.WasDispatched);
        Assert.False(row.IsSeeded);
        Assert.True(session.SeedOutcomeUnknown);
        row.Title = "Later starter typing";
        Assert.Empty(draft.Capture().Seeds);
        Assert.Throws<InvalidOperationException>(session.BeginSeedAttempt);
        session.CompleteSeedAttempt();
        Assert.False(session.SeedOutcomeUnknown);
        Assert.True(row.IsSeeded);
        Assert.Same(row, Assert.Single(draft.StarterObjects));
        Assert.Equal("Later starter typing", row.Title);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task Replay_or_restored_sessions_never_create_starter_effects(bool replayed, bool restored) {
        var (draft, frozen, accepted) = Values();
        var store = new ProjectPendingSubmissionTestStore();
        using var session = new ProjectEditorOperationSession(draft, draft.Capture(), new(store, frozen, true), restored);
        await session.PrepareAsync();
        await session.Pending.DispatchAsync(Context(frozen), Now, false,
            (_, _) => Task.FromResult<ProjectPendingDispatchOutcome>(new ProjectPendingDispatchOutcome.Full(new ProjectDefinitionOutcome<ProjectEditorAcknowledgement>.Accepted(accepted.Result, replayed))));
        session.Reconcile();
        Assert.False(session.CanSeed);
        Assert.Throws<InvalidOperationException>(session.BeginSeedAttempt);
        Assert.False(Assert.Single(draft.StarterObjects).WasDispatched);
    }

    [Fact]
    public void Restore_preserves_exact_frozen_order_and_blank_rows_without_inserting_defaults() {
        var (_, frozen, _) = Values();
        var restored = ProjectEditorDraft.Restore(frozen.Submission);
        Assert.Equal("  Submitted  ", restored.Model.Name);
        Assert.Single(restored.Model.Phases);
        var option = Assert.Single(restored.Model.Options);
        Assert.Equal(ProjectOptionCategory.Other, option.Category);
        Assert.Empty(option.OptionName);
        Assert.Null(option.Id);
        Assert.Equal(frozen.Submission.Operation.Namespace, restored.Scope!.Namespace);
        Assert.False(restored.CanMutate);
    }

    private static (ProjectEditorDraft Draft, ProjectDefinitionFrozenSubmission Frozen, ProjectDefinitionAcceptedReceipt.Full Accepted) Values() {
        var operation = new ProjectDefinitionOperation(new(Guid.NewGuid()), new(1), Guid.NewGuid(), new("pc1:" + new string('a', 64)));
        var expected = new ProjectDefinitionExpectation(new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()), Guid.NewGuid());
        var draft = new ProjectEditorDraft(new() {
            Id = expected.Project.ProjectId, ExpectedLifetimeId = expected.Project.LifetimeId, ExpectedProjectAdmission = expected.Project,
            ExpectedDefinition = expected, Name = "  Submitted  ", Status = ProjectStatus.Active,
            Phases = [new() { Name = "Submitted phase" }], Options = [new() { Category = ProjectOptionCategory.Other }]
        }, new(operation.Namespace, operation.CallerBinding));
        draft.StarterObjects.Add(new() { Title = "Submitted starter" });
        var frozen = new ProjectDefinitionFrozenSubmission(new ProjectDefinitionSubmission.Replace(new(operation, ProjectDefinitionRequestVersion.VersionOne,
            new(expected, new(draft.Model.Name, "", "", ProjectStatus.Active, "", null),
                [new(null, "Submitted phase", "", ProjectPhaseStatus.Planned, null, null)], [new(null, ProjectOptionCategory.Other, "", "")], null))), "test frozen payload");
        var accepted = new ProjectDefinitionAcceptedReceipt.Full(new(operation, ProjectDefinitionResultVersion.VersionOne, ProjectDefinitionCommandKind.Replace,
            Now, Now.AddDays(7), new(new(expected.Project, Guid.NewGuid()), "Submitted", "", "", ProjectStatus.Active, "", null,
                [new(0, Guid.NewGuid(), "Submitted phase", "", ProjectPhaseStatus.Planned, null, null)], [], null)));
        return (draft, frozen, accepted);
    }

    private static ProjectDefinitionWriteContext Context(ProjectDefinitionFrozenSubmission frozen)
        => new(frozen.Submission.Operation.Namespace, frozen.Submission.Operation.CallerBinding, frozen.Submission.Operation.Epoch,
            Now.AddDays(1), Now.AddDays(7), [ProjectDefinitionRequestVersion.VersionOne], [ProjectDefinitionResultVersion.VersionOne], ProjectDefinitionLimits.VersionOneMaximum);
}
