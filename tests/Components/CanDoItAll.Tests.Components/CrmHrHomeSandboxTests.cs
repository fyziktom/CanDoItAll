using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.CrmHr.UI.Home;
using CanDoItAll.CrmHr.UiSandbox;
using CanDoItAll.CrmHr.UiSandbox.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.CrmHr;

public sealed class CrmHrHomeSandboxTests : IDisposable
{
    private readonly BunitContext context = new();

    public CrmHrHomeSandboxTests()
    {
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddLogging();
        context.Services.AddCanDoItAllBaseLib();
    }

    [Theory]
    [InlineData("populated", "crmhr-home-opportunity-item")]
    [InlineData("loading", "crmhr-home-loading")]
    [InlineData("empty", "crmhr-home-directory-empty")]
    [InlineData("failed", "crmhr-home-retry")]
    [InlineData("sensitive", "crmhr-home-sensitive-item")]
    [InlineData("long-text", "crmhr-home-directory-item")]
    [InlineData("null-optionals", "crmhr-home-sensitive-empty")]
    [InlineData("large-totals", "crmhr-home-sensitive-card")]
    public void Query_restores_scenarios_with_the_real_surface(string scenario, string testId)
    {
        var cut = Render(scenario);

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find($"[data-testid='{testId}']")));
        Assert.Equal(scenario, cut.Find("[data-testid='crmhr-sandbox-frame']").GetAttribute("data-scenario"));
        Assert.NotNull(cut.FindComponent<CrmHrHomeSurface>());
    }

    [Fact]
    public void Large_totals_scenario_shows_totals_above_the_preview_caps()
    {
        var cut = Render("large-totals");

        cut.WaitForAssertion(() => Assert.Contains("1284", cut.Find("[data-testid='crmhr-home-stat-parties']").TextContent, StringComparison.Ordinal));
        Assert.Equal(5, cut.FindAll("[data-testid='crmhr-home-directory-item']").Count);
        Assert.Equal(3, cut.FindAll("[data-testid='crmhr-home-sensitive-item']").Count);
        Assert.Equal(6, cut.FindAll("[data-testid='crmhr-home-opportunity-item']").Count);
        Assert.Contains("41 sensitive record(s)", cut.Find("[data-testid='crmhr-home-sensitive-card']").TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void Long_text_scenario_renders_markup_looking_values_as_text()
    {
        var cut = Render("long-text");

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid='crmhr-home-directory-item']")));
        Assert.Empty(cut.FindAll("script"));
        Assert.Empty(cut.FindAll("img"));
        Assert.Contains("&lt;script&gt;", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Interactions_update_the_intent_line_only_and_retry_resolves_the_failed_scenario_locally()
    {
        var cut = Render("failed");
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid='crmhr-home-retry']")));

        cut.Find("[data-testid='crmhr-home-route-workforce']").Click();
        cut.WaitForAssertion(() => Assert.Contains("Navigate: Workforce", cut.Find("[data-testid='crmhr-sandbox-intent']").TextContent, StringComparison.Ordinal));

        cut.Find("[data-testid='crmhr-home-retry']").Click();
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid='crmhr-home-opportunity-item']")));
        Assert.Contains("Retry: generation", cut.Find("[data-testid='crmhr-sandbox-intent']").TextContent, StringComparison.Ordinal);

        cut.Find("[data-testid='crmhr-home-opportunity-open']").Click();
        cut.WaitForAssertion(() => Assert.Contains("Open opportunity: account", cut.Find("[data-testid='crmhr-sandbox-intent']").TextContent, StringComparison.Ordinal));
        Assert.Equal("failed", cut.Find("[data-testid='crmhr-sandbox-frame']").GetAttribute("data-scenario"));
    }

    // The sandbox composes the rendering library over the same dependency categories the library itself may use (UI
    // primitives, shared UI families, lightweight contracts) plus its own ASP.NET Core host. No backend module,
    // persistence, runtime or production composition assembly reaches it, directly or transitively.
    [Fact]
    public void Sandbox_assembly_references_no_backend_module_or_host()
    {
        var sandbox = typeof(CrmHrSandboxAssets).Assembly;
        var references = sandbox.GetReferencedAssemblies().Select(reference => reference.Name ?? string.Empty).ToArray();

        Assert.Contains(CrmHrUiBoundary.RenderingLibrary.GetName().Name, references);
        Assert.All(references, name => Assert.True(
            name == CrmHrUiBoundary.RenderingLibrary.GetName().Name ||
            CrmHrUiBoundary.IsAllowedDirectReference(name) ||
            name.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal),
            $"The sandbox references '{name}', which is outside the rendering library's dependency categories."));
        Assert.DoesNotContain(CrmHrUiBoundary.TransitiveReferenceNames(sandbox), CrmHrUiBoundary.IsForbidden);
    }

    public void Dispose() => context.Dispose();

    private IRenderedComponent<Routes> Render(string scenario)
    {
        context.Services.GetRequiredService<NavigationManager>().NavigateTo($"/crm-hr?scenario={scenario}&layout=matched");
        return context.Render<Routes>();
    }
}
