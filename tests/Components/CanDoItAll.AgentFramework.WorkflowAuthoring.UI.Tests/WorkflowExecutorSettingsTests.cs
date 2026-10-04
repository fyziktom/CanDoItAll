using System.Text.Json;
using CanDoItAll.AgentFramework.WorkflowAuthoring.UI;
using CanDoItAll.SharedKernel.Configuration;

namespace CanDoItAll.Tests.Components.WorkflowAuthoring;

public sealed class WorkflowExecutorSettingsTests {
    private static readonly ConfigurationSchema Schema = new("1", [
        new("limit", "Limit", ConfigurationFieldType.Number, false, string.Empty),
        new("label", "Label", ConfigurationFieldType.Text, false, string.Empty)
    ]);

    [Fact]
    public void Structured_edit_preserves_unknown_nested_values_case_and_unchanged_known_bytes() {
        const string original = """{ "Limit":2, "label":"  original  ", "future":{"items":[1,null,true]}, "Future":[{"n":2}] }""";
        var state = WorkflowExecutorConfigurationMapper.ReadState(original, Schema);
        state.SetText("limit", "4");
        var merged = WorkflowExecutorConfigurationMapper.MergeState(original, Schema, state);
        using var result = JsonDocument.Parse(merged);
        Assert.Equal(4, result.RootElement.GetProperty("Limit").GetInt32());
        Assert.Equal("  original  ", result.RootElement.GetProperty("label").GetString());
        Assert.Equal("{\"items\":[1,null,true]}", result.RootElement.GetProperty("future").GetRawText());
        Assert.Equal("[{\"n\":2}]", result.RootElement.GetProperty("Future").GetRawText());
    }

    [Fact]
    public void Unchanged_schema_projection_retains_original_json_exactly() {
        const string original = """{ "Limit": 2, "unknown": null }""";
        var state = WorkflowExecutorConfigurationMapper.ReadState(original, Schema);
        Assert.Equal(original, WorkflowExecutorConfigurationMapper.MergeState(original, Schema, state));
    }

    [Fact]
    public void Invalid_original_json_cannot_be_replaced_by_a_schema_edit() {
        var state = new ConfigurationState(new Dictionary<string, string> { ["limit"] = "4" });
        Assert.Throws<InvalidOperationException>(() => WorkflowExecutorConfigurationMapper.MergeState("{ unfinished", Schema, state));
    }

    [Fact]
    public void Ambiguous_case_variants_of_a_known_field_are_explicitly_rejected() {
        Assert.Throws<InvalidOperationException>(() => WorkflowExecutorConfigurationMapper.ReadState("""{"limit":2,"Limit":3}""", Schema));
    }
}
