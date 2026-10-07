using Bunit;
using CanDoItAll.AgentFramework.CapabilityAuthoring.UiSandbox;
using CanDoItAll.Components.BaseLib;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.AgentFramework.CapabilityAuthoring.UI.Tests;

public sealed class CapabilityAuthoringFormTests {
    [Fact]
    public async Task Setup_completion_updates_the_parent_and_later_input_invalidates_the_badge() {
        using var context = CreateContext();
        var fixture = new CapabilityAuthoringScenario(AuthoringScenario.ToolHttp);
        var cut = context.Render<CapabilityAuthoringForm>(p => p.Add(x => x.Operations, fixture.Operations)
            .Add(x => x.Mode, fixture.Mode).Add(x => x.CapabilityId, fixture.CapabilityId));
        await cut.InvokeAsync(() => cut.FindComponent<Tabs>().Instance.SelectedIndexChanged.InvokeAsync(1));
        await cut.Find("[data-testid='agents-capability-details-test-setup']").ClickAsync();
        cut.WaitForAssertion(() => Assert.Contains("Setup passed", cut.Markup, StringComparison.Ordinal));
        Assert.False(cut.Find("[data-testid='agents-capability-details-save']").HasAttribute("disabled"));
        cut.Find("[data-testid='agents-capability-details-tool-test-input']").Input("{\"later\":true}");
        Assert.DoesNotContain("Setup passed", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("previous diagnostic does not apply", cut.Markup, StringComparison.Ordinal);
        Assert.Equal(1, fixture.Setups);
    }

    [Fact]
    public void Loaded_identity_uses_model_values() {
        using var context = CreateContext();
        var fixture = new CapabilityAuthoringScenario(AuthoringScenario.ToolHttp);
        var cut = context.Render<CapabilityAuthoringForm>(p => p.Add(x => x.Operations, fixture.Operations)
            .Add(x => x.Mode, fixture.Mode).Add(x => x.CapabilityId, fixture.CapabilityId));
        Assert.Equal("Fixture capability", cut.Find("[data-testid='agents-capability-details-name']").GetAttribute("value"));
        Assert.Equal("fixture", cut.Find("[data-testid='agents-capability-details-key']").GetAttribute("value"));
    }

    [Fact]
    public async Task Invalid_numeric_text_survives_tab_changes_and_blocks_save_before_blur() {
        using var context = CreateContext();
        var fixture = new CapabilityAuthoringScenario(AuthoringScenario.ToolHttp);
        var cut = context.Render<CapabilityAuthoringForm>(p => p.Add(x => x.Operations, fixture.Operations)
            .Add(x => x.Mode, fixture.Mode).Add(x => x.CapabilityId, fixture.CapabilityId));
        await cut.InvokeAsync(() => cut.FindComponent<Tabs>().Instance.SelectedIndexChanged.InvokeAsync(1));
        cut.Find("[data-testid='agents-capability-details-tool-http-timeout']").Input("invalid");
        await cut.InvokeAsync(() => cut.FindComponent<Tabs>().Instance.SelectedIndexChanged.InvokeAsync(2));
        await cut.InvokeAsync(() => cut.FindComponent<Tabs>().Instance.SelectedIndexChanged.InvokeAsync(1));
        Assert.Equal("invalid", cut.Find("[data-testid='agents-capability-details-tool-http-timeout']").GetAttribute("value"));
        await cut.Find("form").SubmitAsync();
        Assert.Equal(0, fixture.Saves);
        Assert.Contains("Timeout", cut.Markup, StringComparison.OrdinalIgnoreCase);
    }

    private static BunitContext CreateContext() {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddCanDoItAllBaseLib();
        return context;
    }
}
