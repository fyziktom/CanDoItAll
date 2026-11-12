using CanDoItAll.Editing;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Projects.Pages.Components;

namespace CanDoItAll.Tests.Components.ProjectsUi;

public sealed class ProjectDefinitionReviewTests {
    [Fact]
    public void Disjoint_text_and_scalar_changes_combine_without_changing_the_original_or_current() {
        var original = Original();
        var current = Current(original, original.Scalars with { Description = "one\ntwo\npeer\n", CurrentPhase = "Peer delivery" });
        var local = original.Scalars with { Name = "My name", Description = "mine\ntwo\nthree\n" };

        var review = Review(original, local, current);
        var candidate = review.Candidate();

        Assert.Equal("My name", candidate.Name);
        Assert.Equal("mine\ntwo\npeer\n", candidate.Description);
        Assert.Equal("Peer delivery", candidate.CurrentPhase);
        Assert.Equal(current.Expectation, candidate.ExpectedDefinition);
        Assert.Equal("Original", original.Scalars.Name);
        Assert.Equal("one\ntwo\npeer\n", current.Scalars.Description);
    }

    [Fact]
    public void Conflicting_scalar_choices_remain_unresolved_until_an_explicit_local_current_or_custom_choice() {
        var original = Original();
        var current = Current(original, original.Scalars with { Name = "Peer name" });
        var review = Review(original, original.Scalars with { Name = "My name" }, current);
        Assert.False(review.CanApply);
        Assert.Throws<InvalidOperationException>(review.Candidate);

        review.Name.SetResult("Chosen result");

        Assert.Equal("Chosen result", review.Candidate().Name);
        review.Name.UseCurrent();
        Assert.Equal("Peer name", review.Candidate().Name);
        review.Name.UseLocal();
        Assert.Equal("My name", review.Candidate().Name);
    }

