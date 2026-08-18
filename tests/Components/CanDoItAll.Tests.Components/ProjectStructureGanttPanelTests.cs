using Bunit;
using Bunit.TestDoubles;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Components.Gantt;
using CanDoItAll.Components.Mermaid;
using CanDoItAll.Infrastructure.Configuration;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workbench.CanvasAdapters;
using CanDoItAll.Modules.Workbench.Pages;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace CanDoItAll.Tests.Components.ProjectStructure;

public sealed class ProjectStructureGanttPanelTests
{
    private static readonly DateTimeOffset Baseline = new(2026, 7, 15, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Mermaid_export_previews_and_downloads_only_the_gantt_projection_tasks()
    {
        using var context = CreateContext([]);
        var projectId = Guid.NewGuid();
        var predecessor = CreateTask("task-a", "Plan implementation");
        var successor = CreateTask("task-b", "Implement feature");
        var note = CreateTask("note-a", "Architecture note") with
        {
            ObjectType = ProjectObjectType.Note,
            ObjectSubtype = "note"
        };
        var nonTaskWorkItem = CreateTask("work-item-a", "Non-task work item") with
        {
            ObjectSubtype = "feature"
        };
        var systemTask = CreateTask("system-task", "System-managed task") with
        {
            IsSystemManaged = true
        };
        var dependency = new ProjectStructureLink(
            successor.Id,
            predecessor.Id,
            ProjectObjectLinkKind.DependsOn,
            true,
            Guid.NewGuid());
        var surface = CreateSurface(
            projectId,
            [predecessor, successor, note, nonTaskWorkItem, systemTask],
            [dependency]);

        var cut = context.Render<ProjectStructureGanttPanel>(parameters => parameters
            .Add(component => component.ProjectId, projectId)
            .Add(component => component.Surface, surface)
            .Add(component => component.MutationCommitted, () => { }));

        cut.Find("[data-testid='project-structure-gantt-export-mermaid']").Click();

        var source = cut.FindComponent<MermaidDiagram>().Instance.Source;
        Assert.NotNull(source);
        Assert.Contains("Plan implementation", source, StringComparison.Ordinal);
        Assert.Contains("Implement feature", source, StringComparison.Ordinal);
        Assert.Contains("after task1", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Architecture note", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Non-task work item", source, StringComparison.Ordinal);
        Assert.DoesNotContain("System-managed task", source, StringComparison.Ordinal);
        Assert.Equal(2, source.Split('\n').Count(line => line.Contains(" :", StringComparison.Ordinal)));
        Assert.NotNull(cut.Find("[data-testid='project-structure-gantt-copy-mermaid']"));

        var download = cut.Find("[data-testid='project-structure-gantt-download-mermaid']");
        Assert.Equal("project-schedule-gantt.mmd", download.GetAttribute("download"));
        Assert.StartsWith("data:text/vnd.mermaid;charset=utf-8,", download.GetAttribute("href"), StringComparison.Ordinal);
    }

    [Fact]
    public void Mermaid_export_is_disabled_without_canonical_tasks()
    {
        using var context = CreateContext([]);
        var projectId = Guid.NewGuid();
        var note = CreateTask("note-a", "Architecture note") with
        {
            ObjectType = ProjectObjectType.Note,
            ObjectSubtype = "note"
        };

        var cut = context.Render<ProjectStructureGanttPanel>(parameters => parameters
            .Add(component => component.ProjectId, projectId)
            .Add(component => component.Surface, CreateSurface(projectId, note))
            .Add(component => component.MutationCommitted, () => { }));

        Assert.True(cut.Find("[data-testid='project-structure-gantt-export-mermaid']").HasAttribute("disabled"));
        Assert.Empty(cut.FindAll("[data-testid='project-structure-gantt-mermaid-dialog']"));
    }

    [Fact]
    public void Mermaid_preview_closes_when_project_parameters_become_invalid()
    {
        using var context = CreateContext([]);
        var projectId = Guid.NewGuid();
        var surface = CreateSurface(projectId, CreateTask("task-a", "Task"));
        var cut = context.Render<ProjectStructureGanttPanel>(parameters => parameters
            .Add(component => component.ProjectId, projectId)
            .Add(component => component.Surface, surface)
            .Add(component => component.MutationCommitted, () => { }));

        cut.Find("[data-testid='project-structure-gantt-export-mermaid']").Click();
        Assert.NotEmpty(cut.FindAll("[data-testid='project-structure-gantt-mermaid-dialog']"));

        cut.Render(parameters => parameters
            .Add(component => component.ProjectId, Guid.Empty)
            .Add(component => component.Surface, surface)
            .Add(component => component.MutationCommitted, () => { }));

        Assert.Empty(cut.FindAll("[data-testid='project-structure-gantt-mermaid-dialog']"));
    }

    [Fact]
    public void Projection_only_task_allows_schedule_edits_and_loads_assignments()
    {
        var projectId = Guid.NewGuid();
        using var context = CreateContext(
            [CreateAssignment(projectId, "task-a", "Grace Hopper")]);
        var surface = CreateSurface(projectId, CreateTask("task-a", "Projected task"));

        var cut = context.Render<ProjectStructureGanttPanel>(parameters => parameters
            .Add(component => component.ProjectId, projectId)
            .Add(component => component.Surface, surface)
            .Add(component => component.MutationCommitted, () => { }));

        var chart = cut.FindComponent<GanttChart>();
        var task = Assert.Single(chart.Instance.Tasks);
        Assert.Equal(new GanttAssignment(GanttAssignmentKind.Person, "Grace Hopper"), Assert.Single(task.Assignments));
        Assert.NotNull(chart.Instance.ProjectionOnlySelector);
        Assert.True(chart.Instance.ProjectionOnlySelector(task));
        Assert.Null(chart.Instance.TaskScheduleReadOnlySelector);
        Assert.Null(chart.Instance.TaskTitleReadOnlySelector);
        Assert.True(chart.Instance.AllowTaskEditing);
        Assert.True(chart.Instance.TaskDoubleClicked.HasDelegate);
        Assert.Contains("moving or resizing a bar saves the complete displayed schedule", cut.Markup, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Schedule_mutation_snapshot_covers_the_entire_rendered_graph()
    {
        var projectId = Guid.NewGuid();
        var start = new DateTimeOffset(2026, 7, 15, 8, 0, 0, TimeSpan.Zero);
        var projectedNode = CreateTask("task-a", "Projected");
        var canonicalNode = CreateTask("task-b", "Canonical") with
        {
            StartUtc = start.AddHours(1),
            EndUtc = start.AddHours(2),
            DurationSeconds = 3600
        };
        var projectedTasks = new[]
        {
            new GanttTask(new GanttTaskId(projectedNode.Id), projectedNode.Title, start, start.AddHours(1)),
            new GanttTask(
                new GanttTaskId(canonicalNode.Id),
                canonicalNode.Title,
                canonicalNode.StartUtc.Value,
                canonicalNode.EndUtc.Value)
        };
        var scheduleChange = new GanttTaskScheduleChangeRequest(
            projectedTasks[0].Id,
            GanttScheduleGesture.ResizeEnd,
            [new GanttTaskDateChange(
                projectedTasks[0].Id,
                start,
                start.AddHours(1),
                start,
                start.AddHours(2),
                true)],
            []);

        var mutation = ProjectStructureGanttScheduleMutationFactory.Create(
            scheduleChange,
            CreateSurface(projectId, projectedNode, canonicalNode),
            projectedTasks);

        Assert.Equal(2, mutation.ExpectedTaskSchedules.Count);
        var projectedSnapshot = Assert.Single(
            mutation.ExpectedTaskSchedules,
            snapshot => snapshot.TaskId == projectedTasks[0].Id);
        Assert.Null(projectedSnapshot.StartUtc);
        Assert.Null(projectedSnapshot.EndUtc);
        Assert.Equal(start, projectedSnapshot.ProjectedStartUtc);
        Assert.Equal(start.AddHours(1), projectedSnapshot.ProjectedEndUtc);
        var canonicalSnapshot = Assert.Single(
            mutation.ExpectedTaskSchedules,
            snapshot => snapshot.TaskId == projectedTasks[1].Id);
        Assert.Equal(canonicalNode.StartUtc, canonicalSnapshot.StartUtc);
        Assert.Equal(canonicalNode.EndUtc, canonicalSnapshot.EndUtc);
    }

    [Fact]
    public async Task Schedule_resize_keeps_committed_dates_while_authoritative_reload_is_pending()
    {
        using var context = CreateContext([]);
        var projectId = Guid.NewGuid();
        var taskNode = CreateTask("task-a", "Resize task") with
        {
            StartUtc = Baseline,
            EndUtc = Baseline.AddHours(2),
            DurationSeconds = 7200
        };
        await SeedProjectTaskAsync(context, projectId, taskNode);
        var reloadStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseReload = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var cut = context.Render<ProjectStructureGanttPanel>(parameters => parameters
            .Add(component => component.ProjectId, projectId)
            .Add(component => component.Surface, CreateSurface(projectId, taskNode))
            .Add(component => component.MutationCommitted, async () =>
            {
                reloadStarted.TrySetResult(true);
                await releaseReload.Task;
            }));
        var chart = cut.FindComponent<GanttChart>();
        var renderedTask = Assert.Single(chart.Instance.Tasks);
        var proposedEnd = renderedTask.End.AddHours(3);
        var request = new GanttTaskScheduleChangeRequest(
            renderedTask.Id,
            GanttScheduleGesture.ResizeEnd,
            [new GanttTaskDateChange(
                renderedTask.Id,
                renderedTask.Start,
                renderedTask.End,
                renderedTask.Start,
                proposedEnd,
                true)],
            []);

        var resizeTask = cut.InvokeAsync(() => chart.Instance.TaskScheduleChangeRequested.InvokeAsync(request));
        await reloadStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        try
        {
            cut.WaitForAssertion(() =>
            {
                var committedTask = Assert.Single(cut.FindComponent<GanttChart>().Instance.Tasks);
                Assert.Equal(proposedEnd, committedTask.End);
                Assert.False(cut.FindComponent<GanttChart>().Instance.AllowTaskEditing);
            });
            await using var database = await context.Services
                .GetRequiredService<IDbContextFactory<AppDbContext>>()
                .CreateDbContextAsync();
            var persistedTask = await database.Set<ProjectObjectRecord>()
                .SingleAsync(record => record.ProjectId == projectId && record.NodeKey == taskNode.Id);
            Assert.Equal(proposedEnd, persistedTask.EndUtc);
        }
        finally
        {
            releaseReload.TrySetResult(true);
        }

        await resizeTask.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(proposedEnd, Assert.Single(cut.FindComponent<GanttChart>().Instance.Tasks).End);
    }

    [Fact]
    public async Task Rejected_schedule_resize_restores_the_rendered_dates()
    {
        using var context = CreateContext([]);
        var projectId = Guid.NewGuid();
        var taskNode = CreateTask("task-a", "Reject stale resize") with
        {
            StartUtc = Baseline,
            EndUtc = Baseline.AddHours(2),
            DurationSeconds = 7200
        };
        await SeedProjectTaskAsync(context, projectId, taskNode);
        var committedCount = 0;
        var cut = context.Render<ProjectStructureGanttPanel>(parameters => parameters
            .Add(component => component.ProjectId, projectId)
            .Add(component => component.Surface, CreateSurface(projectId, taskNode))
            .Add(component => component.MutationCommitted, () => committedCount++));
        var chart = cut.FindComponent<GanttChart>();
        var renderedTask = Assert.Single(chart.Instance.Tasks);
        var request = new GanttTaskScheduleChangeRequest(
            renderedTask.Id,
            GanttScheduleGesture.ResizeEnd,
            [new GanttTaskDateChange(
                renderedTask.Id,
                renderedTask.Start,
                renderedTask.End.AddMinutes(1),
                renderedTask.Start,
                renderedTask.End.AddHours(3),
                true)],
            []);

        await cut.InvokeAsync(() => chart.Instance.TaskScheduleChangeRequested.InvokeAsync(request));

        var restoredTask = Assert.Single(cut.FindComponent<GanttChart>().Instance.Tasks);
        Assert.Equal(renderedTask.Start, restoredTask.Start);
        Assert.Equal(renderedTask.End, restoredTask.End);
        Assert.Equal(0, committedCount);
        var notification = Assert.Single(context.Services.GetRequiredService<NotificationService>().Messages);
        Assert.Equal(NotificationSeverity.Error, notification.Severity);
        Assert.Equal("Project schedule change rejected", notification.Summary);
    }

    [Fact]
    public async Task Schedule_resize_keeps_committed_dates_when_authoritative_reload_fails()
    {
        using var context = CreateContext([]);
        var projectId = Guid.NewGuid();
        var taskNode = CreateTask("task-a", "Committed resize") with
        {
            StartUtc = Baseline,
            EndUtc = Baseline.AddHours(2),
            DurationSeconds = 7200
        };
        await SeedProjectTaskAsync(context, projectId, taskNode);
        var cut = context.Render<ProjectStructureGanttPanel>(parameters => parameters
            .Add(component => component.ProjectId, projectId)
            .Add(component => component.Surface, CreateSurface(projectId, taskNode))
            .Add(
                component => component.MutationCommitted,
                () => Task.FromException(new InvalidOperationException("Reload failed."))));
        var chart = cut.FindComponent<GanttChart>();
        var renderedTask = Assert.Single(chart.Instance.Tasks);
        var proposedEnd = renderedTask.End.AddHours(3);
        var request = new GanttTaskScheduleChangeRequest(
            renderedTask.Id,
            GanttScheduleGesture.ResizeEnd,
            [new GanttTaskDateChange(
                renderedTask.Id,
                renderedTask.Start,
                renderedTask.End,
                renderedTask.Start,
                proposedEnd,
                true)],
            []);

        await cut.InvokeAsync(() => chart.Instance.TaskScheduleChangeRequested.InvokeAsync(request));

        Assert.Equal(proposedEnd, Assert.Single(cut.FindComponent<GanttChart>().Instance.Tasks).End);
        var notification = Assert.Single(context.Services.GetRequiredService<NotificationService>().Messages);
        Assert.Equal(NotificationSeverity.Warning, notification.Severity);
        Assert.Equal("Project schedule saved; reload required", notification.Summary);
    }

    [Fact]
    public void Assignment_load_failure_blocks_chart_with_safe_explicit_error()
    {
        using var context = CreateContext([], new InvalidOperationException("private connection detail"));
        var projectId = Guid.NewGuid();

        var cut = context.Render<ProjectStructureGanttPanel>(parameters => parameters
            .Add(component => component.ProjectId, projectId)
            .Add(component => component.Surface, CreateSurface(projectId, CreateTask("task-a", "Task")))
            .Add(component => component.MutationCommitted, () => { }));

        Assert.Empty(cut.FindComponents<GanttChart>());
        var alert = cut.Find("[data-testid='project-structure-gantt-load-error']");
        Assert.Contains("could not be loaded", alert.TextContent, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("private connection detail", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Mutation_feedback_uses_notification_service_instead_of_inline_alerts()
    {
        using var context = CreateContext([]);
        var projectId = Guid.NewGuid();
        var cut = context.Render<ProjectStructureGanttPanel>(parameters => parameters
            .Add(component => component.ProjectId, projectId)
            .Add(component => component.Surface, CreateSurface(projectId, CreateTask("task-a", "Task"))));
        var chart = cut.FindComponent<GanttChart>();
        var task = Assert.Single(chart.Instance.Tasks);

        await cut.InvokeAsync(() => chart.Instance.TaskTitleChangeRequested.InvokeAsync(
            new GanttTaskTitleChangeRequest(task.Id, task.Title, "Updated task")));

        var notification = Assert.Single(context.Services.GetRequiredService<NotificationService>().Messages);
        Assert.Equal(NotificationSeverity.Error, notification.Severity);
        Assert.Equal("Project schedule change unavailable", notification.Summary);
        Assert.Contains("authoritative project data", notification.Detail, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("project-structure-gantt-mutation-error", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("project-structure-gantt-mutation-status", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Same_project_refresh_keeps_chart_mounted_until_authoritative_projection_is_ready()
    {
        var projectId = Guid.NewGuid();
        var refreshAssignments = new TaskCompletionSource<IReadOnlyList<ProjectPartyAssignmentDetail>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var bridge = new StubProjectPartyIntegrationBridge([], null, refreshAssignments);
        using var context = CreateContext(bridge);
        var cut = context.Render<ProjectStructureGanttPanel>(parameters => parameters
            .Add(component => component.ProjectId, projectId)
            .Add(component => component.Surface, CreateSurface(projectId, CreateTask("task-a", "Before refresh")))
            .Add(component => component.MutationCommitted, () => { }));
        var originalChart = cut.FindComponent<GanttChart>().Instance;

        var refreshTask = cut.InvokeAsync(() => cut.Render(parameters => parameters
            .Add(component => component.ProjectId, projectId)
            .Add(component => component.Surface, CreateSurface(projectId, CreateTask("task-a", "After refresh")))
            .Add(component => component.MutationCommitted, () => { })));
        await bridge.RefreshRequested.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Same(originalChart, cut.FindComponent<GanttChart>().Instance);
        Assert.False(cut.FindComponent<GanttChart>().Instance.AllowTaskEditing);
        Assert.Contains("Before refresh", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("Building the Gantt projection", cut.Markup, StringComparison.Ordinal);
        await cut.InvokeAsync(() => originalChart.TaskDoubleClicked.InvokeAsync(new GanttTaskId("task-a")));
        var refreshNotification = Assert.Single(
            context.Services.GetRequiredService<NotificationService>().Messages);
        Assert.Equal("Project schedule refresh in progress", refreshNotification.Summary);

        refreshAssignments.SetResult([]);
        await refreshTask;

        cut.WaitForAssertion(() =>
        {
            Assert.Same(originalChart, cut.FindComponent<GanttChart>().Instance);
            Assert.True(cut.FindComponent<GanttChart>().Instance.AllowTaskEditing);
            Assert.Contains("After refresh", cut.Markup, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task Task_double_click_opens_authoritative_details_with_delivery_progress_and_pure_effort()
    {
        var projectId = Guid.NewGuid();
        var assignment = CreateAssignment(projectId, "task-a", "Joe Doe");
        using var context = CreateContext([assignment]);
        var dialogHost = context.Render<DialogHost>();
        var metadata = ProjectObjectMetadataSerializer.Serialize(new ProjectObjectMetadataEnvelope
        {
            WorkItem = new ProjectWorkItemMetadata
            {
                WorkItemKind = ProjectWorkItemKind.Task,
                ExpectedEffortHours = 8m,
                ExpectedEffortUnit = ProjectWorkItemEffortUnit.ManDays,
                ExpectedCostAmount = 900m,
                ExpectedCostCurrencyCode = "USD"
            }
        });
        var startUtc = new DateTimeOffset(2026, 7, 15, 8, 0, 0, TimeSpan.Zero);
        var task = CreateTask("task-a", "Customer acceptance") with
        {
            StartUtc = startUtc,
            EndUtc = startUtc.AddDays(7),
            DurationSeconds = (int)TimeSpan.FromDays(7).TotalSeconds,
            ProgressMode = "progress",
            ProgressPercent = 40,
            MetadataJson = metadata
        };
        var cut = context.Render<ProjectStructureGanttPanel>(parameters => parameters
            .Add(component => component.ProjectId, projectId)
            .Add(component => component.Surface, CreateSurface(projectId, task))
            .Add(component => component.MutationCommitted, () => { }));
        var chart = cut.FindComponent<GanttChart>();

        var openTask = cut.InvokeAsync(() => chart.Instance.TaskDoubleClicked.InvokeAsync(new GanttTaskId(task.Id)));

        dialogHost.WaitForElement("[data-testid='project-structure-gantt-task-progress']");
        Assert.Equal(
            "40",
            dialogHost.Find("[data-testid='project-structure-gantt-task-progress']").GetAttribute("value"));
        Assert.Equal(
            "1",
            dialogHost.Find("[data-testid='project-structure-gantt-task-estimate-effort']").GetAttribute("value"));
        Assert.Contains("Joe Doe", dialogHost.Markup, StringComparison.Ordinal);
        dialogHost.Find("[data-testid='project-structure-gantt-task-cancel']").Click();
        await openTask.WaitAsync(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task Task_double_click_with_person_and_agent_opens_read_only_assignment_details()
    {
        var projectId = Guid.NewGuid();
        var primaryPerson = CreateAssignment(
            projectId,
            "task-a",
            "Joe Doe",
            ProjectPartyType.Person,
            isPrimary: true);
        var agent = CreateAssignment(
            projectId,
            "task-a",
            "Delivery agent",
            ProjectPartyType.AiAgent,
            isPrimary: false);
        using var context = CreateContext([agent, primaryPerson]);
        var dialogHost = context.Render<DialogHost>();
        var task = CreateTask("task-a", "Customer acceptance");
        var cut = context.Render<ProjectStructureGanttPanel>(parameters => parameters
            .Add(component => component.ProjectId, projectId)
            .Add(component => component.Surface, CreateSurface(projectId, task))
            .Add(component => component.MutationCommitted, () => { }));
        var chart = cut.FindComponent<GanttChart>();
        var projectedTask = Assert.Single(chart.Instance.Tasks);
        Assert.Equal(2, projectedTask.Assignments.Count);

        var openTask = cut.InvokeAsync(() =>
            chart.Instance.TaskDoubleClicked.InvokeAsync(new GanttTaskId(task.Id)));

        dialogHost.WaitForElement("[data-testid='project-structure-gantt-task-assignee-readonly']");
        var dialog = dialogHost.FindComponent<ProjectStructureGanttTaskDialog>().Instance;
        Assert.False(dialog.EditModel!.CanChangeDirectAssignee);
        Assert.Equal(
            new ProjectStructureTaskResourceSelection(
                ProjectStructureTaskResourceKind.Person,
                primaryPerson.PartyId),
            dialog.EditModel.Assignee);
        Assert.DoesNotContain(
            context.Services.GetRequiredService<NotificationService>().Messages,
            message => string.Equals(
                message.Summary,
                "Task details unavailable",
                StringComparison.Ordinal));

        dialogHost.Find("[data-testid='project-structure-gantt-task-cancel']").Click();
        await openTask.WaitAsync(TimeSpan.FromSeconds(2));
    }

    private static BunitContext CreateContext(
        IReadOnlyList<ProjectPartyAssignmentDetail> assignments,
        Exception? assignmentFailure = null)
        => CreateContext(new StubProjectPartyIntegrationBridge(assignments, assignmentFailure));

    private static BunitContext CreateContext(IProjectPartyIntegrationBridge bridge)
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddCanDoItAllBaseLib();
        context.Services.AddLogging();
        context.Services.AddSingleton<NotificationService>();
        context.Services.AddSingleton(bridge);
        context.Services.AddSingleton<ProjectStructureGanttProjectionAdapter>();
        var (workbenchService, dbContextFactory, clock) = CreateWorkbenchService();
        context.Services.AddSingleton(dbContextFactory);
        context.Services.AddSingleton<IClock>(clock);
        var mutationService = new ProjectStructureGanttMutationService(
            dbContextFactory,
            clock,
            NullLogger<ProjectStructureGanttMutationService>.Instance);
        context.Services.AddSingleton(mutationService);
        context.Services.AddSingleton(workbenchService);
        var assigneeService = new ProjectStructureWorkItemAssigneeService(
            bridge,
            workbenchService);
        var taskResourceService = new ProjectStructureTaskResourceService(
            assigneeService,
            null!,
            null!,
            null!,
            null!,
            workbenchService);
        context.Services.AddSingleton(taskResourceService);
        var rowOrderService = new ProjectStructureGanttRowOrderService(null!, workbenchService);
        context.Services.AddSingleton(rowOrderService);
        var taskResourceCostService = new ProjectStructureTaskResourceCostService([]);
        context.Services.AddSingleton(taskResourceCostService);
        var estimateRefreshService = new ProjectStructureTaskEstimateRefreshService(taskResourceCostService);
        context.Services.AddSingleton(estimateRefreshService);
        context.Services.AddSingleton(new ProjectStructureTaskCreationService(
            null!,
            null!,
            rowOrderService,
            workbenchService,
            estimateRefreshService,
            NullLogger<ProjectStructureTaskCreationService>.Instance));
        var taskApplicationService = new ProjectStructureTaskApplicationService(
            assigneeService,
            estimateRefreshService,
            new ProjectStructureTaskEditCompensationService(
                dbContextFactory,
                clock),
            workbenchService,
            NullLogger<ProjectStructureTaskApplicationService>.Instance);
        var taskDetailsService = new ProjectStructureTaskDetailsService(
            mutationService,
            taskApplicationService);
        context.Services.AddSingleton(taskDetailsService);
        var currencyFormatter = new StubCurrencyFormatter();
        context.Services.AddSingleton<ICurrencyFormatter>(currencyFormatter);
        var taskPricingCommitService = new ProjectStructureTaskPricingCommitService(
            workbenchService,
            estimateRefreshService,
            new ProjectStructureTaskPricingPersistenceService(dbContextFactory, clock),
            NullLogger<ProjectStructureTaskPricingCommitService>.Instance);
        context.Services.AddSingleton(taskPricingCommitService);
        var taskResourceAttachmentService = new ProjectStructureTaskResourceAttachmentService(
            taskResourceService,
            taskPricingCommitService,
            NullLogger<ProjectStructureTaskResourceAttachmentService>.Instance);
        context.Services.AddSingleton(taskResourceAttachmentService);
        context.Services.AddScoped(serviceProvider => new ProjectStructureGanttTaskEditCoordinator(
            taskResourceService,
            taskResourceCostService,
            taskDetailsService,
            taskResourceAttachmentService,
            serviceProvider.GetRequiredService<DialogService>(),
            serviceProvider.GetRequiredService<NotificationService>(),
            currencyFormatter,
            NullLogger<ProjectStructureGanttTaskEditCoordinator>.Instance));
        return context;
    }

    private static async Task SeedProjectTaskAsync(
        BunitContext context,
        Guid projectId,
        ProjectStructureNode taskNode)
    {
        await using var database = await context.Services
            .GetRequiredService<IDbContextFactory<AppDbContext>>()
            .CreateDbContextAsync();
        database.Set<Project>().Add(new Project
        {
            Id = projectId,
            Name = "Gantt panel test",
            Slug = $"gantt-panel-{projectId:N}",
            CreatedAtUtc = Baseline,
            UpdatedAtUtc = Baseline
        });
        database.Set<ProjectObjectRecord>().Add(new ProjectObjectRecord
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            NodeKey = taskNode.Id,
            ObjectType = ProjectObjectType.WorkItem,
            ObjectSubtype = "task",
            Title = taskNode.Title,
            Status = "Draft",
            MetadataJson = "{}",
            MarkersJson = "[]",
            StartUtc = taskNode.StartUtc,
            EndUtc = taskNode.EndUtc,
            DurationSeconds = taskNode.DurationSeconds,
            CreatedAtUtc = Baseline,
            UpdatedAtUtc = Baseline
        });
        await database.SaveChangesAsync();
    }

    private static (
        ProjectWorkbenchService Service,
        IDbContextFactory<AppDbContext> DbContextFactory,
        IClock Clock) CreateWorkbenchService()
    {
        AppDbContextModelRegistry.ConfigureAssemblies(
        [
            typeof(Project).Assembly,
            typeof(ProjectObjectRecord).Assembly
        ]);
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"gantt-panel-{Guid.NewGuid():N}")
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        var factory = new TestDbContextFactory(options);
        var clock = new FixedClock();
        return (
            new ProjectWorkbenchService(
                factory,
                clock,
                new ProjectAssetStorageService(
                    null!,
                    new ProjectAssetCreationService(),
                    null!),
                null!,
                null!,
                null!,
                null!,
                null!,
                null!),
            factory,
            clock);
    }

    private static ProjectStructureSurface CreateSurface(Guid projectId, params ProjectStructureNode[] nodes)
        => new(projectId, "Gantt test", nodes, [], null);

    private static ProjectStructureSurface CreateSurface(
        Guid projectId,
        IReadOnlyList<ProjectStructureNode> nodes,
        IReadOnlyList<ProjectStructureLink> links)
        => new(projectId, "Gantt test", nodes, links, null);

    private static ProjectStructureNode CreateTask(string id, string title)
        => new(
            Id: id,
            ParentId: null,
            ObjectType: ProjectObjectType.WorkItem,
            ObjectSubtype: "task",
            Title: title,
            Subtitle: string.Empty,
            Status: "Planned",
            Notes: string.Empty,
            Route: string.Empty,
            ArtifactKind: string.Empty,
            ArtifactId: null,
            MediaRelativePath: string.Empty,
            MediaContentType: string.Empty,
            MediaOriginalFileName: string.Empty,
            X: 0,
            Y: 0,
            VisualProfile: new ProjectObjectVisualProfile("pill", "#2563eb", "TK", "Task"),
            Badges: [],
            ProgressMode: string.Empty,
            ProgressPercent: 0,
            MarkerIcon: string.Empty,
            MarkerTone: string.Empty,
            MarkerLabel: string.Empty,
            Markers: [],
            Priority: 0);

    private static ProjectPartyAssignmentDetail CreateAssignment(
        Guid projectId,
        string nodeKey,
        string displayName,
        ProjectPartyType partyType = ProjectPartyType.Person,
        bool isPrimary = false)
        => new(
            Guid.NewGuid(),
            projectId,
            Guid.NewGuid(),
            ProjectPartyAssignmentRole.WorkItemAssignee,
            displayName,
            partyType == ProjectPartyType.Person ? "Person" : "AI agent",
            partyType,
            nodeKey,
            isPrimary,
            null,
            null,
            null,
            string.Empty,
            string.Empty);

    private sealed class StubProjectPartyIntegrationBridge(
        IReadOnlyList<ProjectPartyAssignmentDetail> assignments,
        Exception? assignmentFailure,
        TaskCompletionSource<IReadOnlyList<ProjectPartyAssignmentDetail>>? refreshAssignments = null)
        : IProjectPartyIntegrationBridge
    {
        private readonly TaskCompletionSource<bool> refreshRequested = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private int assignmentRequestCount;

        public Task RefreshRequested => refreshRequested.Task;

        public Task<IReadOnlyList<ProjectPartyAssignmentDetail>> ListAssignmentsDetailedAsync(
            Guid projectId,
            CancellationToken cancellationToken = default)
        {
            if (Interlocked.Increment(ref assignmentRequestCount) > 1 && refreshAssignments is not null)
            {
                refreshRequested.TrySetResult(true);
                return refreshAssignments.Task;
            }

            return assignmentFailure is null
                ? Task.FromResult(assignments)
                : Task.FromException<IReadOnlyList<ProjectPartyAssignmentDetail>>(assignmentFailure);
        }

        public async Task<IReadOnlyList<ProjectPartyAssignmentDetail>> ListAssignmentsDetailedAsync(
            Guid projectId,
            IReadOnlyCollection<ProjectPartyAssignmentRole> roles,
            CancellationToken cancellationToken = default)
        {
            var allAssignments = await ListAssignmentsDetailedAsync(projectId, cancellationToken);
            var allowedRoles = roles.ToHashSet();
            return allAssignments.Where(assignment => allowedRoles.Contains(assignment.Role)).ToArray();
        }

        public async Task<IReadOnlyList<ProjectWorkItemAssigneeBinding>> ListWorkItemAssigneeBindingsAsync(
            Guid projectId,
            CancellationToken cancellationToken = default)
        {
            var allAssignments = await ListAssignmentsDetailedAsync(projectId, cancellationToken);
            return allAssignments
                .Where(assignment => assignment.Role == ProjectPartyAssignmentRole.WorkItemAssignee)
                .Select(assignment => new ProjectWorkItemAssigneeBinding(
                    assignment.ProjectId,
                    assignment.NodeKey,
                    assignment.PartyId,
                    assignment.PartyType))
                .ToArray();
        }

        public Task<IReadOnlyDictionary<Guid, ProjectPortfolioPartyContext>> GetPortfolioContextsAsync(
            IReadOnlyCollection<Guid> projectIds,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<ProjectPartyOption>> ListPartyOptionsAsync(
            Guid projectId,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<ProjectPartyOption?> GetPartyOptionAsync(
            Guid partyId,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<Result<Guid>> SaveAssignmentAsync(
            ProjectPartyAssignmentUpsertRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<Result> ReplaceNodeAssignmentsAsync(
            Guid projectId,
            ProjectNodeReference nodeReference,
            IReadOnlyList<ProjectPartyAssignmentUpsertRequest> desiredAssignments,
            IReadOnlyList<ProjectPartyAssignmentRole> targetRoles,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task DeleteAssignmentAsync(
            Guid assignmentId,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task DeleteAssignmentsForNodesAsync(
            Guid projectId,
            IReadOnlyCollection<ProjectNodeReference> nodeReferences,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task DeleteAssignmentsForProjectAsync(
            Guid projectId,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task MoveAssignmentsToProjectAsync(
            ProjectPartyAssignmentMoveOperationId operationId,
            Guid sourceProjectId,
            IReadOnlyCollection<ProjectNodeReference> nodeReferences,
            Guid targetProjectId,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<Result<ProjectPartyQuickCreateResult>> CreatePartyAsync(
            ProjectPartyQuickCreateRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class TestDbContextFactory(DbContextOptions<AppDbContext> options)
        : IDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext()
            => new(options);

        public Task<AppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(CreateDbContext());
    }

    private sealed class FixedClock : IClock
    {
        public DateTimeOffset GetUtcNow()
            => new(2026, 7, 15, 12, 0, 0, TimeSpan.Zero);
    }

    private sealed class StubCurrencyFormatter : ICurrencyFormatter
    {
        public string CurrencyCode => "USD";

        public string Format(decimal value)
            => $"USD {value:0.##}";
    }
}
