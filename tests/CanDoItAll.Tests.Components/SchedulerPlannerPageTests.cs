using Bunit;
using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Modules.Automation;
using CanDoItAll.Modules.SchedulerPlanner;
using CanDoItAll.Modules.SchedulerPlanner.Pages;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.Tests.Components;

public sealed class SchedulerPlannerPageTests
{
    [Fact]
    public async Task Scheduler_page_renders_tabs_and_canvas_calendar_host()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();

        var cut = harness.Context.RenderComponent<SchedulerPlannerPage>();

        cut.WaitForAssertion(() =>
        {
            var errors = cut.FindAll("[data-testid='scheduler-error']")
                .Select(element => element.TextContent.Trim())
                .ToArray();
            if (errors.Length > 0)
            {
                Assert.Fail(string.Join(Environment.NewLine, errors));
            }

            Assert.NotEmpty(cut.FindAll("[data-testid='scheduler-tabs']"));
        });
        cut.WaitForElement("[data-testid='scheduler-calendar']");

        Assert.Contains("Scheduled runs", cut.Markup);
        Assert.Contains("New schedule", cut.Markup);
        Assert.Contains("History", cut.Markup);
        Assert.Contains("scheduler-planner-calendar", cut.Markup);
    }

    [Fact]
    public async Task Scheduler_target_picker_uses_dialog_cards_and_filters()
    {
        var processId = Guid.NewGuid();
        var workflowId = Guid.NewGuid();
        var workflowVersionId = Guid.NewGuid();
        var workspace = new SchedulerPlannerWorkspace(
            [],
            [],
            [
                new SchedulerTargetOption(
                    SchedulerPlanTargetKind.Process,
                    processId,
                    null,
                    "Onboarding process",
                    "Role-first process for new customer onboarding.",
                    "Published"),
                new SchedulerTargetOption(
                    SchedulerPlanTargetKind.Workflow,
                    workflowId,
                    workflowVersionId,
                    "Release workflow",
                    "Workflow for release readiness validation.",
                    "Draft")
            ],
            new CanvasCalendarSurface
            {
                SurfaceId = "scheduler-planner-calendar",
                InitialView = "week",
                SelectedDate = "2026-05-12",
                Timezone = "UTC",
                Locale = "en-US",
                AllowCreate = false,
                AllowEdit = false,
                AllowDelete = false,
                AllowDragDrop = false,
                AllowResize = false
            });
        var defaultEditor = new SchedulerPlanEditorModel
        {
            TargetKind = SchedulerPlanTargetKind.Process,
            TargetId = processId,
            CronExpression = "0 0 9 ? * MON-FRI",
            TimeZoneId = "UTC",
            InputJson = "{}",
            IsEnabled = true
        };

        await using var harness = await ComponentTestHarness.CreateAsync(services =>
        {
            services.RemoveAll<ISchedulerPlannerService>();
            services.AddSingleton<ISchedulerPlannerService>(new StubSchedulerPlannerService(workspace, defaultEditor));
        });

        var cut = harness.Context.RenderComponent<SchedulerPlannerPage>();

        cut.WaitForElement("[data-testid='scheduler-tabs']");
        cut.FindAll("button[role='tab']")
            .Single(element => element.TextContent.Contains("New schedule", StringComparison.Ordinal))
            .Click();
        cut.WaitForElement("[data-testid='scheduler-target-open']");
        cut.Find("[data-testid='scheduler-target-open']").Click();
        cut.WaitForElement("[data-testid='scheduler-target-dialog']");

        Assert.Equal(2, cut.FindAll("[data-testid='scheduler-target-card']").Count);
        Assert.Contains("Onboarding process", cut.Markup);
        Assert.Contains("Release workflow", cut.Markup);
        Assert.NotEmpty(cut.FindAll("[data-testid='scheduler-target-tag-filter']"));
        Assert.NotEmpty(cut.FindAll("[data-testid='scheduler-target-filter-processes']"));
        Assert.NotEmpty(cut.FindAll("[data-testid='scheduler-target-filter-workflows']"));

        cut.Find("[data-testid='scheduler-target-filter-workflows']").Change(false);

        cut.WaitForAssertion(() =>
        {
            Assert.Single(cut.FindAll("[data-testid='scheduler-target-card']"));
            Assert.Contains("Onboarding process", cut.Markup);
            Assert.DoesNotContain("Release workflow", cut.Markup);
        });
    }

    private sealed class StubSchedulerPlannerService(
        SchedulerPlannerWorkspace workspace,
        SchedulerPlanEditorModel defaultEditor) : ISchedulerPlannerService
    {
        public Task<SchedulerPlannerWorkspace> GetWorkspaceAsync(
            SchedulerHistoryQuery? historyQuery = null,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(workspace);
        }

        public Task<SchedulerPlanEditorModel> CreateDefaultEditorAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(defaultEditor);
        }

        public Task<SchedulerPlanSummary> SavePlanAsync(
            SchedulerPlanEditorModel editor,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new SchedulerPlanSummary(
                Guid.NewGuid(),
                editor.Name,
                editor.Description,
                editor.TargetKind,
                editor.TargetId,
                editor.TargetVersionId,
                "Target",
                editor.CronExpression,
                "Every weekday at 09:00 (UTC).",
                editor.TimeZoneId,
                editor.MisfirePolicy,
                editor.IsEnabled,
                editor.StartAtUtc,
                editor.EndAtUtc,
                null,
                null,
                string.Empty,
                DateTimeOffset.UtcNow));
        }

        public Task SetPlanEnabledAsync(
            Guid planId,
            bool isEnabled,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}