    [Fact]
    public void Fields_on_the_same_persisted_phase_merge_by_identity_without_a_whole_row_conflict() {
        var original = Original();
        var localPhases = original.Phases.Select(ProjectPhaseValues.From).ToArray();
        localPhases[0] = localPhases[0] with { Name = "My phase name" };
        var current = Current(original, phases: [original.Phases[0] with { Goal = "Peer goal" }, original.Phases[1]]);
        var review = Review(original, original.Scalars, current, localPhases);

        var phase = review.Candidate().Phases[0];

        Assert.Equal(original.Phases[0].Id, phase.Id);
        Assert.Equal("My phase name", phase.Name);
        Assert.Equal("Peer goal", phase.Goal);
        Assert.True(review.Phases[0].Row.Choice == MergeChoice.Unresolved);
        Assert.True(review.Phases[0].Fields!.CanApply);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Deletion_versus_edit_requires_a_row_choice_and_retaining_a_missing_phase_requires_order_review(bool delete) {
        var original = Original();
        var current = Current(original, phases: [original.Phases[0] with { Goal = "Peer goal" }, original.Phases[1]]);
        var review = Review(original, original.Scalars, current, [ProjectPhaseValues.From(original.Phases[1])]);
        var row = review.Phases.Single(row => row.Key.StoredId == original.Phases[0].Id);
        Assert.False(review.CanApply);
        Assert.Null(row.Fields);
        if (delete) {
            row.Row.UseLocal();
            Assert.Single(review.Candidate().Phases);
        } else {
            row.Row.UseCurrent();
            Assert.True(review.OrderNeedsReview);
            Assert.False(review.CanApply);
            review.PhaseOrder.UseCurrent();
            Assert.Equal(original.Phases.Select(row => row.Id), review.Candidate().Phases.Select(row => row.Id!.Value));
            Assert.Equal("Peer goal", review.Candidate().Phases[0].Goal);
        }
    }

    [Fact]
    public void Independent_new_phases_survive_explicit_order_choice_and_local_keys_are_never_published_as_stored_ids() {
        var original = Original();
        var localNew = new ProjectPhaseValues(ProjectDefinitionRowKey.Local(), "Local addition", "", default, new(null, null));
        var remoteNew = new ProjectDefinitionPhase(Guid.NewGuid(), "Peer addition", "", default, null, null);
        var local = original.Phases.Select(ProjectPhaseValues.From).Append(localNew).ToArray();
        var current = Current(original, phases: [original.Phases[0], remoteNew, original.Phases[1]]);
        var review = Review(original, original.Scalars, current, local);
        Assert.False(review.PhaseOrder.IsResolved);

        review.PhaseOrder.UseCurrent();
        var candidate = review.Candidate();

        Assert.Equal(4, candidate.Phases.Count);
        Assert.Equal("Local addition", candidate.Phases[^1].Name);
        Assert.Null(candidate.Phases[^1].Id);
        Assert.Equal(remoteNew.Id, candidate.Phases[1].Id);
        Assert.DoesNotContain(candidate.Phases, row => row.Id == localNew.Key.Value);
    }

    [Fact]
    public void Options_merge_fields_by_stable_id_and_keep_native_category_order() {
        var original = Original();
        var local = original.Options.Select(ProjectOptionValues.From).ToArray();
        local[0] = local[0] with { Name = "My runtime" };
        var current = Current(original, options: [original.Options[0] with { Notes = "Peer notes" }]);
        var review = Review(original, original.Scalars, current, options: local);

        var option = Assert.Single(review.Candidate().Options);

        Assert.Equal(original.Options[0].Id, option.Id);
        Assert.Equal("My runtime", option.OptionName);
        Assert.Equal("Peer notes", option.Notes);
    }

    [Fact]
    public void Phase_date_ranges_are_reviewed_as_one_typed_group_and_unfinished_result_dates_block_apply() {
        var original = Original();
        var local = original.Phases.Select(ProjectPhaseValues.From).ToArray();
        local[0] = local[0] with { Dates = new(new DateTime(2027, 1, 5), null) };
        var current = Current(original, phases: [original.Phases[0] with { EndDateUtc = new DateTime(2027, 1, 8) }, original.Phases[1]]);
        var review = Review(original, original.Scalars, current, local);
        var dates = review.Phases[0].Fields!.Dates;
        Assert.False(dates.IsResolved);
        dates.UseCurrent();
        var input = new ProjectMergeInput(ProjectMergeInputField.PhaseStart, local[0].Key);
        review.PendingInput(input, true);
        Assert.False(review.CanApply);
        review.PendingInput(input, false);
        Assert.Null(review.Candidate().Phases[0].StartDateUtc);
        Assert.Equal(current.Phases[0].EndDateUtc, review.Candidate().Phases[0].EndDateUtc);
    }

    [Fact]
    public void A_comparison_cannot_rebind_to_a_recreated_project_with_the_same_id() {
        var original = Original();
        var replaced = new ProjectDefinitionSnapshot(new(new(original.Expectation.Project.DatabaseProfileId,
            original.Expectation.Project.ProjectId, Guid.NewGuid()), Guid.NewGuid()), original.Scalars, null, original.Phases, original.Options);

        Assert.Throws<InvalidOperationException>(() => Review(original, original.Scalars, replaced));
    }

    [Fact]
    public void Already_assigned_external_identity_cannot_be_replaced_by_a_merge_choice() {
        var original = Original();
        var current = new ProjectDefinitionSnapshot(new(original.Expectation.Project, Guid.NewGuid()), original.Scalars,
            new("peer", "external-key"), original.Phases, original.Options);
        var review = Review(original, original.Scalars, current);
        review.ExternalIdentity.UseLocal();
        Assert.False(review.CanApply);
        review.ExternalIdentity.UseCurrent();
        Assert.Equal("external-key", review.Candidate().ExternalKey);
    }

    [Fact]
    public void Keeping_a_locally_edited_phase_removed_by_current_creates_a_new_draft_row_without_its_retired_id() {
        var original = Original();
        var local = original.Phases.Select(ProjectPhaseValues.From).ToArray();
        local[0] = local[0] with { Goal = "My retained goal" };
        var review = Review(original, original.Scalars, Current(original, phases: [original.Phases[1]]), local);
        var removed = review.Phases.Single(row => row.Key.StoredId == original.Phases[0].Id);
        Assert.False(removed.Row.IsResolved);
        removed.Row.UseLocal();
        review.PhaseOrder.UseLocal();
        var candidate = review.Candidate();
        Assert.Null(candidate.Phases[0].Id);
        Assert.Equal("My retained goal", candidate.Phases[0].Goal);
        Assert.Equal(original.Phases[1].Id, candidate.Phases[1].Id);
    }

    internal static ProjectDefinitionSnapshot Original() => new(
        new(new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()), Guid.NewGuid()),
        new("Original", "one\ntwo\nthree\n", "Objective", default, "Planning", null), null,
        [new(Guid.NewGuid(), "First", "First goal", default, null, null), new(Guid.NewGuid(), "Second", "Second goal", default, null, null)],
        [new(Guid.NewGuid(), ProjectOptionCategory.Language, "C#", "Original notes")]);

    internal static ProjectDefinitionSnapshot Current(ProjectDefinitionSnapshot original, ProjectDefinitionScalars? scalars = null,
        IReadOnlyList<ProjectDefinitionPhase>? phases = null, IReadOnlyList<ProjectDefinitionOption>? options = null)
        => new(new(original.Expectation.Project, Guid.NewGuid()), scalars ?? original.Scalars, original.ExternalIdentity,
            phases ?? original.Phases, options ?? original.Options);

    internal static ProjectDefinitionReview Review(ProjectDefinitionSnapshot original, ProjectDefinitionScalars local,
        ProjectDefinitionSnapshot current, IReadOnlyList<ProjectPhaseValues>? phases = null, IReadOnlyList<ProjectOptionValues>? options = null)
        => new(original, local, original.ExternalIdentity, phases ?? original.Phases.Select(ProjectPhaseValues.From).ToArray(),
            options ?? original.Options.Select(ProjectOptionValues.From).ToArray(), current);
}
