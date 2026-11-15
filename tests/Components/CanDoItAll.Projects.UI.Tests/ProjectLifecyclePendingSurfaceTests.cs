using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Projects.Pages.Components;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.ProjectsUi;

public sealed class ProjectLifecyclePendingSurfaceTests {
    [Fact]
    public void Settled_empty_recovery_does_not_displace_the_portfolio() {
        using var context = Context();
        var cut = context.Render<ProjectLifecyclePendingSurface>();

        Assert.Equal(string.Empty, cut.Markup.Trim());
    }

    [Fact]
    public void Loading_keeps_recovery_visible_and_prevents_duplicate_refresh() {
        using var context = Context();
        var cut = context.Render<ProjectLifecyclePendingSurface>(parameters => parameters
            .Add(surface => surface.Loading, true));

        Assert.Single(cut.FindAll("[data-testid='project-lifecycle-pending']"));
        Assert.True(cut.Find("[data-testid='project-lifecycle-refresh']").HasAttribute("disabled"));
    }

    [Fact]
    public async Task An_empty_failed_read_remains_visible_and_can_be_refreshed() {
        await using var context = Context();
        var refreshes = 0;
        var cut = context.Render<ProjectLifecyclePendingSurface>(parameters => parameters
            .Add(surface => surface.Error, "Retained operations could not be read.")
            .Add(surface => surface.Refresh, () => refreshes++));

        Assert.Contains("Retained operations could not be read.", cut.Find("[data-testid='project-lifecycle-pending']").TextContent);
        await cut.InvokeAsync(() => cut.Find("[data-testid='project-lifecycle-refresh']").ClickAsync(new()));

        Assert.Equal(1, refreshes);
    }

    [Fact]
    public async Task A_retained_row_opens_its_original_operation() {
        await using var context = Context();
        var operation = Operation();
        ProjectLifecycleOperation? opened = null;
        var metadata = new ProjectLifecyclePendingMetadata(operation, new(1), ProjectPendingStage.Unconfirmed,
            ProjectLifecycleCommandKind.Delete, [Guid.NewGuid()], true);
        var cut = context.Render<ProjectLifecyclePendingSurface>(parameters => parameters
            .Add(surface => surface.Items, [metadata])
            .Add(surface => surface.Open, value => opened = value));

        Assert.Contains(operation.Id.ToString("D"), cut.Find("[data-testid='project-lifecycle-pending']").TextContent);
        await cut.InvokeAsync(() => cut.Find("[data-testid='project-lifecycle-open']").ClickAsync(new()));

        Assert.Equal(operation, opened);
    }

    [Fact]
    public async Task Selected_recovery_remains_actionable_without_catalog_rows_or_an_inline_refresh() {
        await using var context = Context();
        var operation = Operation();
        ProjectLifecyclePendingIntent? intent = null;
        var view = new ProjectLifecyclePendingView(operation, "The original outcome is unconfirmed.", false,
            false, false, false, false);
        var cut = context.Render<ProjectLifecyclePendingSurface>(parameters => parameters
            .Add(surface => surface.View, view)
            .Add(surface => surface.ShowRefreshAction, false)
            .Add(surface => surface.Action, value => intent = value));

        Assert.Contains(view.Message, cut.Find("[data-testid='project-lifecycle-recovery']").TextContent);
        Assert.Empty(cut.FindAll("[data-testid='project-lifecycle-refresh']"));
        Assert.True(cut.Find("[data-testid='project-lifecycle-retry']").HasAttribute("disabled"));
        await cut.InvokeAsync(() => cut.Find("[data-testid='project-lifecycle-check']").ClickAsync(new()));

        Assert.Equal(new ProjectLifecyclePendingIntent(operation, ProjectLifecyclePendingAction.Check), intent);
    }

    private static BunitContext Context() {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddCanDoItAllBaseLib();
        return context;
    }

    private static ProjectLifecycleOperation Operation()
        => new(Guid.NewGuid(), new(Guid.NewGuid()), new(1), new("pc1:" + new string('b', 64)), Guid.NewGuid());
}
