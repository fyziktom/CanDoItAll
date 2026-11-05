using CanDoItAll.Editing;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Projects.Pages.Components;
using Microsoft.AspNetCore.Components.Forms;
using static CanDoItAll.Tests.Components.ProjectsUi.ProjectDefinitionReviewTests;

namespace CanDoItAll.Tests.Components.ProjectsUi;

public sealed class ProjectDefinitionReconciliationTests {
    [Fact]
    public void Apply_and_undo_preserve_model_EditContext_child_instances_and_independent_starter_effects() {
        var original = Original();
        var draft = Open(original);
        var model = draft.Model;
        var context = draft.EditContext;
        var phase = model.Phases[0];
        var starter = new StarterObjectDraft { Title = "Independent intent" };
        draft.StarterObjects.Add(starter);
        model.Name = "My local name";
        var current = Current(original, original.Scalars with { Description = "Peer description" });
        draft.InspectCurrent(current);
        draft.BeginMerge();
        Assert.True(draft.CanApplyMerge);

        draft.ApplyMerge();

        Assert.Same(model, draft.Model);
        Assert.Same(context, draft.EditContext);
        Assert.Same(phase, model.Phases[0]);
        Assert.Same(current, draft.Original);
        Assert.Equal(current.Expectation, model.ExpectedDefinition);
        Assert.Equal("My local name", model.Name);
        Assert.Equal("Peer description", model.Description);
        Assert.Null(draft.Acknowledgement);
        Assert.Null(draft.AcceptedOutcome);
        Assert.True(draft.CanUndoMerge);
        Assert.Same(starter, Assert.Single(draft.StarterObjects));
        Assert.False(starter.IsSeeded);
        Assert.False(starter.WasDispatched);

        draft.UndoMerge();

        Assert.Same(current, draft.Original);
        Assert.Equal(current.Expectation, model.ExpectedDefinition);
        Assert.Equal("My local name", model.Name);
        Assert.Equal(original.Scalars.Description, model.Description);
        Assert.Same(context, draft.EditContext);
        Assert.Same(phase, model.Phases[0]);
        Assert.False(draft.Definition.IsOutdated);
        Assert.True(draft.CanMutate);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Later_live_input_and_ABA_cannot_be_replaced_by_a_comparison(bool aba) {
        var original = Original();
        var draft = Open(original);
        var current = Current(original, original.Scalars with { Description = "Peer description" });
        draft.InspectCurrent(current);
        draft.BeginMerge();
        draft.Model.Name = "Later input";
        draft.EditContext.NotifyFieldChanged(new(draft.Model, nameof(draft.Model.Name)));
        if (aba) {
            draft.Model.Name = original.Scalars.Name;
            draft.EditContext.NotifyFieldChanged(new(draft.Model, nameof(draft.Model.Name)));
        }

        Assert.False(draft.CanApplyMerge);
        Assert.Throws<InvalidOperationException>(draft.ApplyMerge);
        Assert.Same(original, draft.Original);
        Assert.Equal(aba ? original.Scalars.Name : "Later input", draft.Model.Name);
    }

    [Fact]
    public void Later_input_after_apply_blocks_undo_instead_of_silently_replacing_that_input() {
        var original = Original();
        var draft = Open(original);
        draft.InspectCurrent(Current(original));
        draft.BeginMerge();
        draft.ApplyMerge();
        draft.Model.Name = "After apply";
        draft.EditContext.NotifyFieldChanged(new(draft.Model, nameof(draft.Model.Name)));

        Assert.False(draft.CanUndoMerge);
        Assert.Throws<InvalidOperationException>(draft.UndoMerge);
        Assert.Equal("After apply", draft.Model.Name);
    }

    [Fact]
    public void A_hint_during_the_current_read_or_comparison_keeps_the_comparison_stale() {
        var original = Original();
        var draft = Open(original);
        var beforeRead = draft.Definition.HintRevision;
        draft.Definition.Observe(Batch(original, 1));
        draft.InspectCurrent(Current(original), beforeRead);
        draft.BeginMerge();

        Assert.True(draft.Definition.ReviewIsStale);
        Assert.False(draft.CanApplyMerge);
        Assert.Equal(original.Expectation, draft.Model.ExpectedDefinition);
        draft.InspectCurrent(Current(original), draft.Definition.HintRevision);
        draft.BeginMerge();
        Assert.True(draft.CanApplyMerge);
        draft.Definition.Observe(Batch(original, 2));
        Assert.False(draft.CanApplyMerge);
        Assert.Same(original, draft.Original);
    }

    [Fact]
    public void Direct_model_changes_are_fenced_even_when_a_consumer_omits_field_notifications() {
        var original = Original();
        var draft = Open(original);
        draft.InspectCurrent(Current(original));
        draft.BeginMerge();
        draft.Model.Phases[0].Goal = "Changed without a notification";

        Assert.False(draft.CanApplyMerge);
        Assert.Throws<InvalidOperationException>(draft.ApplyMerge);
        Assert.Equal("Changed without a notification", draft.Model.Phases[0].Goal);
    }

    [Fact]
    public void Pending_native_input_and_unresolved_native_operations_block_apply_and_new_comparisons() {
        var original = Original();
        var draft = Open(original);
        draft.InspectCurrent(Current(original));
        draft.BeginMerge();
        var field = new FieldIdentifier(draft.Model, nameof(draft.Model.TargetDateUtc));
        draft.PendingInput(field, true);
        Assert.False(draft.CanApplyMerge);
        Assert.False(draft.CanMutate);
        draft.PendingInput(field, false);
        Assert.False(draft.CanApplyMerge);
        draft.BeginMerge();
        Assert.True(draft.CanApplyMerge);
        draft.Mutation = ProjectEditorMutationState.SaveOutcomeUnknown;
        Assert.False(draft.CanApplyMerge);
        Assert.Throws<InvalidOperationException>(draft.BeginMerge);
    }

    [Fact]
    public void An_own_hint_cannot_resolve_unknown_and_only_its_exact_known_accepted_operation_is_suppressed() {
        var original = Original();
        var draft = Open(original);
        var own = Batch(original, 1, draft.Definition.OpeningId);
        var other = Batch(original, 2, Guid.NewGuid(), epoch: own.Changes[0].Cursor.Epoch);
        draft.Mutation = ProjectEditorMutationState.SaveOutcomeUnknown;
        draft.Definition.Observe(own);
        Assert.True(draft.Definition.IsOutdated);
        Assert.False(draft.CanMutate);
        draft.Definition.AcceptedOperation(own.Changes[0].OperationId);
        Assert.False(draft.Definition.IsOutdated);
        Assert.Equal(ProjectEditorMutationState.SaveOutcomeUnknown, draft.Mutation);
        draft.Definition.Observe(own);
        Assert.False(draft.Definition.IsOutdated);
        draft.Definition.Observe(other);
        draft.Definition.AcceptedOperation(own.Changes[0].OperationId);
        Assert.True(draft.Definition.IsOutdated);
    }

    [Fact]
    public void Metadata_and_foreign_profile_hints_do_not_stale_the_definition_but_a_gap_requires_explicit_pull() {
        var original = Original();
        var draft = Open(original);
        draft.Definition.Observe(Batch(original, 1, kind: EditingChangeKind.MetadataChanged));
        var foreign = Batch(original, 2);
        var change = foreign.Changes[0];
        draft.Definition.Observe(new([change with { Resource = change.Resource with { Scope = change.Resource.Scope with { Id = Guid.NewGuid() } } }], false));
        Assert.False(draft.Definition.IsOutdated);
        draft.Definition.Observe(new([], true));
        Assert.True(draft.Definition.IsOutdated);
        draft.InspectCurrent(Current(original));
        draft.BeginMerge();
        draft.ApplyMerge();
        Assert.False(draft.Definition.IsOutdated);
        Assert.True(draft.CanMutate);
    }

    [Fact]
    public void A_new_unsaved_project_has_no_existing_definition_to_invalidate_or_merge() {
        var draft = new ProjectEditorDraft(new() { Name = "New project" });
        draft.Definition.Observe(new([], true));
        Assert.True(draft.CanMutate);
        Assert.False(draft.CanCompare);
        Assert.Throws<InvalidOperationException>(draft.BeginMerge);
    }

    [Fact]
    public void Undo_retains_current_authority_and_never_reuses_child_identities_removed_by_that_current() {
        var original = Original();
        var draft = Open(original);
        var removedPhase = draft.Model.Phases[0];
        var current = Current(original, phases: [original.Phases[1]], options: []);
        draft.InspectCurrent(current);
        draft.BeginMerge();
        draft.ApplyMerge();
        draft.UndoMerge();
        Assert.Same(current, draft.Original);
        Assert.Equal(current.Expectation, draft.Model.ExpectedDefinition);
        Assert.Same(removedPhase, draft.Model.Phases[0]);
        Assert.Null(removedPhase.Id);
        Assert.Null(draft.Model.Options.Single(row => row.OptionName == "C#").Id);
        Assert.True(draft.CanMutate);
    }

    [Fact]
    public void Rebuilding_a_comparison_keeps_only_choices_with_unchanged_original_local_and_current_values() {
        var original = Original();
        var draft = Open(original);
        draft.Model.Name = "My name";
        draft.InspectCurrent(Current(original, original.Scalars with { Name = "Peer name" }));
        draft.BeginMerge();
        var first = draft.Definition.Review!;
        first.Name.SetResult("My custom result");
        draft.InspectCurrent(Current(original, original.Scalars with { Name = "Peer name", Description = "New description" }));
        draft.BeginMerge();
        Assert.Equal("My custom result", draft.Definition.Review!.Name.Result);
        Assert.Equal("New description", draft.Definition.Review.Description.Result);
        draft.InspectCurrent(Current(original, original.Scalars with { Name = "New peer name" }));
        draft.BeginMerge();
        Assert.False(draft.Definition.Review!.Name.IsResolved);
        Assert.Equal("My custom result", draft.Definition.PreviousReview!.Name.Result);
        Assert.Equal("My name", draft.Model.Name);
    }

    [Fact]
    public void Refresh_cannot_replace_an_unfinished_comparison_result() {
        var original = Original();
        var draft = Open(original);
        draft.InspectCurrent(Current(original));
        draft.BeginMerge();
        var review = draft.Definition.Review!;
        review.PendingInput(new(ProjectMergeInputField.TargetDate), true);
        draft.InspectCurrent(Current(original));
        Assert.Throws<InvalidOperationException>(draft.BeginMerge);
        Assert.Same(review, draft.Definition.Review);
        Assert.Equal(original.Expectation, draft.Model.ExpectedDefinition);
    }

    private static ProjectEditorDraft Open(ProjectDefinitionSnapshot original)
        => new(ProjectEditorDraft.CreateModel(original), original: original);

    [Fact]
    public void Local_changes_are_measured_against_the_actual_baseline_including_rows_and_unfinished_input() {
        var original = Original();
        var draft = Open(original);
        Assert.False(draft.HasLocalChanges);
        draft.Model.Name = "Unsaved name";
        Assert.True(draft.HasLocalChanges);
        draft.Model.Name = original.Scalars.Name;
        Assert.False(draft.HasLocalChanges);
        draft.PendingInput(new(draft.Model, nameof(draft.Model.TargetDateUtc)), true);
        Assert.True(draft.HasLocalChanges);
        draft.PendingInput(new(draft.Model, nameof(draft.Model.TargetDateUtc)), false);
        Assert.False(draft.HasLocalChanges);
        draft.Model.Phases.Reverse();
        Assert.True(draft.HasLocalChanges);
        draft.Model.Phases.Reverse();
        draft.Model.Options.RemoveAll(row => row.Id.HasValue);
        Assert.True(draft.HasLocalChanges);
    }

    [Fact]
    public void Applying_only_current_values_is_clean_but_undo_restores_a_local_change_against_current() {
        var original = Original();
        var draft = Open(original);
        draft.InspectCurrent(Current(original, original.Scalars with { Name = "New current name" }));
        draft.BeginMerge();
        Assert.True(draft.HasLocalChanges);
        draft.ApplyMerge();
        Assert.False(draft.HasLocalChanges);
        draft.UndoMerge();
        Assert.True(draft.HasLocalChanges);
    }

    private static EditingChangeBatch Batch(ProjectDefinitionSnapshot original, long sequence, Guid? editor = null,
        EditingChangeKind kind = EditingChangeKind.EditableContentChanged, Guid? epoch = null) => new([
            new(new(epoch ?? Guid.NewGuid(), sequence), new(new(EditingScopeKind.Dataset, original.Expectation.Project.DatabaseProfileId, 1),
                EditingOwner.Project, original.Expectation.Project.ProjectId), kind, editor, Guid.NewGuid(), DateTimeOffset.UnixEpoch)
        ], false);
}
