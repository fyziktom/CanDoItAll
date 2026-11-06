using System.Data.Common;
using System.Text.Json;
using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Editing;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workbench.Pages;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using CanDoItAll.Workbench.Planning.UI;
using CanDoItAll.Workbench.Structure.UI;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.ProjectStructure;

[Trait("Category", "HostPlatform")]
public sealed class ProjectStructureReadAccessTests {
    private const string PrivateNotes = "Private Project body acquired after access retirement";
    private const string AssignmentTable = "\"CrmHr_ProjectPartyAssignments\"";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Native_initial_structure_read_withholds_the_body_when_access_is_denied_before_or_during_SQL(bool afterSql) {
        var access = new EditingUiTestAccess();
        var probe = new RevokeReadAfterSql(access, "\"Workbench_ProjectObjects\"");
        await using var harness = await CreateHarnessAsync(access, probe);
        var (project, _) = await CreateNoteProjectAsync(harness);
        access.Allowed = afterSql;
        probe.Armed = afterSql;
        var page = OpenPage(harness, project);
        page.WaitForState(() => page.FindComponent<StructureWorkspace>().Instance.Presentation is { Canvas: not null } or { Error: not null });
        Assert.Equal(afterSql, probe.Reached);
        Assert.Null(page.FindComponent<StructureWorkspace>().Instance.Presentation.Canvas);
        Assert.DoesNotContain(PrivateNotes, page.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Native_explicit_Gantt_reload_cannot_publish_a_fresh_private_Project_graph_after_revocation() {
        var access = new EditingUiTestAccess();
        await using var harness = await CreateHarnessAsync(access);
        var (project, note) = await CreateNoteProjectAsync(harness);
        var services = harness.Context.Services;
        var workbench = services.GetRequiredService<ProjectWorkbenchService>();
        var admission = (await workbench.GetStructureAsync(project)).ExpectedProjectAdmission!;
        var owner = Owner(admission);
        await services.GetRequiredService<ProjectStructureTaskCreationService>().CreateAsync(project,
            new("Readback task", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(8)) { ExpectedProjectAdmission = admission }, owner);
        var page = OpenPage(harness, project, gantt: true);
        page.WaitForAssertion(() => Assert.True(page.FindComponent<PlanningGanttSurface>().Instance.Presentation.CanMutate));
        await using var peerScope = services.CreateAsyncScope();
        Assert.NotNull(await peerScope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>().UpdateObjectAsync(project, note.Id,
            new(note.Title, note.Subtitle, PrivateNotes + " fresh peer version", note.StartUtc, note.EndUtc, note.MetadataJson,
                note.DurationSeconds, note.NodeReferences) { ExpectedProjectAdmission = admission, ExpectedNode = note }));
        var renderer = page.FindComponent<PlanningGanttSurface>().Instance;
        access.Allowed = false;
        await page.InvokeAsync(() => renderer.ReloadRequested.InvokeAsync(new(renderer.Presentation.Origin)));
        var visible = JsonSerializer.Serialize(page.FindComponent<StructureWorkspace>().Instance.Presentation);
        Assert.DoesNotContain(page.FindComponents<ProjectStructureGanttPanel>(), panel => panel.Instance.Surface?.Nodes
            .Any(node => node.Notes.Contains("fresh peer version", StringComparison.Ordinal)) == true);
        Assert.DoesNotContain("fresh peer version", visible, StringComparison.Ordinal);
        Assert.DoesNotContain("fresh peer version", page.Markup, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Native_Gantt_private_assignment_read_validates_access_before_publishing_the_acquired_projection(bool revoke) {
        var access = new EditingUiTestAccess();
        var probe = new RevokeReadAfterSql(access, AssignmentTable) { Revoke = revoke };
        await using var harness = await CreateHarnessAsync(access, probe);
        var services = harness.Context.Services;
        var (project, _) = await CreateNoteProjectAsync(harness);
        var workbench = services.GetRequiredService<ProjectWorkbenchService>();
        var admission = (await workbench.GetStructureAsync(project)).ExpectedProjectAdmission!;
        var person = await services.GetRequiredService<PartyDirectoryService>().SavePartyAsync(new PartyEditorModel {
            PartyType = PartyType.Person, LifecycleStatus = PartyLifecycleStatus.Active,
            DisplayName = "Private live Gantt assignee", LastChangedBy = "component-tests"
        });
        Assert.True(person.IsSuccess);
        var start = DateTimeOffset.UtcNow;
        await services.GetRequiredService<ProjectStructureTaskCreationService>().CreateAsync(project,
            new("Private assignment task", start, start.AddHours(8),
                Resource: new(ProjectStructureTaskResourceKind.Person, person.Value)) {
                    ExpectedProjectAdmission = admission
                }, Owner(admission));
        var surface = await workbench.GetStructureAsync(project);
        probe.Armed = true;
        var panel = harness.Context.Render<ProjectStructureGanttPanel>(parameters => parameters
            .Add(component => component.ProjectId, project).Add(component => component.Surface, surface));
        panel.WaitForState(() => !panel.FindComponent<PlanningGanttSurface>().Instance.Presentation.IsLoading);
        Assert.True(probe.Reached);
        var presentation = panel.FindComponent<PlanningGanttSurface>().Instance.Presentation;
        Assert.Equal(!revoke, presentation.HasProjection);
        if (!revoke) {
            Assert.Single(Assert.Single(presentation.Tasks).Assignments);
        } else {
            Assert.NotNull(presentation.LoadError);
            Assert.Empty(presentation.Tasks);
        }
    }

    private static Task<ComponentTestHarness> CreateHarnessAsync(EditingUiTestAccess access, RevokeReadAfterSql? probe = null)
        => ComponentTestHarness.CreateAsync(services => {
            services.AddSingleton<IEditingUiAccess>(access);
            if (probe is not null) {
                services.ConfigureDbContext<WorkbenchDbContext>(options => options.AddInterceptors(probe), ServiceLifetime.Singleton);
                services.ConfigureDbContext<CrmHrDbContext>(options => options.AddInterceptors(probe), ServiceLifetime.Singleton);
            }
        });

    [Fact]
    public async Task A_denied_root_refresh_retains_the_mounted_Gantt_editor_and_its_exact_invalid_raw_draft() {
        var access = new EditingUiTestAccess();
        await using var harness = await CreateHarnessAsync(access);
        var (project, _) = await CreateNoteProjectAsync(harness);
        var services = harness.Context.Services;
        var workbench = services.GetRequiredService<ProjectWorkbenchService>();
        var admission = (await workbench.GetStructureAsync(project)).ExpectedProjectAdmission!;
        var start = DateTimeOffset.UtcNow;
        var task = await services.GetRequiredService<ProjectStructureTaskCreationService>().CreateAsync(project,
            new("Retained private-read draft", start, start.AddHours(8)) { ExpectedProjectAdmission = admission }, Owner(admission));
        var host = harness.Context.Render<DialogHost>();
        var page = OpenPage(harness, project, gantt: true);
        page.WaitForAssertion(() => Assert.True(page.FindComponent<PlanningGanttSurface>().Instance.Presentation.CanMutate));
        var renderer = page.FindComponent<PlanningGanttSurface>().Instance;
        var opening = page.InvokeAsync(() => renderer.TaskOpened.InvokeAsync(new(renderer.Presentation.Origin, new(task.TaskNodeId))));
        try {
            host.WaitForElement("[data-testid='project-structure-gantt-task-title']");
            var mounted = host.FindComponent<ProjectStructureTaskEstimateEditor>().Instance;
            await host.InvokeAsync(() => host.Find("[data-testid='project-structure-gantt-task-estimate-effort']")
                .InputAsync(new() { Value = "3e" }));
            var workspace = page.FindComponent<StructureWorkspace>().Instance;
            access.Allowed = false;
            await page.InvokeAsync(() => workspace.Dispatch.InvokeAsync(new StructureToolbarIntent(workspace.Presentation.ContextId, StructureToolbarCommand.Retry)));
            Assert.False(opening.IsCompleted);
            Assert.Same(mounted, host.FindComponent<ProjectStructureTaskEstimateEditor>().Instance);
            Assert.Equal("3e", host.Find("[data-testid='project-structure-gantt-task-estimate-effort']").GetAttribute("value"));
            Assert.Null(page.FindComponent<StructureWorkspace>().Instance.Presentation.Error);
            Assert.Contains("new project data was not displayed", page.Markup, StringComparison.OrdinalIgnoreCase);
        } finally {
            await TaskDialogTestDriver.CloseAsync(host, services.GetRequiredService<DialogService>(), opening);
        }
    }

    private static async Task<(Guid Project, ProjectStructureNode Note)> CreateNoteProjectAsync(ComponentTestHarness harness) {
        var services = harness.Context.Services;
        var project = (await services.GetRequiredService<ProjectsService>().SaveAsync(new() { Name = "Private Structure read" })).Value;
        var workbench = services.GetRequiredService<ProjectWorkbenchService>();
        var admission = (await workbench.GetStructureAsync(project)).ExpectedProjectAdmission!;
        var note = await workbench.CreateObjectAsync(project, new(ProjectObjectType.Note, "Private note", "", PrivateNotes,
            $"project:{project:D}", 100, 100) { ExpectedProjectAdmission = admission });
        return (project, note);
    }

    private static IRenderedComponent<ProjectStructurePage> OpenPage(ComponentTestHarness harness, Guid project, bool gantt = false) {
        harness.Context.JSInterop.Setup<bool>("CanDoItAll.canvasWorkbench.create", _ => true).SetResult(true);
        harness.Context.Services.GetRequiredService<NavigationManager>().NavigateTo($"/projects/{project:D}/structure{(gantt ? "?tab=gantt" : "")}");
        return harness.Context.Render<ProjectStructurePage>(parameters => parameters.Add(component => component.ProjectId, project));
    }

    private static ProjectStructureAgentContext Owner(ProjectWriteAdmission admission)
        => new("component-tests", "Private read authority", Environment.MachineName, AppContext.BaseDirectory, "", "private-read-authority") {
            ExpectedProjectAdmission = admission
        };

    private sealed class RevokeReadAfterSql(EditingUiTestAccess access, string table) : DbCommandInterceptor {
        public bool Armed { get; set; }
        public bool Reached { get; private set; }
        public bool Revoke { get; set; } = true;

        public override ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData, DbDataReader result,
            CancellationToken cancellationToken = default) {
            if (Armed && command.CommandText.Contains(table, StringComparison.Ordinal)) {
                Armed = false;
                Reached = true;
                if (Revoke) {
                    access.Allowed = false;
                }
            }
            return ValueTask.FromResult(result);
        }
    }
}
