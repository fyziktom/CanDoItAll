using System.Data.Common;
using System.Text.Json;
using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Editing;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workbench.Pages;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using CanDoItAll.Workbench.Planning.UI;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.ProjectStructure;

[Trait("Category", "HostPlatform")]
public sealed class ProjectStructureTaskCreationAccessTests {
    private const string DraftTitle = "Task creation with current authority";
    private const string GanttSurfaceKind = "gantt";
    public enum CreationCommitStage { Task, Backlog, RowOrder, Assignment }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Native_Task_creator_refuses_revoked_access_before_submission_or_after_Task_SQL(bool gantt, bool afterSql) {
        var access = new EditingUiTestAccess();
        var probe = new RevokeAfterSql(access);
        await using var harness = await ComponentTestHarness.CreateAsync(services => {
            services.AddSingleton<IEditingUiAccess>(access);
            services.ConfigureDbContext<WorkbenchDbContext>(options => options.AddInterceptors(probe), ServiceLifetime.Singleton);
        });
        var project = await CreateProjectAsync(harness);
        var form = await OpenAsync(harness, project, gantt);
        try {
            await form.EnterTitleAsync();
            probe.Armed = afterSql;
            if (!afterSql) {
                access.Allowed = false;
            }
            await form.SaveAsync();
            var current = await form.Workbench.GetStructureAsync(project);
            Assert.DoesNotContain(current.Nodes, node => node.Title == DraftTitle);
            Assert.Equal(afterSql, probe.Reached);
            Assert.False(form.Opening.IsCompleted);
            Assert.Equal(DraftTitle, form.Host.Find($"[data-testid='{form.Prefix}-title']").GetAttribute("value"));
            var result = form.Host.FindComponent<PlanningTaskFormActions>().Instance.Result;
            Assert.NotNull(result);
            Assert.Equal(PlanningTaskSaveStatus.Rejected, result.Status);
            Assert.Empty(result.TaskIds);
            Assert.True(form.Host.Find($"[data-testid='{form.Prefix}-submit']").HasAttribute("disabled"));
        } finally {
            await form.CloseAsync();
        }
    }

