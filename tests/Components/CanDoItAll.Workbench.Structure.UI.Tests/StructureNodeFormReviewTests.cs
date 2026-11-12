using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Editing;
using CanDoItAll.Workbench.Structure.UI;

namespace CanDoItAll.Workbench.Structure.UI.Tests;

public sealed class StructureNodeFormReviewTests {
    private static StructureNodeFormSchema Schema() => new([new() { Key = "amount", Label = "Amount", InputMode = "number" }]);

    private static StructureNodeFormValues Values(StructureNodeFormSchema schema, string amount,
        string title = "Title", string notes = "first\nmiddle\nlast") => schema.Read(title, "", notes, [new() { Key = "amount", Value = amount }]);

    [Fact]
    public void Independent_text_changes_and_equivalent_numbers_combine_without_mutating_any_source() {
        var schema = Schema();
        var original = Values(schema, "2.00");
        var local = Values(schema, "2", notes: "first\nmiddle\nlocal last");
        var current = Values(schema, "3", notes: "peer first\nmiddle\nlast");
        var review = new StructureNodeFormReview(schema, original, local, current);

        var candidate = review.Candidate();

        Assert.Equal("peer first\nmiddle\nlocal last", candidate.Notes);
        Assert.Equal(new StructureNodeFieldValue.Number(3), candidate.Fields[new("amount")]);
        Assert.Equal("first\nmiddle\nlast", original.Notes);
        Assert.Equal("first\nmiddle\nlocal last", local.Notes);
        Assert.Equal("peer first\nmiddle\nlast", current.Notes);
    }

    [Fact]
    public void Conflicting_scalar_requires_an_explicit_choice_and_unfinished_result_is_retained() {
        var schema = Schema();
        var review = new StructureNodeFormReview(schema, Values(schema, "2"), Values(schema, "4"), Values(schema, "3"));
        var field = Assert.Single(review.Fields);
        Assert.Equal(MergeChoice.Unresolved, field.Scalar!.Choice);
        Assert.False(review.CanApply);
        field.SetResultInput("2e");
        Assert.True(review.HasPendingInput);
        Assert.Equal("2e", field.ResultInput);
        Assert.Throws<InvalidOperationException>(() => review.Candidate());
        field.SetResultInput("5.00");
        Assert.True(review.CanApply);
        Assert.Equal(new StructureNodeFieldValue.Number(5), review.Candidate().Fields[new("amount")]);
    }

    [Fact]
    public void Choice_carry_requires_an_unchanged_triple_and_preserves_current_custom_text_for_copying() {
        var schema = Schema();
        var original = Values(schema, "2");
        var local = Values(schema, "4");
        var current = Values(schema, "3");
        var previous = new StructureNodeFormReview(schema, original, local, current);
        Assert.Single(previous.Fields).SetResultInput("5.00");
        var same = new StructureNodeFormReview(schema, original, local, current);
        same.PreserveChoices(previous);
        Assert.Equal(MergeChoice.Custom, Assert.Single(same.Fields).Scalar!.Choice);
        Assert.Equal("5.00", Assert.Single(same.Fields).ResultInput);
        var changed = new StructureNodeFormReview(schema, original, local, Values(schema, "6"));
        changed.PreserveChoices(previous);
        Assert.Equal(MergeChoice.Unresolved, Assert.Single(changed.Fields).Scalar!.Choice);
        Assert.Equal("5.00", Assert.Single(previous.Fields).ResultInput);
    }

    [Fact]
    public void Refresh_cannot_erase_unfinished_result_input() {
        var schema = Schema();
        var values = Values(schema, "2");
        var previous = new StructureNodeFormReview(schema, values, values, values);
        Assert.Single(previous.Fields).SetResultInput("-");
        var replacement = new StructureNodeFormReview(schema, values, values, values);
        Assert.Throws<InvalidOperationException>(() => replacement.PreserveChoices(previous));
        Assert.Equal("-", Assert.Single(previous.Fields).ResultInput);
    }
}
