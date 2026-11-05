using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Workbench.Structure.UI;

namespace CanDoItAll.Workbench.Structure.UI.Tests;

public sealed class StructureNodeFormSchemaTests {
    [Theory]
    [InlineData("email", "person@example.invalid")]
    [InlineData("tel", "+591 123456")]
    [InlineData("url", "https://example.invalid/retained")]
    public void Native_text_protocol_fields_remain_exact_typed_text(string mode, string text) {
        var schema = new StructureNodeFormSchema([new() { Key = "address", Label = "Address", InputMode = mode }]);
        var values = schema.Read("Title", "", "", [new() { Key = "address", Value = text }]);
        Assert.Equal(new StructureNodeFieldValue.Text(text), values.Fields[new("address")]);
        Assert.Equal(text, Assert.Single(schema.Write(values)).Value);
    }

    [Fact]
    public void Scalar_values_have_meaningful_types_and_original_schema_does_not_follow_caller_mutation() {
        var field = new CanvasWorkbenchInputField { Key = "amount", Label = "Amount", InputMode = "number" };
        var schema = new StructureNodeFormSchema([field]);
        field.Key = "later-key";
        var first = schema.Read("Title", "", "", [new() { Key = "amount", Value = "2.00" }]);
        var second = schema.Read("Title", "", "", [new() { Key = "amount", Value = "2" }]);
        Assert.Equal(first.Fields[new("amount")], second.Fields[new("amount")]);
        Assert.IsType<StructureNodeFieldValue.Number>(first.Fields[new("amount")]);
        Assert.Equal("2", Assert.Single(schema.Write(first)).Value);
    }

    [Theory]
    [InlineData("number", "2e")]
    [InlineData("date", "2026-11-")]
    [InlineData("datetime-local", "2026-11-02T2")]
    public void Unfinished_scalar_text_is_rejected_without_inventing_a_value(string kind, string raw) {
        var schema = new StructureNodeFormSchema([new() { Key = "value", Label = "Value", InputMode = kind }]);
        var input = new CanvasWorkbenchInputValue { Key = "value", Value = raw };
        Assert.Throws<ArgumentException>(() => schema.Read("Title", "", "", [input]));
        Assert.Equal(raw, input.Value);
    }

    [Fact]
    public void Unknown_modes_and_changed_or_duplicate_field_sets_are_explicit_errors() {
        Assert.Throws<ArgumentException>(() => new StructureNodeFormSchema([new() { Key = "custom", InputMode = "custom-unsupported" }]));
        var schema = new StructureNodeFormSchema([new() { Key = "original", Label = "Original" }]);
        Assert.Throws<ArgumentException>(() => schema.Read("Title", "", "", [new() { Key = "replacement", Value = "local" }]));
        Assert.Throws<ArgumentException>(() => new StructureNodeFormSchema([new() { Key = "same" }, new() { Key = "same" }]));
    }

    [Fact]
    public void Current_option_membership_is_validated_without_dropping_an_old_value() {
        var schema = new StructureNodeFormSchema([new() { Key = "choice", Label = "Choice", InputMode = "select",
            Options = [new() { Value = "current", Label = "Current choice" }] }]);
        var old = schema.Read("Title", "", "", [new() { Key = "choice", Value = "retired" }]);
        var definition = Assert.Single(schema.Fields);
        Assert.Equal("retired", definition.Format(old.Fields[new("choice")]));
        Assert.False(definition.IsValid(old.Fields[new("choice")]));
        Assert.True(definition.IsValid(definition.Parse("current")));
    }

    [Theory]
    [InlineData("2026-11-02T13:04", "2026-11-02T13:04")]
    [InlineData("2026-11-02T13:04:05", "2026-11-02T13:04:05")]
    [InlineData("2026-11-02T13:04:05.123", "2026-11-02T13:04:05.123")]
    [InlineData("2026-11-02T13:04:05.0001", "2026-11-02T13:04:05.0001")]
    public void Date_time_values_round_trip_without_a_timezone_conversion(string input, string expected) {
        var schema = new StructureNodeFormSchema([new() { Key = "date", Label = "Date", InputMode = "datetime-local" }]);
        var values = schema.Read("Title", "", "", [new() { Key = "date", Value = input }]);
        var date = Assert.IsType<StructureNodeFieldValue.Date>(values.Fields[new("date")]);
        Assert.Equal(DateTimeKind.Unspecified, date.Value!.Value.Kind);
        Assert.Equal(expected, Assert.Single(schema.Write(values)).Value);
    }
}
