using Bunit;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.WorkflowAuthoring.UI;
using CanDoItAll.Components.BaseLib;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.WorkflowAuthoring;

public sealed class WorkflowProviderSelectionTests {
    [Fact]
    public async Task Source_models_preserve_exact_routes_and_display_native_names() {
        await using var context = new BunitContext();
        context.Services.AddCanDoItAllBaseLib();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        var provider = new WorkflowProviderOption(Guid.NewGuid(), "Published source", ProviderKind.OpenAi,
            ProviderTransportKind.Responses, ProviderProfilePurpose.Chat, "Route", ["Route", "route", " route "],
            true, true, true, true, true, false) {
            IsSourceManaged = true,
            ModelCatalog = [new("Route", "Native default"), new("route", "Native lower case"), new(" route ", "Native spaced")]
        };
        string? accepted = null;
        var cut = context.Render<WorkflowProviderModelSelector>(p => p.Add(x => x.WorkflowProvider, provider)
            .Add(x => x.Value, "route").Add(x => x.ValueChanged, value => accepted = value));
        Assert.Equal(["Provider default (Native default)", "Native lower case", "Native spaced"],
            cut.FindAll("option").Select(option => option.TextContent));
        Assert.Equal("1", cut.Find("select").GetAttribute("value"));
        await cut.InvokeAsync(() => cut.Find("select").Change("2"));
        Assert.Equal(" route ", accepted);
        await cut.InvokeAsync(() => cut.Find("select").Change("0"));
        Assert.Equal("Route", accepted);
    }
}
