using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Workbench.Structure.UI;

namespace CanDoItAll.Workbench.Structure.UI.Tests;

public sealed class StructureNodeComparisonRendererTests {
    [Fact]
    public async Task Unresolved_choice_renders_all_three_values_and_changes_only_the_local_result() {
        await using var context = Context();
        var schema = new StructureNodeFormSchema([new() { Key = "choice", Label = "Choice", InputMode = "select",
            Options = [new() { Value = "original", Label = "Original choice" }, new() { Value = "local", Label = "Local choice" }, new() { Value = "peer", Label = "Peer choice" }] }]);
        StructureNodeFormValues Values(string choice) => schema.Read("Title", "", "", [new() { Key = "choice", Value = choice }]);
        var review = new StructureNodeFormReview(schema, Values("original"), Values("local"), Values("peer"));
        var rendered = context.Render<StructureNodeComparison>(parameters => parameters.Add(component => component.Review, review));
        Assert.Contains("Choose a result", rendered.Markup, StringComparison.Ordinal);
        Assert.False(review.CanApply);

        await rendered.InvokeAsync(() => rendered.FindAll("button").Single(button => button.TextContent.Trim() == "Use current").ClickAsync(new()));

        Assert.True(review.CanApply);
        Assert.Equal(new StructureNodeFieldValue.Choice("peer"), review.Candidate().Fields[new("choice")]);
        Assert.Equal(1, review.InputRevision);
    }

    [Fact]
    public async Task Unfinished_result_retains_its_input_component_when_unchanged_fields_are_hidden() {
        await using var context = Context();
        var schema = new StructureNodeFormSchema([new() { Key = "amount", Label = "Amount", InputMode = "number" }]);
        var values = schema.Read("Title", "", "", [new() { Key = "amount", Value = "2" }]);
        var review = new StructureNodeFormReview(schema, values, values, values);
        var rendered = context.Render<StructureNodeComparison>(parameters => parameters.Add(component => component.Review, review));
        await rendered.InvokeAsync(() => rendered.Find("button").ClickAsync(new()));
        var input = rendered.Find("[data-testid='structure-node-result-amount']");
        var control = rendered.FindComponents<TextInput>().Single(component => component.FindAll("[data-testid='structure-node-result-amount']").Count == 1).Instance;
        await rendered.InvokeAsync(() => input.InputAsync(new() { Value = "2e" }));
        Assert.True(review.HasPendingInput);
        Assert.False(review.CanApply);

        await rendered.InvokeAsync(() => rendered.Find("button").ClickAsync(new()));

        Assert.Same(control, rendered.FindComponents<TextInput>().Single(component => component.FindAll("[data-testid='structure-node-result-amount']").Count == 1).Instance);
        Assert.Equal("2e", rendered.Find("[data-testid='structure-node-result-amount']").GetAttribute("value"));
        Assert.Equal("2e", Assert.Single(review.Fields).ResultInput);
    }

    [Fact]
    public async Task Previous_custom_subtitle_remains_readable_for_copying_when_a_new_triple_requires_another_choice() {
        await using var context = Context();
        var schema = new StructureNodeFormSchema([]);
        var original = schema.Read("Title", "Original context", "Notes", []);
        var local = schema.Read("Title", "Local context", "Notes", []);
        var current = schema.Read("Title", "Peer context", "Notes", []);
        var previous = new StructureNodeFormReview(schema, original, local, current);
        previous.Subtitle.SetResult("Retained custom context");
        previous.RecordInput();
        var refreshed = new StructureNodeFormReview(schema, original, local, current with { Subtitle = "Third context" });
        refreshed.PreserveChoices(previous);
        Assert.False(refreshed.Subtitle.IsResolved);
        var rendered = context.Render<StructureNodeComparisonDialog>(parameters => parameters
            .Add(component => component.Review, refreshed).Add(component => component.Previous, previous));

        var copied = rendered.Find("[data-testid='structure-node-previous-subtitle']");

        Assert.Equal("Retained custom context", copied.GetAttribute("value"));
        Assert.True(copied.HasAttribute("readonly"));
    }

    private static BunitContext Context() {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddCanDoItAllBaseLib();
        return context;
    }
}
