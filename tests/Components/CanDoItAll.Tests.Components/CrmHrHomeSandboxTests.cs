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
    private static readonly string[] ForbiddenReferenceFragments =
    [
        "CanDoItAll.Modules.",
        "CanDoItAll.AgentFramework",
        "CanDoItAll.Infrastructure",
        "CanDoItAll.Web",
        "CanDoItAll.Composition",
        "CanDoItAll.AppComponents",
        "CanDoItAll.SharedKernel",
        "EntityFrameworkCore"
    ];

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

    [Fact]
    public void Sandbox_assembly_references_no_backend_module_or_host()
    {
        var references = typeof(CrmHrSandboxAssets).Assembly.GetReferencedAssemblies().Select(reference => reference.FullName).ToArray();

        Assert.Contains(references, reference => reference.StartsWith("CanDoItAll.CrmHr.UI", StringComparison.Ordinal));
        Assert.All(references, reference =>
            Assert.DoesNotContain(ForbiddenReferenceFragments, fragment => reference.Contains(fragment, StringComparison.Ordinal)));
    }

    public void Dispose() => context.Dispose();

    private IRenderedComponent<Routes> Render(string scenario)
    {
        context.Services.GetRequiredService<NavigationManager>().NavigateTo($"/crm-hr?scenario={scenario}&layout=matched");
        return context.Render<Routes>();
    }
}
