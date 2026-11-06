using CanDoItAll.Modules.Workbench;
using CanDoItAll.Workbench.Planning.UI;

namespace CanDoItAll.Tests.WorkbenchPlanning;

public sealed class TaskFormReviewTests {
    internal static PlanningTaskEditorInput.Structure Original() => new("Original", "Lane", "first\nmiddle\nlast", "2026-11-02T12:00:00.1234567",
        null, new("8", "Hours", "", "USD"), new(ProjectTaskExecutionState.NotStarted, "", ""), null, null, null);

    [Fact]
    public void Peer_title_and_local_notes_combine_without_losing_invalid_raw_cost_or_attachment_intent() {
        var original = Original();
        var attachment = new ProjectStructureTaskResourceSelection(ProjectStructureTaskResourceKind.Workflow, Guid.NewGuid(), Guid.NewGuid());
        var local = original with { Notes = "local notes", Estimate = original.Estimate! with { Cost = "2e" }, Attachment = attachment, SelectedResource = attachment };
        var current = original with { Title = "Peer" };
        var review = new PlanningTaskFormReview(original, local, new(Guid.NewGuid(), current, true, false));
        var result = Assert.IsType<PlanningTaskEditorInput.Structure>(review.Candidate());
        Assert.Equal("Peer", result.Title);
        Assert.Equal("local notes", result.Notes);
        Assert.Equal("2e", result.Estimate!.Cost);
        Assert.Equal(attachment, result.Attachment);
        Assert.Equal(attachment, result.SelectedResource);
        Assert.Equal(original.Due, result.Due);
    }

    [Fact]
    public void Overlapping_text_requires_choice_and_custom_result_is_retained_only_for_the_same_triple() {
        var original = Original();
        var local = original with { Notes = "my notes" };
        var saved = original with { Notes = "peer notes" };
        var review = new PlanningTaskFormReview(original, local, new(Guid.NewGuid(), saved, true, false));
        Assert.False(review.CanApply);
        review.Notes!.SetResult("chosen custom notes");
        var same = new PlanningTaskFormReview(original, local, new(Guid.NewGuid(), saved, true, false));
        same.PreserveChoices(review);
        Assert.Equal("chosen custom notes", same.Notes!.Result);
        var newer = new PlanningTaskFormReview(original, local, new(Guid.NewGuid(), saved with { Notes = "third notes" }, true, false));
        newer.PreserveChoices(review);
        Assert.False(newer.CanApply);
        Assert.Contains("chosen custom notes", review.CopyResults(), StringComparison.Ordinal);
    }

    [Fact]
    public void Disjoint_notes_use_the_bounded_text_merge() {
        var original = Original();
        var review = new PlanningTaskFormReview(original, original with { Notes = "local\nmiddle\nlast" },
            new(Guid.NewGuid(), original with { Notes = "first\nmiddle\npeer" }, true, false));
        Assert.Equal("local\nmiddle\npeer", review.Notes!.Result);
    }

    [Fact]
    public void Effort_amount_and_unit_are_one_conflict_group() {
        var original = Original();
        var local = original with { Estimate = original.Estimate! with { Effort = "2" } };
        var current = original with { Estimate = original.Estimate! with { Unit = "ManDays" } };
        var review = new PlanningTaskFormReview(original, local, new(Guid.NewGuid(), current, true, false));
        Assert.False(review.Effort.IsResolved);
        review.Effort.SetResult(new("3", "Hours"));
        var result = Assert.IsType<PlanningTaskEditorInput.Structure>(review.Candidate());
        Assert.Equal("3", result.Estimate!.Effort);
        Assert.Equal("Hours", result.Estimate.Unit);
    }