    [Theory]
    [InlineData(CreationCommitStage.Backlog)]
    [InlineData(CreationCommitStage.RowOrder)]
    public async Task Gantt_creation_checks_access_after_backlog_or_row_order_SQL_and_retains_native_compensation(CreationCommitStage stage) {
        var access = new EditingUiTestAccess();
        var probe = new RevokeAfterSql(access) { Stage = stage };
        await using var harness = await ComponentTestHarness.CreateAsync(services => {
            services.AddSingleton<IEditingUiAccess>(access);
            services.ConfigureDbContext<WorkbenchDbContext>(options => options.AddInterceptors(probe), ServiceLifetime.Singleton);
        });
        var project = await CreateProjectAsync(harness);
        var form = await OpenAsync(harness, project, gantt: true);
        try {
            await form.EnterTitleAsync();
            probe.Armed = true;
            await form.SaveAsync();
            Assert.True(probe.Reached);
            var surface = await form.Workbench.GetStructureAsync(project);
            Assert.DoesNotContain(surface.Nodes, node => node.Title == DraftTitle);
            var result = form.Host.FindComponent<PlanningTaskFormActions>().Instance.Result!;
            Assert.Equal(PlanningTaskSaveStatus.Rejected, result.Status);
            if (stage == CreationCommitStage.Backlog) {
                Assert.DoesNotContain(surface.Nodes, node => node.ObjectSubtype == "backlog");
                Assert.Empty(result.TaskIds);
            } else {
                Assert.Single(surface.Nodes, node => node.ObjectSubtype == "backlog");
                Assert.Single(result.TaskIds);
                Assert.Contains(result.Phases, phase => phase.Phase == PlanningTaskPhase.Task && phase.State == PlanningTaskPhaseState.Compensated);
            }
            await form.Host.InvokeAsync(() => form.Host.FindComponent<PlanningTaskFormActions>().Instance.Submitted.InvokeAsync());
            Assert.DoesNotContain((await form.Workbench.GetStructureAsync(project)).Nodes, node => node.Title == DraftTitle);
            Assert.Equal(DraftTitle, form.Host.Find($"[data-testid='{form.Prefix}-title']").GetAttribute("value"));
        } finally {
            await form.CloseAsync();
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Native_creation_rolls_back_a_denied_assignment_and_compensates_only_the_created_Task(bool gantt) {
        var access = new EditingUiTestAccess();
        var probe = new RevokeAfterSql(access) { Stage = CreationCommitStage.Assignment };
        await using var harness = await ComponentTestHarness.CreateAsync(services => {
            services.AddSingleton<IEditingUiAccess>(access);
            services.ConfigureDbContext<WorkbenchDbContext>(options => options.AddInterceptors(probe), ServiceLifetime.Singleton);
        });
        var services = harness.Context.Services;
        var person = await services.GetRequiredService<PartyDirectoryService>().SavePartyAsync(new PartyEditorModel {
            PartyType = PartyType.Person, LifecycleStatus = PartyLifecycleStatus.Active,
            DisplayName = "Task creation assignee", LastChangedBy = "component-tests"
        });
        Assert.True(person.IsSuccess);
        probe.Party = person.Value;
        var project = await CreateProjectAsync(harness);
        var form = await OpenAsync(harness, project, gantt);
        try {
            await form.EnterTitleAsync();
            var option = gantt ? "project-structure-gantt-task-resource" : "project-structure-task-create-assignee";
            await TaskDialogTestDriver.ClickAsync(form.Host, $"{option}-person-{person.Value:N}");
            probe.Armed = true;
            await form.SaveAsync();
            Assert.True(probe.Reached);
            Assert.DoesNotContain((await form.Workbench.GetStructureAsync(project)).Nodes, node => node.Title == DraftTitle);
            Assert.Empty(await services.GetRequiredService<IProjectPartyIntegrationBridge>().ListAssignmentsDetailedAsync(project));
            var result = form.Host.FindComponent<PlanningTaskFormActions>().Instance.Result!;
            Assert.Equal(PlanningTaskSaveStatus.Rejected, result.Status);
            Assert.Contains(result.Phases, phase => phase.Phase == PlanningTaskPhase.Task && phase.State == PlanningTaskPhaseState.Compensated);
            Assert.Equal(DraftTitle, form.Host.Find($"[data-testid='{form.Prefix}-title']").GetAttribute("value"));
        } finally {
            await form.CloseAsync();
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_held_private_Task_read_cannot_disclose_a_body_after_its_grant_or_opening_changes(bool retireOpening) {
        var ui = new EditingUiTestAccess();
        await using var harness = await ComponentTestHarness.CreateAsync(services => services.AddSingleton<IEditingUiAccess>(ui));
        var project = await CreateProjectAsync(harness);
        var services = harness.Context.Services;
        var admission = (await services.GetRequiredService<ProjectWorkbenchService>().GetStructureAsync(project)).ExpectedProjectAdmission!;
        var current = true;
        await using var access = services.GetRequiredService<ProjectStructureTaskEditingSessions>().CreateAccess(admission, () => current);
        await access.OpenAsync(CancellationToken.None);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var pending = access.ReadPrivateAsync(async _ => {
            entered.TrySetResult();
            await release.Task;
            return "Private current body";
        }, CancellationToken.None);
        try {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            if (retireOpening) {
                current = false;
            } else {
                ui.Allowed = false;
            }
            release.TrySetResult();
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => pending);
            Assert.False(access.IsActive);
        } finally {
            release.TrySetResult();
            if (!pending.IsCompleted) {
                try {
                    await pending.WaitAsync(TimeSpan.FromSeconds(5));
                } catch (UnauthorizedAccessException) {
                }
            }
        }
    }

    [Fact]
    public async Task Native_Gantt_creation_publishes_its_committed_Task_to_the_displayed_parent_surface() {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var form = await OpenAsync(harness, await CreateProjectAsync(harness), gantt: true);
        try {
            await form.EnterTitleAsync();
            await form.SaveAsync();
            await form.Opening.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Single((await form.Workbench.GetStructureAsync(form.Project)).Nodes, node => node.Title == DraftTitle);
            form.Page.WaitForAssertion(() => Assert.Single(
                form.Page.FindComponent<ProjectStructureGanttPanel>().Instance.Surface.Nodes, node => node.Title == DraftTitle));
            form.Page.WaitForAssertion(() => Assert.Contains(DraftTitle, form.Page.Markup));
        } finally {
            await form.CloseAsync();
        }
    }

    [Fact]
    public async Task Gantt_creation_retains_the_same_raw_controls_across_same_Project_refresh_and_can_save() {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var project = await CreateProjectAsync(harness);
        var form = await OpenAsync(harness, project, gantt: true);
        try {
            await form.EnterTitleAsync();
            var estimate = form.Host.FindComponent<ProjectStructureTaskEstimateEditor>().Instance;
            await form.Host.InvokeAsync(() => form.Host.Find("[data-testid='project-structure-gantt-task-estimate-effort']").InputAsync(new() { Value = "3e" }));
            var panel = form.Page.FindComponent<ProjectStructureGanttPanel>();
            var refreshed = await form.Workbench.GetStructureAsync(project);
            await form.Page.InvokeAsync(() => panel.Render(parameters => parameters.Add(component => component.Surface, refreshed)));
            Assert.Same(estimate, form.Host.FindComponent<ProjectStructureTaskEstimateEditor>().Instance);
            Assert.Equal("3e", form.Host.Find("[data-testid='project-structure-gantt-task-estimate-effort']").GetAttribute("value"));
            await form.Host.InvokeAsync(() => form.Host.Find("[data-testid='project-structure-gantt-task-estimate-effort']").InputAsync(new() { Value = "8" }));
            await form.SaveAsync();
            await form.Opening.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Single((await form.Workbench.GetStructureAsync(project)).Nodes, node => node.Title == DraftTitle);
        } finally {
            await form.CloseAsync();
        }
    }

    [Fact]
    public async Task Access_revoked_during_actual_Project_readback_preserves_the_committed_Task_and_withholds_the_fresh_private_graph() {
        var access = new EditingUiTestAccess();
        var gate = new ReadbackGate();
        await using var harness = await ComponentTestHarness.CreateAsync(services => {
            services.AddSingleton<IEditingUiAccess>(new ReadbackUiAccess(access, gate));
            services.ConfigureDbContext<WorkbenchDbContext>(options => options.AddInterceptors(
                new ArmReadbackAfterRowCommit(gate), new DenyReadbackBody(gate, access)), ServiceLifetime.Singleton);
        });
        var project = await CreateProjectAsync(harness);
        gate.Project = project;
        var workbench = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();
        var admission = (await workbench.GetStructureAsync(project)).ExpectedProjectAdmission!;
        var note = await workbench.CreateObjectAsync(project, new(ProjectObjectType.Note, "Readback peer", "", "Original private notes",
            $"project:{project:D}", -2000, -2000) { ExpectedProjectAdmission = admission });
        var form = await OpenAsync(harness, project, gantt: true);
        try {
            await form.EnterTitleAsync();
            await using var peerScope = harness.Context.Services.CreateAsyncScope();
            var peer = peerScope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();
            Assert.NotNull(await peer.UpdateObjectAsync(project, note.Id,
                new(note.Title, note.Subtitle, "Fresh private notes from peer", note.StartUtc, note.EndUtc, note.MetadataJson,
                    note.DurationSeconds, note.NodeReferences) { ExpectedProjectAdmission = admission, ExpectedNode = note }));
            await form.SaveAsync();
            Assert.True(gate.Reached);
            var saved = await workbench.GetStructureAsync(project);
            Assert.Single(saved.Nodes, node => node.Title == DraftTitle);
            Assert.Equal("Fresh private notes from peer", Assert.Single(saved.Nodes, node => node.Id == note.Id).Notes);
            Assert.Equal("Original private notes", Assert.Single(form.Page.FindComponent<ProjectStructureGanttPanel>().Instance.Surface.Nodes,
                node => node.Id == note.Id).Notes);
            var result = form.Host.FindComponent<PlanningTaskFormActions>().Instance.Result!;
            Assert.Equal(PlanningTaskSaveStatus.Partial, result.Status);
            Assert.True(result.RequiresReadback);
            Assert.False(result.CanRetry);
            Assert.Contains(result.Phases, phase => phase.Phase == PlanningTaskPhase.Task && phase.State == PlanningTaskPhaseState.Committed);
            Assert.Equal(DraftTitle, form.Host.Find($"[data-testid='{form.Prefix}-title']").GetAttribute("value"));
        } finally {
            await form.CloseAsync();
        }
    }

    private static async Task<Guid> CreateProjectAsync(ComponentTestHarness harness) {
        var result = await harness.Context.Services.GetRequiredService<ProjectsService>().SaveAsync(new() { Name = "Task creation authority" });
        Assert.True(result.IsSuccess);
        return result.Value;
    }

    private static async Task<CreationOpening> OpenAsync(ComponentTestHarness harness, Guid project, bool gantt) {
        harness.Context.JSInterop.Setup<bool>("CanDoItAll.canvasWorkbench.create", _ => true).SetResult(true);
        var services = harness.Context.Services;
        var host = harness.Context.Render<DialogHost>();
        services.GetRequiredService<NavigationManager>().NavigateTo($"/projects/{project:D}/structure{(gantt ? "?tab=gantt" : string.Empty)}");
        var page = harness.Context.Render<ProjectStructurePage>(parameters => parameters.Add(component => component.ProjectId, project));
        Task opening;
        if (gantt) {
            page.WaitForAssertion(() => Assert.False(page.FindComponent<PlanningGanttSurface>().Instance.Presentation.IsLoading));
            var renderer = page.FindComponent<PlanningGanttSurface>().Instance;
            opening = page.InvokeAsync(() => renderer.AddRequested.InvokeAsync(new(renderer.Presentation.Origin)));
        } else {
            page.WaitForElement("[data-testid='project-structure-canvas-loaded']");
            var canvas = page.FindComponent<CanvasWorkbench>().Instance;
            opening = page.InvokeAsync(() => canvas.OnContextActionRequest(JsonSerializer.Serialize(new CanvasWorkbenchContextActionRequest(
                $"project:{project:D}", ProjectStructureTaskActionIds.Create, 100, 100, "node"))));
        }
        var prefix = gantt ? "project-structure-gantt-task" : "project-structure-task-create";
        host.WaitForElement($"[data-testid='{prefix}-title']");
        return new(project, services.GetRequiredService<ProjectWorkbenchService>(), services.GetRequiredService<DialogService>(), host, page, opening, prefix);
    }

    private sealed record CreationOpening(Guid Project, ProjectWorkbenchService Workbench, DialogService Dialogs,
        IRenderedComponent<DialogHost> Host, IRenderedComponent<ProjectStructurePage> Page, Task Opening, string Prefix) {
        public Task EnterTitleAsync() => Host.InvokeAsync(() => Host.Find($"[data-testid='{Prefix}-title']").InputAsync(new() { Value = DraftTitle }));
        public Task SaveAsync() => TaskDialogTestDriver.ClickAsync(Host, $"{Prefix}-submit");
        public Task CloseAsync() => TaskDialogTestDriver.CloseAsync(Host, Dialogs, Opening);
    }

    private sealed class RevokeAfterSql(EditingUiTestAccess access) : SaveChangesInterceptor {
        public CreationCommitStage Stage { get; set; }
        public Guid? Party { get; set; }
        public bool Armed { get; set; }
        public bool Reached { get; private set; }
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result,
            CancellationToken cancellationToken = default) {
            if (Armed && result > 0 && eventData.Context is WorkbenchDbContext context && (Stage switch {
                CreationCommitStage.Task => context.ChangeTracker.Entries<ProjectObjectRecord>().Any(entry => entry.Entity.Title == DraftTitle),
                CreationCommitStage.Backlog => context.ChangeTracker.Entries<ProjectObjectRecord>().Any(entry => entry.Entity.ObjectSubtype == "backlog"),
                CreationCommitStage.RowOrder => context.ChangeTracker.Entries<ProjectWorkbenchViewStateRecord>().Any(entry => entry.Entity.SurfaceKind == GanttSurfaceKind),
                CreationCommitStage.Assignment => context.ChangeTracker.Entries<ProjectWorkAssignmentRecord>().Any(entry => entry.Entity.PartyId == Party),
                _ => false
            })) {
                Assert.NotNull(context.Database.CurrentTransaction);
                Armed = false;
                Reached = true;
                access.Allowed = false;
            }
            return ValueTask.FromResult(result);
        }
    }

    private sealed class ReadbackGate {
        public Guid Project { get; set; }
        public bool Armed { get; set; }
        public bool ReadAuthorized { get; set; }
        public bool Reached { get; set; }
    }

    private sealed class ReadbackUiAccess(EditingUiTestAccess access, ReadbackGate gate) : IEditingUiAccess {
        public Task<EditingUiGrant> OpenAsync(EditingTopic topic, CancellationToken cancellationToken) => access.OpenAsync(topic, cancellationToken);

        public async ValueTask ValidateAsync(EditingUiGrant grant, bool write, CancellationToken cancellationToken) {
            await access.ValidateAsync(grant, write, cancellationToken);
            if (gate.Armed && !write) {
                gate.ReadAuthorized = true;
            }
        }
    }

    private sealed class ArmReadbackAfterRowCommit(ReadbackGate gate) : DbTransactionInterceptor {
        public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData,
            CancellationToken cancellationToken = default) {
            if (eventData.Context is WorkbenchDbContext context && context.ChangeTracker.Entries<ProjectWorkbenchViewStateRecord>()
                .Any(entry => entry.Entity.ProjectId == gate.Project && entry.Entity.SurfaceKind == GanttSurfaceKind)) {
                gate.Armed = true;
            }
            return Task.CompletedTask;
        }
    }

    private sealed class DenyReadbackBody(ReadbackGate gate, EditingUiTestAccess access) : DbCommandInterceptor {
        public override ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData, DbDataReader result,
            CancellationToken cancellationToken = default) {
            if (gate.Armed && gate.ReadAuthorized && command.CommandText.Contains("\"Workbench_ProjectObjects\"", StringComparison.Ordinal)) {
                gate.Armed = false;
                gate.Reached = true;
                access.Allowed = false;
            }
            return ValueTask.FromResult(result);
        }
    }
}
