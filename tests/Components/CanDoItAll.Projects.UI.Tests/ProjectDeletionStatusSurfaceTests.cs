using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Modules.Projects.Pages.Components;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.ProjectsUi;

public sealed class ProjectDeletionStatusSurfaceTests {
    [Fact]
    public void No_feedback_or_history_renders_no_notice() {
        using var context = Context();
        var cut = context.Render<ProjectDeletionStatusSurface>();
        Assert.Empty(cut.FindAll("[data-testid='project-cleanup-summary']"));
        Assert.Empty(cut.FindAll("[data-testid='project-deletion-notice']"));
    }

    [Fact]
    public async Task Large_history_stays_compact_until_requested_and_all_operations_are_reachable() {
        using var context = Context();
        var notices = Notices(103);
        var cut = Render(context, notices);
        Assert.Contains("103 completed", cut.Markup);
        Assert.DoesNotContain("retained-object-", cut.Markup);
        Assert.Empty(cut.FindAll("[data-testid='project-cleanup-completed-item']"));
        Assert.True(cut.Markup.Length < 4000);
        Assert.Equal(AlertStyle.Info, cut.FindComponent<Alert>().Instance.AlertStyle);
        await Click(cut, "project-cleanup-open");
        var observed = new HashSet<Guid>();
        do {
            var rows = cut.FindAll("[data-testid='project-cleanup-completed-item']");
            Assert.InRange(rows.Count, 1, 10);
            foreach (var notice in notices.Where(notice => rows.Any(row => row.TextContent.Contains(notice.Target.RecoveryId.ToString())))) {
                observed.Add(notice.Target.RecoveryId);
            }
            if (cut.Find("[data-testid='project-cleanup-next']").HasAttribute("disabled")) {
                break;
            }
            await Click(cut, "project-cleanup-next");
        } while (observed.Count < notices.Length);
        Assert.Equal(notices.Length, observed.Count);
        Assert.DoesNotContain("retained-object-", cut.Markup);
    }

    [Fact]
    public async Task Media_details_are_explicit_bounded_and_complete() {
        using var context = Context();
        var notice = Notices(1, 23)[0];
        var cut = Render(context, [notice]);
        await Click(cut, "project-cleanup-open");
        await Click(cut, "project-cleanup-inspect");
        var observed = new HashSet<string>();
        do {
            var rows = cut.FindAll("[data-testid='project-cleanup-media-item']");
            Assert.InRange(rows.Count, 1, 10);
            foreach (var row in rows) {
                observed.Add(row.TextContent);
            }
            if (cut.Find("[data-testid='project-cleanup-media-next']").HasAttribute("disabled")) {
                break;
            }
            await Click(cut, "project-cleanup-media-next");
        } while (observed.Count < notice.Warnings.Count);
        Assert.Equal(notice.Warnings.Order(), observed.Order());
        await Click(cut, "project-cleanup-back");
        Assert.Empty(cut.FindAll("[data-testid='project-cleanup-media-item']"));
    }

    [Fact]
    public async Task Pending_work_takes_priority_and_retry_carries_exact_identity_with_busy_state() {
        using var context = Context();
        var target = Notices(1)[0].Target;
        var pending = new ProjectCleanupStatus(target, "Failed", true, null, "Retry this operation.");
        var cut = Render(context, Notices(30));
        ProjectCleanupTarget? received = null;
        cut.Render(p => p.Add(x => x.Pending, [pending]).Add(x => x.Retry, value => received = value));
        Assert.Equal(AlertStyle.Warning, cut.FindComponent<Alert>().Instance.AlertStyle);
        Assert.Contains("1 cleanup operation(s) need attention", cut.Markup);
        await Click(cut, "project-cleanup-open");
        Assert.Single(cut.FindAll("[data-testid='project-cleanup-pending-item']"));
        Assert.Empty(cut.FindAll("[data-testid='project-cleanup-completed-item']"));
        cut.Render(p => p.Add(x => x.BusyTargets, new HashSet<ProjectCleanupTarget> { target }));
        Assert.True(cut.Find("[data-testid='project-deletion-retry']").HasAttribute("disabled"));
        Assert.Null(received);
        cut.Render(p => p.Add(x => x.BusyTargets, new HashSet<ProjectCleanupTarget>()));
        await Click(cut, "project-deletion-retry");
        Assert.Equal(target, received);
    }

