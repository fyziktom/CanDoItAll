using Bunit;
using CanDoItAll.Application.UI.Runtime;
using CanDoItAll.Application.UiSandbox.Components;
using CanDoItAll.Components.BaseLib;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.ApplicationUI;

public sealed class RuntimeSurfaceTests : BunitContext {
    public RuntimeSurfaceTests() {
        Services.AddCanDoItAllBaseLib();
    }

    [Fact]
    public async Task Real_capability_children_preserve_status_facts_and_refresh_is_explicit() {
        var cut = Render<RuntimePage>();
        Assert.Contains("Startup is blocked", cut.Find("[data-testid='runtime-readiness-summary']").TextContent);
        Assert.Contains("Misconfigured", cut.Find("[data-testid='runtime-capability-core']").TextContent);
        Assert.Contains("Unsupported", cut.Find("[data-testid='runtime-capability-terminal']").TextContent);
        Assert.Contains("Unverified", cut.Find("[data-testid='runtime-capability-vault']").TextContent);
        Assert.Contains("Unavailable", cut.Find("[data-testid='runtime-capability-worker']").TextContent);
        Assert.Equal(19, cut.FindComponents<CapabilityFact>().Count);
        Assert.Contains("ScenarioVault 2.0", cut.Markup);
        Assert.Contains("Operating System", cut.Markup);
        Assert.Contains("Validate the actual host.", cut.Markup);
        Assert.Equal("Scenario refreshes: 0", cut.Find("[data-testid='runtime-scenario-refreshes']").TextContent);

        await cut.Find("[data-testid='runtime-capabilities-refresh']").ClickAsync(new());
        Assert.Contains("Mandatory runtime capabilities are ready", cut.Find("[data-testid='runtime-readiness-summary']").TextContent);
        Assert.Contains("Available", cut.Find("[data-testid='runtime-capability-core']").TextContent);
        Assert.Equal("Scenario refreshes: 1", cut.Find("[data-testid='runtime-scenario-refreshes']").TextContent);
    }
}
