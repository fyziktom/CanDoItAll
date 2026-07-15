using Bunit;
using Bunit.TestDoubles;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Components.Gantt;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workbench.CanvasAdapters;
using CanDoItAll.Modules.Workbench.Pages;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace CanDoItAll.Tests.Components;

public sealed class ProjectStructureGanttPanelTests
{
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

    private static TestContext CreateContext(
        IReadOnlyList<ProjectPartyAssignmentDetail> assignments,
        Exception? assignmentFailure = null)
    {
        var context = new TestContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddLogging();
        context.Services.AddSingleton<NotificationService>();
        context.Services.AddSingleton<IProjectPartyIntegrationBridge>(
            new StubProjectPartyIntegrationBridge(assignments, assignmentFailure));
        context.Services.AddSingleton<ProjectStructureGanttProjectionAdapter>();
        context.Services.AddSingleton(new ProjectStructureGanttMutationService(
            null!,
            null!,
            NullLogger<ProjectStructureGanttMutationService>.Instance));
        return context;
    }

    private static ProjectStructureSurface CreateSurface(Guid projectId, params ProjectStructureNode[] nodes)
        => new(projectId, "Gantt test", nodes, [], null);

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
            string.Empty);

    private sealed class StubProjectPartyIntegrationBridge(
        IReadOnlyList<ProjectPartyAssignmentDetail> assignments,
        Exception? assignmentFailure) : IProjectPartyIntegrationBridge
    {
        public Task<IReadOnlyList<ProjectPartyAssignmentDetail>> ListAssignmentsDetailedAsync(
            Guid projectId,
            CancellationToken cancellationToken = default)
            => assignmentFailure is null
                ? Task.FromResult(assignments)
                : Task.FromException<IReadOnlyList<ProjectPartyAssignmentDetail>>(assignmentFailure);

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
}
