using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Prompts.UiSandbox;
using CanDoItAll.Prompts.UiSandbox.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.Prompts;

public sealed class PromptsSandboxTests : IDisposable
{
    private static readonly string[] ForbiddenReferenceFragments =
    [
        "CanDoItAll.Modules.Prompts,",
        "CanDoItAll.Modules.AgentFramework",
        "CanDoItAll.AgentFramework",
        "CanDoItAll.Infrastructure",
        "CanDoItAll.Web",
        "CanDoItAll.Composition",
        "CanDoItAll.AppComponents",
        "EntityFrameworkCore"
    ];

    private readonly BunitContext context = new();

    public PromptsSandboxTests()
    {
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddLogging();
        context.Services.AddCanDoItAllBaseLib();
    }

    [Theory]
    [InlineData("normal", "prompt-gallery-grid")]
    [InlineData("long-data", "prompt-gallery-next-page")]
    [InlineData("error", "prompt-gallery-retry")]
    [InlineData("editor-new", "prompt-gallery-item-editor")]
    [InlineData("editor-edit", "prompt-gallery-editor-versions")]
    [InlineData("editor-missing", "prompt-gallery-editor-retry")]
    [InlineData("editor-rejected", "prompt-gallery-editor-warning")]
    [InlineData("editor-unknown-result", "prompt-gallery-editor-warning")]
    [InlineData("picker", "prompt-gallery-actual-model-filter")]
    [InlineData("compatibility", "prompt-compatibility-insert-suppress")]
    [InlineData("compatibility-blocked", "prompt-compatibility-cancel")]
    public void Query_restores_scenarios_with_the_real_surfaces(string scenario, string testId)
    {
        var cut = Render(scenario);

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find($"[data-testid='{testId}']")));
        Assert.Equal(scenario, cut.Find("[data-testid='prompts-sandbox-frame']").GetAttribute("data-scenario"));
    }

    [Theory]
    [InlineData("loading", "Searching reusable prompts")]
    [InlineData("empty", "No prompt items match these filters")]
    [InlineData("editor-loading", "Loading prompt item")]
    [InlineData("editor-busy", "aria-busy=\"true\"")]
    public void Query_restores_loading_empty_and_busy_presentations(string scenario, string expected)
    {
        var cut = Render(scenario);

        cut.WaitForAssertion(() => Assert.Contains(expected, cut.Markup, StringComparison.Ordinal));
    }

    [Fact]
    public void Favorite_busy_scenario_disables_only_the_busy_favorite_button()
    {
        var cut = Render("favorite-busy");

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid='prompt-gallery-grid']")));
        var buttons = cut.FindAll("[data-testid='prompt-gallery-favorite']");
        Assert.True(buttons[0].HasAttribute("disabled"));
        Assert.False(buttons[1].HasAttribute("disabled"));
    }

    [Fact]
    public void Compatibility_blocked_scenario_offers_no_insert_path()
    {
        var cut = Render("compatibility-blocked");

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid='prompt-compatibility-cancel']")));
        Assert.Empty(cut.FindAll("[data-testid='prompt-compatibility-insert']"));
        Assert.Empty(cut.FindAll("[data-testid='prompt-compatibility-insert-suppress']"));
    }

    [Fact]
    public void Interactions_update_local_state_and_the_intent_line_only()
    {
        var cut = Render("normal");
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid='prompt-gallery-grid']")));

        cut.Find("[data-testid='prompt-gallery-favorite']").Click();
        cut.WaitForAssertion(() => Assert.Contains("favorite", cut.Find("[data-testid='prompts-sandbox-intent']").TextContent, StringComparison.OrdinalIgnoreCase));

        cut.Find("[data-testid='prompt-gallery-kind-filter']").Change("Part");
        cut.WaitForAssertion(() => Assert.Contains("Filters changed", cut.Find("[data-testid='prompts-sandbox-intent']").TextContent, StringComparison.Ordinal));

        cut.Find("[data-testid='prompt-gallery-edit']").Click();
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid='prompt-gallery-item-editor']")));
        cut.Find("[data-testid='prompt-gallery-editor-cancel']").Click();
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("[data-testid='prompt-gallery-item-editor']")));
    }

    [Fact]
    public void Sandbox_assembly_references_no_backend_module_or_host()
    {
        var references = typeof(PromptsSandboxAssets).Assembly.GetReferencedAssemblies().Select(reference => reference.FullName).ToArray();

        Assert.Contains(references, reference => reference.StartsWith("CanDoItAll.Prompts.UI", StringComparison.Ordinal));
        Assert.All(references, reference =>
            Assert.DoesNotContain(ForbiddenReferenceFragments, fragment => reference.Contains(fragment, StringComparison.Ordinal)));
    }

    public void Dispose() => context.Dispose();

    private IRenderedComponent<Routes> Render(string scenario)
    {
        context.Services.GetRequiredService<NavigationManager>().NavigateTo($"/prompt-gallery?scenario={scenario}&layout=matched");
        return context.Render<Routes>();
    }
}
