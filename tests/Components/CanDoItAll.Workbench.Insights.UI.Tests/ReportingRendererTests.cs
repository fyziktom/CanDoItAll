using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Components.Charts;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Workbench.Insights.UI;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Workbench.Insights.UI.Tests;

public sealed class ReportingRendererTests {
    [Fact]
    public async Task Unloaded_activity_totals_are_not_reported_as_zero() {
        using var context = CreateContext();
        using var session = new ManagerActivitySession(new ReportingSessionTests.ControlledActivity());
        var dialog = context.Render<ProjectManagerActivityDialog>(parameters => parameters.Add(component => component.Session, session));
        Assert.All(dialog.FindComponents<MetricCard>(), card => Assert.Equal("—", card.Instance.Value));
        Assert.Contains("No accepted page", dialog.Markup);
        await context.DisposeAsync();
    }

    [Fact]
    public async Task Mounted_panel_is_lazy_and_actual_charts_keep_accepted_range() {
        using var context = CreateContext();
        var source = new ReportingSessionTests.ControlledReports();
        using var session = new ManagerSummarySession(source);
        var panel = context.Render<ProjectManagerSummaryPanel>(parameters => parameters.Add(component => component.Session, session));
        Assert.Empty(source.Reads);
        await panel.Find("[data-testid=manager-summary-range-year]").ClickAsync(new());
        Assert.Empty(source.Reads);
        var load = panel.InvokeAsync(() => session.LoadAsync(session.Options));
        source.Reads[0].Succeed();
        await load;
        await panel.Find("[data-testid=manager-summary-range-day]").ClickAsync(new());
        Assert.Equal(ProjectManagerSummaryTimeRange.Day, session.Options.TimeRange);
        Assert.Equal("MMM", panel.FindComponents<CdaChart>().Single(chart => chart.Instance.Title == "Expense trend").Instance.Options.DateTimeLabelFormat);
        Assert.Contains("Options changed", panel.Markup);
        await context.DisposeAsync();
    }

    [Fact]
    public async Task Queued_control_belongs_to_original_view_even_when_receiver_is_replaced() {
        using var context = CreateContext();
        var original = new ReportingSessionTests.ControlledReports();
        using var first = new ManagerSummarySession(original);
        var next = new ReportingSessionTests.ControlledReports();
        using var second = new ManagerSummarySession(next);
        var panel = context.Render<ProjectManagerSummaryPanel>(parameters => parameters.Add(component => component.Session, first));
        var load = panel.FindComponents<Button>().Single(button => button.Instance.Text == "Load summary").Instance.Click;
        first.Dispose();
        panel.Render(parameters => parameters.Add(component => component.Session, second));
        await panel.InvokeAsync(() => load.InvokeAsync(new()));
        Assert.Empty(original.Reads);
        Assert.Empty(next.Reads);
        await context.DisposeAsync();
    }

    private static BunitContext CreateContext() {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddCanDoItAllBaseLib();
        return context;
    }
}
