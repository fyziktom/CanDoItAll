using Bunit;
using CanDoItAll.AgentFramework.UI.Shell;
using CanDoItAll.AgentFramework.UI.Overview;
using CanDoItAll.AgentFramework.UI.Usage;
using CanDoItAll.AgentFramework.UiSandbox;
using CanDoItAll.AgentFramework.Usage;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Components.Charts;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.AgentFramework;

public sealed class AgentsCompletionSurfaceTests {
    public static IEnumerable<object[]> Scopes() =>
        from selection in new[] { ProviderUsageWorkloadSelection.Agents, ProviderUsageWorkloadSelection.SimpleChats, ProviderUsageWorkloadSelection.Both }
        from period in Enum.GetValues<ProviderUsagePeriod>()
        select new object[] { selection, period };

    [Theory]
    [MemberData(nameof(Scopes))]
    public async Task All_details_preserve_the_exact_accepted_window_and_independent_snapshot(ProviderUsageWorkloadSelection selection, ProviderUsagePeriod period) {
        using var context = Context();
        var state = CompletionSandboxFixture.Usage(selection: selection, period: period);
        var intents = new List<UsageDetailIntent>();
        var consumer = context.Render<AgentUsageSurface>(p => p.Add(c => c.State, state).Add(c => c.Intent, intents.Add));
        var provider = context.Render<ProviderUsageSurface>(p => p.Add(c => c.State, state).Add(c => c.Intent, intents.Add));
        var model = context.Render<ModelUsageSurface>(p => p.Add(c => c.State, state).Add(c => c.Intent, intents.Add));
        Assert.Equal(12, consumer.FindComponent<DataGrid<ProviderUsageConsumerRow>>().Instance.Data!.Count());
        Assert.Equal(10, consumer.FindComponent<CdaChart>().Instance.Series.Single().Points.Count());
        Assert.Equal(new decimal[] { 12, 11, 10, 9, 8, 7, 6, 5, 4, 3 }, consumer.FindComponent<CdaChart>().Instance.Series.Single().Points.Select(point => point.Value));
        var rows = provider.FindComponent<DataGrid<ProviderUsageProviderRow>>().Instance.Data!.ToArray();
        Assert.Equal(2, rows.Length);
        Assert.Equal(rows[0].ProviderName, rows[1].ProviderName);
        Assert.NotEqual(rows[0].ProviderProfileId, rows[1].ProviderProfileId);
        Assert.Equal(new[] { "opaque-model/2:revision", "opaque-model/1:revision" },
            model.FindComponent<DataGrid<ProviderUsageModelRow>>().Instance.Data!.Select(row => row.Model));
        foreach (var cut in new IRenderedComponent<IComponent>[] { consumer, provider, model }) {
            await cut.Find("[data-testid='usage-detail-close']").ClickAsync();
        }
        Assert.Equal(3, intents.Count);
        Assert.All(intents, intent => {
            Assert.Same(state.Query, intent.Query);
            Assert.Equal(state.Origin, intent.Origin);
            Assert.Equal(UsageDetailAction.Close, intent.Action);
        });
    }

    [Theory]
    [InlineData(CompletionScenario.Loading)]
    [InlineData(CompletionScenario.Error)]
    [InlineData(CompletionScenario.Retired)]
    public void Unavailable_states_never_render_cached_charts_and_always_allow_close(CompletionScenario scenario) {
        using var context = Context();
        var state = CompletionSandboxFixture.Usage(scenario);
        var cuts = new IRenderedComponent<IComponent>[] {
            context.Render<AgentUsageSurface>(p => p.Add(c => c.State, state)),
            context.Render<ProviderUsageSurface>(p => p.Add(c => c.State, state)),
            context.Render<ModelUsageSurface>(p => p.Add(c => c.State, state))
        };
        foreach (var cut in cuts) {
            Assert.Empty(cut.FindComponents<CdaChart>());
            Assert.False(cut.Find("[data-testid='usage-detail-close']").HasAttribute("disabled"));
            Assert.Equal(scenario == CompletionScenario.Error ? 1 : 0, cut.FindAll("[data-testid='usage-detail-retry']").Count);
        }
    }

    [Fact]
    public async Task Shell_buttons_capture_the_rendered_origin_and_leave_content_in_its_slot() {
        using var context = Context();
        var state = CompletionSandboxFixture.Shell();
        var intents = new List<AgentsShellIntent>();
        var cut = context.Render<AgentsShellSurface>(p => p.Add(c => c.State, state).Add(c => c.Intent, intents.Add)
            .AddChildContent("Independent native slot"));
        var original = cut.FindComponents<PageHeaderActionButton>().Single(button => button.Instance.Label == "Open workflows").Instance.Click;
        cut.Render(p => p.Add(c => c.State, CompletionSandboxFixture.Shell(2, "providers")));
        await cut.InvokeAsync(() => original.InvokeAsync());
        Assert.Equal(state.Origin, Assert.Single(intents).Origin);
        Assert.Contains("Independent native slot", cut.Markup, StringComparison.Ordinal);
        Assert.Equal(11, cut.FindComponent<SecondaryTabs>().Instance.Items.Count());
        Assert.Empty(cut.FindAll("[data-testid='agents-header-retry']"));
    }

    [Fact]
    public void Partial_evidence_and_unknown_usage_remain_distinct_from_empty() {
        using var context = Context();
        var partial = context.Render<AgentUsageSurface>(p => p.Add(c => c.State, CompletionSandboxFixture.Usage(CompletionScenario.Partial)));
        Assert.Single(partial.FindAll("[data-testid='usage-dialog-partial']"));
        Assert.Contains("Unknown observations", partial.Markup, StringComparison.Ordinal);
        Assert.Contains("Unpriced", partial.Markup, StringComparison.Ordinal);
        var empty = context.Render<ProviderUsageSurface>(p => p.Add(c => c.State, CompletionSandboxFixture.Usage(CompletionScenario.Empty)));
        Assert.Contains("No provider usage rows", empty.Markup, StringComparison.Ordinal);
        Assert.Empty(empty.FindAll("[data-testid='usage-dialog-partial']"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Queued_grid_template_keeps_its_original_denominator_after_snapshot_retirement(bool models) {
        using var context = Context();
        var state = CompletionSandboxFixture.Usage();
        RenderFragment queued;
        if (models) {
            var cut = context.Render<ModelUsageSurface>(p => p.Add(c => c.State, state));
            var column = cut.FindComponents<DataGridColumn<ProviderUsageModelRow>>().Single(item => item.Instance.Title == "Share").Instance;
            queued = column.Template!(state.Snapshot!.Models[1]);
            cut.Render(p => p.Add(c => c.State, state with { Retired = true, Snapshot = null }));
            Assert.Contains("This usage view has closed", cut.Markup, StringComparison.Ordinal);
        } else {
            var cut = context.Render<ProviderUsageSurface>(p => p.Add(c => c.State, state));
            var column = cut.FindComponents<DataGridColumn<ProviderUsageProviderRow>>().Single(item => item.Instance.Title == "Share").Instance;
            queued = column.Template!(state.Snapshot!.Providers[1]);
            cut.Render(p => p.Add(c => c.State, state with { Retired = true, Snapshot = null }));
            Assert.Contains("This usage view has closed", cut.Markup, StringComparison.Ordinal);
        }
        var retainedCell = context.Render(queued);
        Assert.Contains(AgentUsageDisplay.FormatUsageShare(69, 102), retainedCell.Markup, StringComparison.Ordinal);
    }

    private static BunitContext Context() {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddLogging();
        context.Services.AddCanDoItAllBaseLib();
        return context;
    }
}
