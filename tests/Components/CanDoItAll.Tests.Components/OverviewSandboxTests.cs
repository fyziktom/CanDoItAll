using System.Text.Json;
using Bunit;
using Bunit.TestDoubles;
using CanDoItAll.AgentFramework.UI.Catalog;
using CanDoItAll.AgentFramework.UI.Overview;
using CanDoItAll.AgentFramework.UiSandbox;
using CanDoItAll.AgentFramework.UiSandbox.Components;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Components.Charts;
using CanDoItAll.Modules.AgentFramework;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.AgentFramework;

public sealed class OverviewSandboxTests : IDisposable {
    private readonly BunitContext context = new();

    public OverviewSandboxTests() {
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddLogging();
        context.Services.AddCanDoItAllBaseLib();
        context.Services.AddCanDoItAllCharts();
        using var stream = typeof(CatalogAssets).Assembly.GetManifestResourceStream("CatalogFixture.json")!;
        context.Services.AddSingleton(JsonSerializer.Deserialize<AgentCatalogSnapshot>(stream, new JsonSerializerOptions(JsonSerializerDefaults.Web))!);
    }

    [Fact]
    public void Overview_query_renders_real_charts_without_production_services() {
        var cut = Render("baseline");
        Assert.Null(context.Services.GetService<IAgentsWorkspaceQuery>());
        Assert.Single(cut.FindComponents<AgentsOverviewSurface>());
        Assert.NotEmpty(cut.FindComponents<CdaChart>());
        Assert.All(cut.FindComponents<CdaChart>(), chart => Assert.NotEmpty(chart.Instance.Series));
        Assert.Contains("Delivery governance", cut.Find("[data-testid='agents-overview-top-consumers']").TextContent);
    }

    [Theory]
    [InlineData("baseline", "Delivery governance")]
    [InlineData("loading", "Usage ranking is loading")]
    [InlineData("initial-failure", "Agent runtime summary could not be loaded")]
    [InlineData("stale-overview", "Showing the previously accepted agent summary")]
    [InlineData("empty", "No consumers are ranked")]
    [InlineData("ready", "Product research")]
    [InlineData("header-partial", "The sample HR agent is unavailable")]
    [InlineData("bound-unavailable", "The sample bound-resource count is unavailable")]
    [InlineData("usage-loading", "Usage ranking is loading")]
    [InlineData("stale-usage", "Showing previously accepted usage for this scope")]
    [InlineData("scope-pending", "Usage ranking is loading")]
    [InlineData("wrong-scope", "did not match the requested scope")]
    [InlineData("partial", "Displayed totals are partial")]
    [InlineData("unknown-unpriced", "1 unpriced")]
    [InlineData("long-content", "deliberately long")]
    [InlineData("detail-pending", "Delivery governance")]
    public void Named_states_render_their_explicit_public_presentation(string scenario, string text) {
        var cut = Render(scenario);
        Assert.Contains(text, cut.Find("[data-testid='sandbox-overview-specimen']").TextContent);
        var state = cut.FindComponent<AgentsOverviewSurface>().Instance.State;
        Assert.Equal(!state.CanOpen(AgentsOverviewDetail.Models), cut.Find("[data-testid='agents-overview-open-model-usage']").HasAttribute("disabled"));
    }

    [Theory]
    [InlineData("agents", "Agents")]
    [InlineData("chats", "Chats")]
    [InlineData("both", "Both")]
    public async Task Scope_intent_updates_sample_and_replace_history_query(string scope, string label) {
        var cut = Render("ready", scope == "both" ? "agents" : "both");
        await cut.FindAll("[data-testid='agents-overview-usage-scope'] button").Single(button => button.TextContent.Trim() == label).ClickAsync();
        var navigation = Assert.IsType<BunitNavigationManager>(context.Services.GetRequiredService<NavigationManager>());
        cut.WaitForAssertion(() => {
            Assert.Contains("usageScope=" + scope, navigation.Uri);
            Assert.True(navigation.History.First().Options.ReplaceHistoryEntry);
            Assert.Contains("No query runs", cut.Find("[data-testid='sandbox-intent']").TextContent);
            Assert.True(cut.FindComponent<AgentsOverviewSurface>().Instance.State.HasUsage);
        });
    }

    [Theory]
    [InlineData("initial-failure", "agents-overview-retry", "agents-overview-load-error")]
    [InlineData("wrong-scope", "agents-overview-usage-retry", "agents-overview-usage-error")]
    [InlineData("stale-usage", "agents-overview-usage-retry", "agents-overview-usage-stale")]
    public async Task Retry_clears_only_sample_failure_without_io(string scenario, string action, string warning) {
        var cut = Render(scenario);
        Assert.NotNull(cut.Find($"[data-testid='{warning}']"));
        await cut.Find($"[data-testid='{action}']").ClickAsync();
        Assert.Empty(cut.FindAll($"[data-testid='{warning}']"));
        Assert.Contains("No query runs", cut.Find("[data-testid='sandbox-intent']").TextContent);
        Assert.Null(context.Services.GetService<IAgentsWorkspaceQuery>());
    }

    [Fact]
    public async Task Detail_and_team_intents_log_without_production_navigation() {
        var cut = Render("ready");
        var navigation = context.Services.GetRequiredService<NavigationManager>();
        var before = navigation.Uri;
        foreach (var kind in new[] { "agent", "provider", "model" }) {
            await cut.Find($"[data-testid='agents-overview-open-{kind}-usage']").ClickAsync();
            Assert.Contains("No data-loading dialog opens", cut.Find("[data-testid='sandbox-intent']").TextContent);
            Assert.True(cut.Find($"[data-testid='agents-overview-open-{kind}-usage']").HasAttribute("disabled"));
        }
        await cut.FindAll("[data-testid='agents-overview-team-shortcut']").First().ClickAsync();
        Assert.Contains("No production navigation", cut.Find("[data-testid='sandbox-intent']").TextContent);
        Assert.Equal(before, navigation.Uri);
    }

    [Theory]
    [InlineData("sandbox-catalog", "sandbox-normal")]
    [InlineData("sandbox-capabilities", "agents-capabilities-panel")]
    public async Task Existing_catalog_and_capabilities_specimens_remain_available(string action, string target) {
        var cut = Render("ready");
        await cut.Find($"[data-testid='{action}']").ClickAsync();
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find($"[data-testid='{target}']")));
        Assert.Empty(cut.FindAll("[data-testid='sandbox-overview-specimen']"));
    }

    private IRenderedComponent<Routes> Render(string scenario, string scope = "both") {
        context.Services.GetRequiredService<NavigationManager>().NavigateTo($"/agents?specimen=overview&scenario={scenario}&usageScope={scope}");
        var cut = context.Render<Routes>();
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid='agents-overview-dashboard']")));
        return cut;
    }

    public void Dispose() => context.Dispose();
}
