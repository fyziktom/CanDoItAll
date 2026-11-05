using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Components.Gantt;
using CanDoItAll.Editing;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workbench.Pages;
using CanDoItAll.Tests.Support;
using CanDoItAll.Workbench.Planning.UI;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace CanDoItAll.Tests.Components.ProjectStructure;

[Trait("Category", "HostPlatform")]
public sealed class ProjectStructureTaskCommitAccessTests {
    public enum CommitRevocation { None, Assignment, Task }
    private const string AssignmentTitle = "Task with own assignment";

    [Theory]
    [InlineData(CommitRevocation.None)]
    [InlineData(CommitRevocation.Assignment)]
    [InlineData(CommitRevocation.Task)]
    public async Task Native_Canvas_assignment_commit_preserves_unknown_metadata_and_obeys_the_after_SQL_grant(CommitRevocation revocation) {
        var access = new EditingUiTestAccess();
        var probe = new RevokeAssignmentAfterSql(access);
        await using var harness = await ComponentTestHarness.CreateAsync(services => {
            services.AddSingleton<IEditingUiAccess>(access);
            services.ConfigureDbContext<WorkbenchDbContext>(options => options.AddInterceptors(probe), ServiceLifetime.Singleton);
        });
        harness.Context.JSInterop.Setup<bool>("CanDoItAll.canvasWorkbench.create", _ => true).SetResult(true);
        var services = harness.Context.Services;
        var workbench = services.GetRequiredService<ProjectWorkbenchService>();
        var project = (await services.GetRequiredService<ProjectsService>().SaveAsync(new() { Name = "Task assignment commit authority" })).Value;
        var admission = (await workbench.GetStructureAsync(project)).ExpectedProjectAdmission!;
        var owner = new ProjectStructureAgentContext("component-tests", "Task assignment authority", Environment.MachineName,
            AppContext.BaseDirectory, "", "task-assignment-commit-authority") { ExpectedProjectAdmission = admission };
        var person = await services.GetRequiredService<PartyDirectoryService>().SavePartyAsync(new PartyEditorModel {
            PartyType = PartyType.Person, LifecycleStatus = PartyLifecycleStatus.Active, DisplayName = "Native Task assignee", LastChangedBy = "component-tests"
        });
        Assert.True(person.IsSuccess);
        var start = new DateTimeOffset(2026, 11, 2, 12, 0, 0, TimeSpan.Zero);
        var created = await services.GetRequiredService<ProjectStructureTaskCreationService>().CreateAsync(project,
            new("Original Task", start, start.AddHours(8), Estimate: new(8, ProjectWorkItemEffortUnit.Hours, 123, "USD")) {
                ExpectedProjectAdmission = admission
            }, owner);
        var original = Assert.Single((await workbench.GetStructureAsync(project)).Nodes, node => node.Id == created.TaskNodeId);
        var metadata = JsonNode.Parse(original.MetadataJson)!.AsObject();
        metadata["futureTaskConfig"] = new JsonObject { ["keep"] = true };
        metadata["workItem"]!.AsObject()["futureEffortPolicy"] = new JsonObject { ["keep"] = true };
        original = (await workbench.UpdateObjectAsync(project, original.Id,
            new(original.Title, original.Subtitle, original.Notes, original.StartUtc, original.EndUtc, metadata.ToJsonString(),
                original.DurationSeconds, original.NodeReferences) { ExpectedProjectAdmission = admission, ExpectedNode = original }))!;
        services.GetRequiredService<NavigationManager>().NavigateTo($"/projects/{project:D}/structure");
        var dialogs = services.GetRequiredService<DialogService>();
        var host = harness.Context.Render<DialogHost>();
        var page = harness.Context.Render<ProjectStructurePage>(parameters => parameters.Add(component => component.ProjectId, project));
        page.WaitForElement("[data-testid='project-structure-canvas-loaded']");
        var opening = page.InvokeAsync(() => page.FindComponent<CanvasWorkbench>().Instance.OnContextAction(original.Id, "edit", 0, 0));
        try {
            host.WaitForElement("[data-testid='project-structure-task-create-title']");
            await host.InvokeAsync(() => host.Find("[data-testid='project-structure-task-create-title']").InputAsync(new() { Value = AssignmentTitle }));
            await TaskDialogTestDriver.ClickAsync(host, $"project-structure-task-create-assignee-person-{person.Value:N}");
            probe.Party = person.Value;
            probe.Stage = revocation;
            probe.Armed = revocation != CommitRevocation.None;
            await TaskDialogTestDriver.ClickAsync(host, "project-structure-task-create-submit");
            var saved = Assert.Single((await workbench.GetStructureAsync(project)).Nodes, node => node.Id == original.Id);
            using var savedJson = JsonDocument.Parse(saved.MetadataJson);
            Assert.True(savedJson.RootElement.GetProperty("futureTaskConfig").GetProperty("keep").GetBoolean());
            Assert.True(savedJson.RootElement.GetProperty("workItem").GetProperty("futureEffortPolicy").GetProperty("keep").GetBoolean());
            var assignments = (await services.GetRequiredService<ProjectStructureWorkItemAssigneeService>().ReadAsync(project, original.Id,
                expectedProjectAdmission: admission)).DirectAssignments;
            if (revocation != CommitRevocation.None) {
                Assert.True(probe.Reached);
                Assert.Equal(original.Title, saved.Title);
                Assert.Empty(assignments);
                if (revocation == CommitRevocation.Assignment) {
                    Assert.Equal(original.MetadataJson, saved.MetadataJson);
                } else {
                    Assert.Equal(2, ProjectObjectMetadataSerializer.Parse(saved.MetadataJson).WorkItem!.DirectAssignmentRevision);
                    Assert.Equal(ProjectStructureTaskEditStatePolicy.Read(original).Estimate, ProjectStructureTaskEditStatePolicy.Read(saved).Estimate);
                }
                Assert.Equal(revocation == CommitRevocation.Assignment ? PlanningTaskSaveStatus.Rejected : PlanningTaskSaveStatus.Unknown,
                    host.FindComponent<PlanningStructureTaskEditor>().Instance.ReadCloseRequest().Outcome!.Status);
                Assert.Equal(AssignmentTitle, host.Find("[data-testid='project-structure-task-create-title']").GetAttribute("value"));
            } else {
                await opening.WaitAsync(TimeSpan.FromSeconds(5));
                Assert.Equal(AssignmentTitle, saved.Title);
                Assert.Equal(person.Value, Assert.Single(assignments).PartyId);
                Assert.Equal(1, ProjectObjectMetadataSerializer.Parse(saved.MetadataJson).WorkItem!.DirectAssignmentRevision);
            }
        } finally {
            await TaskDialogTestDriver.CloseAsync(host, dialogs, opening);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Native_Task_editor_rolls_back_when_Structure_access_is_revoked_after_SQL(bool gantt) {
        var access = new EditingUiTestAccess();
        var probe = new RevokeAfterSql(access);
        await using var harness = await ComponentTestHarness.CreateAsync(services => {
            services.AddSingleton<IEditingUiAccess>(access);
            services.ConfigureDbContext<WorkbenchDbContext>(options => options.AddInterceptors(probe), ServiceLifetime.Singleton);
        });
        harness.Context.JSInterop.Setup<bool>("CanDoItAll.canvasWorkbench.create", _ => true).SetResult(true);
        var services = harness.Context.Services;
        var workbench = services.GetRequiredService<ProjectWorkbenchService>();
        var project = (await services.GetRequiredService<ProjectsService>().SaveAsync(new() { Name = "Native Task commit authority" })).Value;
        var admission = (await workbench.GetStructureAsync(project)).ExpectedProjectAdmission!;
        var owner = new ProjectStructureAgentContext("component-tests", "Native Task authority", Environment.MachineName,
            AppContext.BaseDirectory, "", "native-task-commit-authority") { ExpectedProjectAdmission = admission };
        var start = new DateTimeOffset(2026, 11, 2, 12, 0, 0, TimeSpan.Zero);
        var created = await services.GetRequiredService<ProjectStructureTaskCreationService>().CreateAsync(project,
            new("Original Task", start, start.AddHours(8), Estimate: new(8, ProjectWorkItemEffortUnit.Hours, null, "")) {
                ExpectedProjectAdmission = admission
            }, owner);
        var surface = await workbench.GetStructureAsync(project);
        var original = Assert.Single(surface.Nodes, node => node.Id == created.TaskNodeId);
        var dialogs = services.GetRequiredService<DialogService>();
        var host = harness.Context.Render<DialogHost>();
        Task opening;
        if (gantt) {
            IRenderedComponent<ProjectStructureGanttPanel> panel = null!;
            panel = harness.Context.Render<ProjectStructureGanttPanel>(parameters => parameters.Add(component => component.ProjectId, project)
                .Add(component => component.Surface, surface)
                .Add(component => component.ReadAuthoritativeProject, async authorize => {
                    await authorize(CancellationToken.None);
                    var refreshed = await workbench.GetStructureAsync(project);
                    await authorize(CancellationToken.None);
                    panel.Render(parameters => parameters.Add(component => component.Surface, refreshed));
                }));
            panel.WaitForAssertion(() => Assert.Single(panel.FindComponent<GanttChart>().Instance.Tasks));
            opening = panel.InvokeAsync(() => panel.FindComponent<GanttChart>().Instance.TaskDoubleClicked.InvokeAsync(new GanttTaskId(original.Id)));
        } else {
            services.GetRequiredService<NavigationManager>().NavigateTo($"/projects/{project:D}/structure");
            var page = harness.Context.Render<ProjectStructurePage>(parameters => parameters.Add(component => component.ProjectId, project));
            page.WaitForElement("[data-testid='project-structure-canvas-loaded']");
            opening = page.InvokeAsync(() => page.FindComponent<CanvasWorkbench>().Instance.OnContextAction(original.Id, "edit", 0, 0));
        }
        try {
            var prefix = gantt ? "project-structure-gantt-task" : "project-structure-task-create";
            host.WaitForElement($"[data-testid='{prefix}-title']");
            await host.InvokeAsync(() => host.Find($"[data-testid='{prefix}-title']").InputAsync(new() { Value = RevokeAfterSql.Target }));
            probe.Armed = true;
            await TaskDialogTestDriver.ClickAsync(host, $"{prefix}-submit");
            Assert.True(probe.Reached);
            var saved = Assert.Single((await workbench.GetStructureAsync(project)).Nodes, node => node.Id == original.Id);
            Assert.Equal(original.Title, saved.Title);
            Assert.Equal(original.MetadataJson, saved.MetadataJson);
            Assert.Equal(original.StartUtc, saved.StartUtc);
            Assert.Equal(original.EndUtc, saved.EndUtc);
            var result = gantt ? host.FindComponent<PlanningGanttTaskEditor>().Instance.ReadCloseRequest() :
                host.FindComponent<PlanningStructureTaskEditor>().Instance.ReadCloseRequest();
            Assert.Equal(PlanningTaskSaveStatus.Rejected, result.Outcome!.Status);
            Assert.Equal(RevokeAfterSql.Target, host.Find($"[data-testid='{prefix}-title']").GetAttribute("value"));
            Assert.True(host.Find($"[data-testid='{prefix}-submit']").HasAttribute("disabled"));
        } finally {
            await TaskDialogTestDriver.CloseAsync(host, dialogs, opening);
        }
    }

    private sealed class RevokeAfterSql(EditingUiTestAccess access) : SaveChangesInterceptor {
        public const string Target = "Task access revoked after SQL";
        public bool Armed { get; set; }
        public bool Reached { get; private set; }
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result,
            CancellationToken cancellationToken = default) {
            if (Armed && result > 0 && eventData.Context is WorkbenchDbContext context &&
                context.ChangeTracker.Entries<ProjectObjectRecord>().Any(entry => entry.Entity.Title == Target)) {
                Assert.NotNull(context.Database.CurrentTransaction);
                Reached = true;
                Armed = false;
                access.Allowed = false;
            }
            return ValueTask.FromResult(result);
        }
    }

    private sealed class RevokeAssignmentAfterSql(EditingUiTestAccess access) : SaveChangesInterceptor {
        public Guid Party { get; set; }
        public CommitRevocation Stage { get; set; }
        public bool Armed { get; set; }
        public bool Reached { get; private set; }
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result,
            CancellationToken cancellationToken = default) {
            if (Armed && result > 0 && eventData.Context is WorkbenchDbContext context &&
                (Stage == CommitRevocation.Assignment && context.ChangeTracker.Entries<ProjectWorkAssignmentRecord>().Any(entry => entry.Entity.PartyId == Party) ||
                 Stage == CommitRevocation.Task && context.ChangeTracker.Entries<ProjectObjectRecord>().Any(entry => entry.Entity.Title == AssignmentTitle))) {
                Assert.NotNull(context.Database.CurrentTransaction);
                Reached = true;
                Armed = false;
                access.Allowed = false;
            }
            return ValueTask.FromResult(result);
        }
    }
}