    [Fact]
    public async Task Refresh_clamps_pages_and_never_rebinds_a_missing_selected_operation() {
        using var context = Context();
        var notices = Notices(12);
        var cut = Render(context, notices);
        cut.Render(p => p.Add(x => x.Review, new(ProjectCleanupView.RetainedMedia, 900)));
        Assert.Contains(notices[^1].Target.RecoveryId.ToString(), cut.Markup);
        cut.Render(p => p.Add(x => x.Completed, new[] { notices[0] }));
        Assert.Single(cut.FindAll("[data-testid='project-cleanup-completed-item']"));
        await Click(cut, "project-cleanup-inspect");
        cut.Render(p => p.Add(x => x.Completed, new[] { notices[1] }));
        Assert.Single(cut.FindAll("[data-testid='project-cleanup-missing']"));
        Assert.Empty(cut.FindAll("[data-testid='project-cleanup-media-item']"));
    }

    [Fact]
    public async Task Close_preserves_history_and_reopen_starts_at_the_operation_list() {
        using var context = Context();
        var cut = Render(context, Notices(6));
        await Click(cut, "project-cleanup-open");
        await Click(cut, "project-cleanup-inspect");
        var queuedBody = cut.FindComponents<Stack>().Single(stack => stack.Instance.GapScale == LayoutGap.Medium).Instance.ChildContent;
        await cut.InvokeAsync(() => cut.Find("button[aria-label='Close']").ClickAsync(new()));
        Assert.Empty(cut.FindAll("[data-testid='project-cleanup-dialog']"));
        context.Render<Stack>(parameters => parameters.Add(stack => stack.ChildContent, queuedBody));
        Assert.Contains("6 completed", cut.Markup);
        await Click(cut, "project-cleanup-open");
        Assert.NotEmpty(cut.FindAll("[data-testid='project-cleanup-completed-item']"));
        Assert.Empty(cut.FindAll("[data-testid='project-cleanup-media-item']"));
    }

    [Fact]
    public void Unknown_operation_feedback_remains_visible_without_history() {
        using var context = Context();
        var cut = context.Render<ProjectDeletionStatusSurface>(p => p
            .Add(x => x.Message, "Cleanup completion is unconfirmed. Refresh before another retry.")
            .Add(x => x.IsError, true));
        Assert.Contains("unconfirmed", cut.Find("[data-testid='project-deletion-notice']").TextContent);
        Assert.Equal(AlertStyle.Warning, cut.FindComponent<Alert>().Instance.AlertStyle);
    }

    private static BunitContext Context() {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddCanDoItAllBaseLib();
        return context;
    }

    private static IRenderedComponent<ProjectDeletionStatusSurface> Render(BunitContext context, ProjectCleanupNotice[] notices) {
        var cut = context.Render<ProjectDeletionStatusSurface>(p => p.Add(x => x.Completed, notices));
        cut.Render(p => p.Add(x => x.ReviewChanged, review => cut.Render(next => next.Add(x => x.Review, review))));
        return cut;
    }

    private static Task Click(IRenderedComponent<ProjectDeletionStatusSurface> cut, string testId)
        => cut.InvokeAsync(() => cut.Find($"[data-testid='{testId}']").ClickAsync(new()));

    private static ProjectCleanupNotice[] Notices(int count, int warningCount = 1)
        => Enumerable.Range(0, count).Select(index => new ProjectCleanupNotice(
            new(Guid.NewGuid(), new("media"), Guid.NewGuid()), "project deletion",
            Enumerable.Range(0, warningCount).Select(item => $"retained-object-{index}-{item}: Keep the immutable object or review the external pin.").ToArray())).ToArray();
}
