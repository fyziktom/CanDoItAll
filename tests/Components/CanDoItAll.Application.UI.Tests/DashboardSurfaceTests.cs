using Bunit;
using CanDoItAll.Application.UI.Dashboard;
using CanDoItAll.Application.UiSandbox;
using CanDoItAll.Components.BaseLib;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.ApplicationUI;

public sealed class DashboardSurfaceTests : BunitContext {
    public DashboardSurfaceTests() {
        Services.AddCanDoItAllBaseLib();
    }

    [Fact]
    public void Loading_has_real_links_and_no_fabricated_totals() {
        var cut = Render<DashboardSurface>(parameters => parameters
            .Add(component => component.View, DashboardScenarios.Create(DashboardScenario.Loading)));

        Assert.Empty(cut.FindComponents<CompactStat>());
        Assert.Single(cut.FindComponents<LoadingState>());
        Assert.Equal(["/projects", "/agents", "/processes/live", "/scheduler"],
            cut.FindAll("a[data-testid^='dashboard-action-']").Select(link => link.GetAttribute("href")));
    }

    [Fact]
    public async Task Populated_controls_keep_captured_links_partial_cost_and_owner_selected_activity() {
        var view = DashboardScenarios.Create(DashboardScenario.PartialPricing);
        DashboardActivityTab? selected = null;
        var cut = Render<DashboardSurface>(parameters => parameters
            .Add(component => component.View, view)
            .Add(component => component.ActivityChanged, value => selected = value));

        Assert.Equal($"/projects?projectId={DashboardScenarios.ProjectId:D}",
            cut.Find($"[data-testid='dashboard-project-{DashboardScenarios.ProjectId:N}']").GetAttribute("href"));
        Assert.Equal($"/agents/workflows?workflowId={DashboardScenarios.WorkflowId:D}&runId={DashboardScenarios.WorkflowRunId:D}",
            cut.Find($"[data-testid='dashboard-workflow-{DashboardScenarios.WorkflowRunId:N}']").GetAttribute("href"));
        var cost = Assert.Single(cut.FindComponents<CompactStat>(), stat => stat.Instance.Label == "Known cost (USD)");
        Assert.Equal("$0.1235", cost.Instance.Value);
        Assert.Contains("excludes 2", cost.Instance.TooltipText);

        await cut.Find("[data-testid='dashboard-processes-tab']").ClickAsync(new());
        Assert.Equal(DashboardActivityTab.Processes, selected);
        cut.Render(parameters => parameters.Add(component => component.View, view with { Activity = Assert.IsType<DashboardActivityTab>(selected) }));
        Assert.Equal($"/processes/live?runId={DashboardScenarios.ProcessRunId:D}",
            cut.Find($"[data-testid='dashboard-process-{DashboardScenarios.ProcessRunId:N}']").GetAttribute("href"));
        Assert.Contains("Projection behind", cut.Markup);

        cut.Render(parameters => parameters.Add(component => component.View, view with {
            Activity = DashboardActivityTab.Processes, NowUtc = view.NowUtc.AddMinutes(1)
        }));
        Assert.NotEmpty(cut.FindAll($"[data-testid='dashboard-process-{DashboardScenarios.ProcessRunId:N}']"));
    }

    [Fact]
    public async Task Stale_and_initial_failure_remain_distinct_and_retry_is_an_owner_intent() {
        var refreshes = 0;
        var cut = Render<DashboardSurface>(parameters => parameters
            .Add(component => component.View, DashboardScenarios.Create(DashboardScenario.Stale))
            .Add(component => component.Refresh, () => refreshes++));
        Assert.Contains("Showing the last successful snapshot", cut.Find("[data-testid='dashboard-stale-warning']").TextContent);
        Assert.Contains("Release planning", cut.Markup);
        Assert.Equal(2, cut.FindComponents<CompactStat>().Count);

        cut.Render(parameters => parameters.Add(component => component.View, DashboardScenarios.Create(DashboardScenario.Failure)));
        Assert.Empty(cut.FindComponents<CompactStat>());
        Assert.Empty(cut.FindAll("[data-testid='dashboard-stale-warning']"));
        await cut.Find("[data-testid='dashboard-retry']").ClickAsync(new());
        Assert.Equal(1, refreshes);
        Assert.Contains("snapshot unavailable", cut.Find("[data-testid='dashboard-load-error']").TextContent);
    }

    [Fact]
    public void Separate_views_do_not_share_activity_or_refresh_state() {
        var first = Render<DashboardSurface>(parameters => parameters
            .Add(component => component.View, DashboardScenarios.Create(DashboardScenario.Populated, DashboardActivityTab.Processes)));
        var second = Render<DashboardSurface>(parameters => parameters
            .Add(component => component.View, DashboardScenarios.Create(DashboardScenario.Loading)));
        Assert.NotEmpty(first.FindAll($"[data-testid='dashboard-process-{DashboardScenarios.ProcessRunId:N}']"));
        Assert.Empty(second.FindComponents<CompactStat>());
        Assert.Single(second.FindComponents<LoadingState>());
    }
}
