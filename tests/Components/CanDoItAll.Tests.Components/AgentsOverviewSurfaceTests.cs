using System.Collections.Immutable;
using Bunit;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Usage;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Components.Charts;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.AgentFramework.UI.Overview;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.AgentFramework;

public sealed class AgentsOverviewSurfaceTests {
    [Fact]
    public void Real_overview_surface_renders_without_application_services() {
        using var context = Context();
        var cut = context.Render<AgentsOverviewSurface>(p => p.Add(c => c.State, Ready()));
        Assert.Null(context.Services.GetService<IAgentsWorkspaceQuery>());
        Assert.Contains("42", cut.Find("[data-testid='agents-overview-metric-agents']").TextContent);
        Assert.Contains("Representative consumer", cut.Find("[data-testid='agents-overview-top-consumers']").TextContent);
        Assert.NotEmpty(cut.FindComponents<CdaChart>().SelectMany(c => c.Instance.Series));
    }

    [Fact]
    public async Task Surface_emits_typed_scope_retry_detail_and_team_intents_once() {
        using var context = Context();
        var state = Ready();
        var intents = new List<AgentsOverviewIntent>();
        var cut = context.Render<AgentsOverviewSurface>(p => p.Add(c => c.State, state).Add(c => c.Intent, intents.Add));
        await cut.FindAll("[data-testid='agents-overview-usage-scope'] button").Single(b => b.TextContent.Trim() == "Chats").ClickAsync();
        Assert.Equal(new AgentsOverviewIntent.SelectUsage(ProviderUsageWorkloadSelection.SimpleChats), Assert.Single(intents));
        Assert.Equal(ProviderUsageWorkloadSelection.Both, cut.Instance.State.DesiredScope);
        intents.Clear();
        await cut.Find("[data-testid='agents-overview-retry']").ClickAsync();
        Assert.IsType<AgentsOverviewIntent.RetryOverview>(Assert.Single(intents));
        intents.Clear();
        await cut.Find("[data-testid='agents-overview-usage-retry']").ClickAsync();
        Assert.IsType<AgentsOverviewIntent.RetryUsage>(Assert.Single(intents));
        intents.Clear();
        await cut.Find("[data-testid='agents-overview-open-provider-usage']").ClickAsync();
        Assert.Equal(new AgentsOverviewIntent.OpenDetail(AgentsOverviewDetail.Providers, ProviderUsageWorkloadSelection.Both), Assert.Single(intents));
        intents.Clear();
        await cut.Find("[data-testid='agents-overview-team-shortcut']").ClickAsync();
        Assert.Equal(new AgentsOverviewIntent.OpenTeam(state.Teams[0].TeamId), Assert.Single(intents));
    }

    [Fact]
    public void Mismatched_accepted_scope_never_renders_old_rows_or_enables_details() {
        using var context = Context();
        var cut = context.Render<AgentsOverviewSurface>(p => p.Add(c => c.State, Ready() with { DesiredScope = ProviderUsageWorkloadSelection.SimpleChats }));
        Assert.DoesNotContain("Representative consumer", cut.Markup);
        Assert.DoesNotContain("Representative provider", cut.Markup);
        Assert.True(cut.Find("[data-testid='agents-overview-open-provider-usage']").HasAttribute("disabled"));
        Assert.Contains("Usage ranking is unavailable", cut.Markup);
        Assert.DoesNotContain("No consumers are ranked", cut.Markup);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Failed_and_stale_states_are_distinct(bool accepted) {
        using var context = Context();
        var state = Ready() with {
            Totals = accepted ? Ready().Totals : null,
            OverviewPhase = accepted ? AgentsOverviewReadPhase.Stale : AgentsOverviewReadPhase.Unavailable,
            OverviewError = "The summary could not be refreshed."
        };
        var cut = context.Render<AgentsOverviewSurface>(p => p.Add(c => c.State, state));
        Assert.NotNull(cut.Find(accepted ? "[data-testid='agents-overview-stale']" : "[data-testid='agents-overview-load-error']"));
        Assert.Contains(accepted ? "42" : "\u2014", cut.Find("[data-testid='agents-overview-metric-agents']").TextContent);
    }

    [Fact]
    public void Surface_chart_options_and_series_belong_to_each_instance() {
        using var context = Context();
        var state = Ready();
        var a = context.Render<AgentsOverviewSurface>(p => p.Add(c => c.State, state));
        var b = context.Render<AgentsOverviewSurface>(p => p.Add(c => c.State, state));
        var left = a.FindComponents<CdaChart>().Select(c => c.Instance).ToArray();
        var right = b.FindComponents<CdaChart>().Select(c => c.Instance).ToArray();
        foreach (var chart in left) {
            Assert.DoesNotContain(right, other => ReferenceEquals(chart.Options, other.Options));
            Assert.DoesNotContain(right, other => ReferenceEquals(chart.Series, other.Series));
        }
        left[0].Options.Unit = "Changed in first renderer";
        Assert.DoesNotContain(right, chart => chart.Options.Unit == "Changed in first renderer");
    }

    [Fact]
    public async Task Header_partial_has_its_own_typed_retry() {
        using var context = Context();
        var intents = new List<AgentsOverviewIntent>();
        var cut = context.Render<AgentsOverviewSurface>(p => p.Add(c => c.State, Ready() with { HeaderWarning = "HR Agent is unavailable." }).Add(c => c.Intent, intents.Add));
        await cut.Find("[data-testid='agents-overview-header-partial'] button").ClickAsync();
        Assert.IsType<AgentsOverviewIntent.RetryHeader>(Assert.Single(intents));
    }

    private static BunitContext Context() {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddLogging();
        context.Services.AddCanDoItAllBaseLib();
        return context;
    }

    private static AgentsOverviewState Ready() {
        var totals = ProviderUsageTotals.Empty with { ExecutionCount = 3, UsageObservationCount = 3, UnpricedObservationCount = 3 };
        return new() {
            DesiredScope = ProviderUsageWorkloadSelection.Both,
            AcceptedScope = ProviderUsageWorkloadSelection.Both,
            Totals = AgentOverviewTotals.Empty with { AgentCount = 42 },
            UsageTotals = totals,
            OverviewPhase = AgentsOverviewReadPhase.Ready,
            UsagePhase = AgentsOverviewReadPhase.Ready,
            Consumers = [new(ProviderUsageConsumerKind.Agent, "consumer", "Representative consumer", totals, null)],
            Providers = [new(null, "Representative provider", ProviderKind.OpenAi, totals, null)],
            Teams = [new(Guid.NewGuid(), "Representative team", "", "groups", 2)]
        };
    }
}
