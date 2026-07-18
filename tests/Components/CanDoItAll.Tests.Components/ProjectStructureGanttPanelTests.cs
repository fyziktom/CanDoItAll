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

namespace CanDoItAll.Tests.Components;

public sealed class ProjectStructureGanttPanelTests
{
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

        var cut = context.RenderComponent<ProjectStructureGanttPanel>(parameters => parameters
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

        var cut = context.RenderComponent<ProjectStructureGanttPanel>(parameters => parameters
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
        var cut = context.RenderComponent<ProjectStructureGanttPanel>(parameters => parameters
            .Add(component => component.ProjectId, projectId)
            .Add(component => component.Surface, surface)
            .Add(component => component.MutationCommitted, () => { }));

        cut.Find("[data-testid='project-structure-gantt-export-mermaid']").Click();
        Assert.NotEmpty(cut.FindAll("[data-testid='project-structure-gantt-mermaid-dialog']"));

        cut.SetParametersAndRender(parameters => parameters
            .Add(component => component.ProjectId, Guid.Empty)
            .Add(component => component.Surface, surface)
            .Add(component => component.MutationCommitted, () => { }));

        Assert.Empty(cut.FindAll("[data-testid='project-structure-gantt-mermaid-dialog']"));
    }

    [Fact]
    public void Projection_only_task_disables_schedule_edits_but_keeps_title_editing_and_loads_assignments()
    {
        var projectId = Guid.NewGuid();
        using var context = CreateContext(
            [CreateAssignment(projectId, "task-a", "Grace Hopper")]);
        var surface = CreateSurface(projectId, CreateTask("task-a", "Projected task"));

        var cut = context.RenderComponent<ProjectStructureGanttPanel>(parameters => parameters
            .Add(component => component.ProjectId, projectId)
            .Add(component => component.Surface, surface)
            .Add(component => component.MutationCommitted, () => { }));

        var chart = cut.FindComponent<GanttChart>();
        var task = Assert.Single(chart.Instance.Tasks);
        Assert.Equal(new GanttAssignment(GanttAssignmentKind.Person, "Grace Hopper"), Assert.Single(task.Assignments));
        Assert.NotNull(chart.Instance.TaskScheduleReadOnlySelector);
        Assert.True(chart.Instance.TaskScheduleReadOnlySelector(task));
        Assert.Null(chart.Instance.TaskTitleReadOnlySelector);
        Assert.True(chart.Instance.AllowTaskEditing);
        Assert.True(chart.Instance.TaskDoubleClicked.HasDelegate);
        Assert.Contains("read-only projection", cut.Markup, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Assignment_load_failure_blocks_chart_with_safe_explicit_error()
    {
        using var context = CreateContext([], new InvalidOperationException("private connection detail"));
        var projectId = Guid.NewGuid();

        var cut = context.RenderComponent<ProjectStructureGanttPanel>(parameters => parameters
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
        var cut = context.RenderComponent<ProjectStructureGanttPanel>(parameters => parameters
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
        var cut = context.RenderComponent<ProjectStructureGanttPanel>(parameters => parameters
            .Add(component => component.ProjectId, projectId)
            .Add(component => component.Surface, CreateSurface(projectId, CreateTask("task-a", "Before refresh")))
            .Add(component => component.MutationCommitted, () => { }));
        var originalChart = cut.FindComponent<GanttChart>().Instance;

        var refreshTask = cut.InvokeAsync(() => cut.SetParametersAndRender(parameters => parameters
            .Add(component => component.ProjectId, projectId)
            .Add(component => component.Surface, CreateSurface(projectId, CreateTask("task-a", "After refresh")))
            .Add(component => component.MutationCommitted, () => { })));
        await bridge.RefreshRequested.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Same(originalChart, cut.FindComponent<GanttChart>().Instance);
        Assert.Contains("Before refresh", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("Building the Gantt projection", cut.Markup, StringComparison.Ordinal);

        refreshAssignments.SetResult([]);
        await refreshTask;

        cut.WaitForAssertion(() =>
        {
            Assert.Same(originalChart, cut.FindComponent<GanttChart>().Instance);
            Assert.Contains("After refresh", cut.Markup, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task Task_double_click_opens_authoritative_details_with_delivery_progress_and_pure_effort()
    {
        var projectId = Guid.NewGuid();
        var assignment = CreateAssignment(projectId, "task-a", "Joe Doe");
        using var context = CreateContext([assignment]);
        var dialogHost = context.RenderComponent<DialogHost>();
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
        var cut = context.RenderComponent<ProjectStructureGanttPanel>(parameters => parameters
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

    private static TestContext CreateContext(
        IReadOnlyList<ProjectPartyAssignmentDetail> assignments,
        Exception? assignmentFailure = null)
        => CreateContext(new StubProjectPartyIntegrationBridge(assignments, assignmentFailure));

    private static TestContext CreateContext(IProjectPartyIntegrationBridge bridge)
    {
        var context = new TestContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddCanDoItAllBaseLib();
        context.Services.AddLogging();
        context.Services.AddSingleton<NotificationService>();
        context.Services.AddSingleton(bridge);
        context.Services.AddSingleton<ProjectStructureGanttProjectionAdapter>();
        var mutationService = new ProjectStructureGanttMutationService(
            null!,
            null!,
            NullLogger<ProjectStructureGanttMutationService>.Instance);
        context.Services.AddSingleton(mutationService);
        var workbenchService = CreateWorkbenchService();
        context.Services.AddSingleton(workbenchService);
        var assigneeService = new ProjectStructureWorkItemAssigneeService(
            bridge,
            workbenchService,
            NullLogger<ProjectStructureWorkItemAssigneeService>.Instance);
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
        context.Services.AddSingleton(new ProjectStructureTaskCreationService(
            null!,
            null!,
            rowOrderService,
            workbenchService,
            NullLogger<ProjectStructureTaskCreationService>.Instance));
        var taskDetailsService = new ProjectStructureTaskDetailsService(
            mutationService,
            assigneeService,
            bridge,
            workbenchService,
            NullLogger<ProjectStructureTaskDetailsService>.Instance);
        context.Services.AddSingleton(taskDetailsService);
        var currencyFormatter = new StubCurrencyFormatter();
        context.Services.AddSingleton<ICurrencyFormatter>(currencyFormatter);
        var taskResourceCostService = new ProjectStructureTaskResourceCostService(
            null!,
            null!,
            null!,
            null!,
            null!,
            TimeProvider.System);
        context.Services.AddSingleton(taskResourceCostService);
        context.Services.AddScoped(serviceProvider => new ProjectStructureGanttTaskEditCoordinator(
            taskResourceService,
            taskResourceCostService,
            taskDetailsService,
            serviceProvider.GetRequiredService<DialogService>(),
            serviceProvider.GetRequiredService<NotificationService>(),
            currencyFormatter,
            NullLogger<ProjectStructureGanttTaskEditCoordinator>.Instance));
        return context;
    }

    private static ProjectWorkbenchService CreateWorkbenchService()
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
        return new ProjectWorkbenchService(
            factory,
            new FixedClock(),
            null!,
            null!,
            null!,
            null!,
            null!,
            null!);
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
        string displayName)
        => new(
            Guid.NewGuid(),
            projectId,
            Guid.NewGuid(),
            ProjectPartyAssignmentRole.WorkItemAssignee,
            displayName,
            "Person",
            ProjectPartyType.Person,
            nodeKey,
            false,
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

        public Task MoveAssignmentsToProjectAsync(
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
