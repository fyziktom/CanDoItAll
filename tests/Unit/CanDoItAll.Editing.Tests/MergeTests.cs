using CanDoItAll.Editing;
using Xunit;

namespace CanDoItAll.Editing.Tests;

public sealed class MergeTests {
    [Theory]
    [InlineData("base", "base", "base", "base")]
    [InlineData("base", "mine", "base", "mine")]
    [InlineData("base", "base", "remote", "remote")]
    [InlineData("base", "same", "same", "same")]
    [InlineData(null, "", null, "")]
    [InlineData("", null, "", null)]
    [InlineData(null, null, "remote", "remote")]
    [InlineData("base", null, null, null)]
    public void Scalar_comparison_preserves_null_empty_and_equal_changes(string? b, string? l, string? r, string? expected) {
        var field = new MergeField<string?>(b, l, r, StringComparer.Ordinal);
        Assert.True(field.IsResolved);
        Assert.Equal(expected, field.Result);
    }

    [Fact]
    public void Overlapping_changes_require_an_explicit_choice_and_never_modify_the_inputs() {
        var field = new MergeField<string>("base", "mine", "remote");
        Assert.False(field.IsResolved);
        Assert.Throws<InvalidOperationException>(() => field.Result);
        field.UseCurrent();
        Assert.Equal("remote", field.Result);
        field.UseLocal();
        Assert.Equal("mine", field.Result);
        field.SetResult("reviewed");
        Assert.Equal(MergeChoice.Custom, field.Choice);
        Assert.Equal("reviewed", field.Result);
        Assert.Equal("base", field.Original);
        Assert.Equal("mine", field.Local);
        Assert.Equal("remote", field.Current);
    }

    [Fact]
    public void Membership_preserves_removal_and_independent_additions_in_domain_equality() {
        var result = CollectionMerge.Membership<string>(["common", "gone"], ["COMMON", "new"],
            ["common", "gone", "remote"], StringComparer.OrdinalIgnoreCase);
        Assert.Equal(new[] { "COMMON", "new", "remote" }, result);
    }

    [Fact]
    public void Keyed_rows_distinguish_delete_edit_and_add_add_conflicts() {
        var rows = CollectionMerge.ByKey<Row, Row>(
            [new("edited", "base")], [], [], value => value);
        Assert.False(Assert.Single(rows).Field.Result.Exists);

        var conflict = CollectionMerge.ByKey<string, Row>([new("one", "base")],
            [new("two", "mine")], [new("one", "changed"), new("two", "remote")], value => value.Id);
        Assert.False(conflict[0].Field.IsResolved);
        Assert.False(conflict[0].Field.Local.Exists);
        Assert.True(conflict[0].Field.Current.Exists);
        Assert.False(conflict[1].Field.IsResolved);
        Assert.False(conflict[1].Field.Original.Exists);
        conflict[0].Field.UseCurrent();
        conflict[1].Field.UseLocal();
        Assert.Equal("changed", conflict[0].Field.Result.Value!.Value);
        Assert.Equal("mine", conflict[1].Field.Result.Value!.Value);
    }

    [Fact]
    public void Keyed_merge_uses_identity_and_preserves_foreign_insertions() {
        var rows = CollectionMerge.ByKey<string, Row>([new("a", "base"), new("b", "base")],
            [new("b", "local"), new("a", "base")],
            [new("c", "remote"), new("a", "remote"), new("b", "base")], value => value.Id);
        Assert.Equal(new[] { "a", "b", "c" }, rows.Select(row => row.Key));
        Assert.Equal(new[] { "remote", "local", "remote" }, rows.Select(row => row.Field.Result.Value!.Value));
    }

    [Fact]
    public void Duplicate_keys_and_oversize_collections_are_explicit_adapter_errors() {
        Assert.Throws<ArgumentException>(() => CollectionMerge.ByKey<string, Row>([],
            [new("a", "first"), new("a", "second")], [], value => value.Id));
        Assert.Throws<ArgumentException>(() => CollectionMerge.ByKey<string, Row>([],
            [new("a", "first"), new("b", "second")], [], value => value.Id, maximumRows: 1));
    }

    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public void Disjoint_text_hunks_preserve_unicode_line_endings_and_the_final_newline(string newline) {
        var original = string.Join(newline, "α", "middle", "終", "");
        var local = string.Join(newline, "α local", "middle", "終", "");
        var current = string.Join(newline, "α", "middle", "終 remote", "");
        var merged = TextMerge.Compare(original, local, current);
        Assert.Equal(string.Join(newline, "α local", "middle", "終 remote", ""), merged.Field.Result);
        Assert.Equal(0, merged.LocalChange!.StartLine);
        Assert.Equal(2, merged.CurrentChange!.StartLine);
    }

    [Fact]
    public void Text_overlap_and_simultaneous_insertion_require_a_decision() {
        Assert.False(TextMerge.Compare("same\nsame\nsame", "same\nmine\nsame", "same\ntheirs\nsame").Field.IsResolved);
        Assert.False(TextMerge.Compare("line", "mine\nline", "theirs\nline").Field.IsResolved);
    }

    [Fact]
    public void Text_deletion_and_insertion_keep_the_entire_document() {
        var result = TextMerge.Compare("a\nb\nc\n", "b\nc\n", "a\nb\nc\nd");
        Assert.Equal("b\nc\nd", result.Field.Result);
    }

    [Fact]
    public void Large_and_many_line_inputs_require_manual_review_without_truncation() {
        var original = new string('b', TextMerge.MaximumAutomaticCharacters + 1);
        var local = original + "local";
        var current = original + "remote";
        var result = TextMerge.Compare(original, local, current);
        Assert.True(result.RequiresManualComparison);
        Assert.False(result.Field.IsResolved);
        result.Field.UseLocal();
        Assert.Equal(local, result.Field.Result);
        var lines = string.Concat(Enumerable.Repeat("x\n", TextMerge.MaximumAutomaticLines + 1));
        Assert.True(TextMerge.Compare(lines, "first\n" + lines, lines + "last").RequiresManualComparison);
    }

    [Fact]
    public void Core_assembly_has_no_module_framework_or_infrastructure_dependencies() {
        var references = typeof(MergeField<>).Assembly.GetReferencedAssemblies().Select(name => name.Name!).ToArray();
        Assert.All(references, name => Assert.StartsWith("System.", name));
    }

    private sealed record Row(string Id, string Value);
}