    [Fact]
    public void Unset_cost_is_distinct_from_zero_and_money_currency_is_coupled() {
        var original = Original();
        var local = original with { Estimate = original.Estimate! with { Cost = "0" } };
        var current = original with { Estimate = original.Estimate! with { Cost = "5", Currency = "EUR" } };
        var review = new PlanningTaskFormReview(original, local, new(Guid.NewGuid(), current, true, false));
        Assert.False(review.Money.IsResolved);
        review.Money.UseLocal();
        Assert.Equal(new("0", "USD"), review.Money.Result);
    }

    [Fact]
    public void Equivalent_valid_numbers_and_dates_compare_semantically_while_raw_local_spelling_is_retained() {
        var original = Original();
        var local = original with { Estimate = original.Estimate! with { Effort = "8.00" }, Due = "2026-11-02T14:00:00.1234567+02:00" };
        var review = new PlanningTaskFormReview(original, local, new(Guid.NewGuid(), original, true, false));
        Assert.False(review.Effort.LocalChanged);
        Assert.False(review.Due!.LocalChanged);
        var result = Assert.IsType<PlanningTaskEditorInput.Structure>(review.Candidate());
        Assert.Equal(local.Estimate, result.Estimate);
        Assert.Equal(local.Due, result.Due);
    }

    [Fact]
    public void A_new_complete_assignment_set_cannot_be_collapsed_by_a_local_single_assignee() {
        var original = Original();
        var local = original with { Assignee = new(ProjectStructureTaskResourceKind.Person, Guid.NewGuid()) };
        var review = new PlanningTaskFormReview(original, local, new(Guid.NewGuid(), original, false, false));
        Assert.False(review.CanApply);
        review.Assignee.UseCurrent();
        Assert.True(review.CanApply);
        Assert.Null(Assert.IsType<PlanningTaskEditorInput.Structure>(review.Candidate()).Assignee);
    }

    [Fact]
    public void Saved_execution_retains_historical_cost_until_current_cost_is_explicitly_chosen() {
        var original = Original();
        var local = original with { Estimate = original.Estimate! with { Cost = "5" } };
        var current = original with { Execution = new(ProjectTaskExecutionState.Started, "2026-11-02T12:00", "") };
        var review = new PlanningTaskFormReview(original, local, new(Guid.NewGuid(), current, true, false));
        Assert.False(review.CanApply);
        review.Money.UseCurrent();
        Assert.True(review.CanApply);
        Assert.Equal(current.Execution, Assert.IsType<PlanningTaskEditorInput.Structure>(review.Candidate()).Execution);
    }

    [Fact]
    public void Delivery_interval_is_coupled_and_projected_schedule_must_be_preserved() {
        var structure = Original();
        var original = new PlanningTaskEditorInput.Gantt("Original", "2026-11-02T12:00", "2026-11-02T20:00", "8", "-1",
            structure.Estimate, structure.Execution, null, null, null);
        var local = original with { Start = "2026-11-02T13:00", End = "2026-11-02T21:00" };
        var current = original with { End = "2026-11-02T22:00", Duration = "10" };
        var review = new PlanningTaskFormReview(original, local, new(Guid.NewGuid(), current, true, true));
        Assert.False(review.Delivery!.IsResolved);
        review.Delivery.UseLocal();
        Assert.False(review.CanApply);
        review.Delivery.UseCurrent();
        var result = Assert.IsType<PlanningTaskEditorInput.Gantt>(review.Candidate());
        Assert.Equal(current.Start, result.Start);
        Assert.Equal(current.End, result.End);
        Assert.Equal(current.Duration, result.Duration);
    }

    [Fact]
    public void A_review_requires_mounted_children_and_matching_editor_shapes() {
        var original = Original();
        Assert.Throws<ArgumentException>(() => new PlanningTaskFormReview(original, original with { Estimate = null },
            new(Guid.NewGuid(), original, true, false)));
        Assert.Throws<ArgumentException>(() => new PlanningTaskFormReview(original, original,
            new(Guid.NewGuid(), new PlanningTaskEditorInput.Gantt("", "", "", "", "", null, null, null, null, null), true, false)));
    }
}
