using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workbench.Pages;
using CanDoItAll.Workbench.Planning.UI;
using CanDoItAll.SharedKernel;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.ProjectStructure;

public sealed class ProjectTaskNativeDialogOutcomeTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Native_create_receipt_survives_failed_parent_refresh_without_duplicate_creation(bool general) {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var services = harness.Context.Services;
        var (projectId, owner) = await CreateProjectAsync(harness);
        var workbench = services.GetRequiredService<ProjectWorkbenchService>();
        var dialogHost = harness.Context.Render<DialogHost>();
        var refreshCalls = 0;
        Task Refresh() {
            refreshCalls++;
            throw new IOException("Injected parent refresh failure after the task committed.");
        }
        Task opening;
        if (general) {
            var context = new ProjectStructureCanvasTaskDialogContext(projectId, [],
                (draft, configure) => CreateNodeAsync(workbench, projectId, draft, configure), _ => Refresh(), owner);
            opening = dialogHost.InvokeAsync(() => services.GetRequiredService<ProjectStructureCanvasTaskDialogCoordinator>()
                .OpenCreateAsync(context, Request(projectId)));
        } else {
            var surface = await workbench.GetStructureAsync(projectId);
            var panel = harness.Context.Render<ProjectStructureGanttPanel>(parameters => parameters
                .Add(component => component.ProjectId, projectId).Add(component => component.Surface, surface)
                .Add(component => component.MutationCommitted, Refresh));
            panel.WaitForAssertion(() => Assert.False(panel.FindComponent<PlanningGanttSurface>().Instance.Presentation.IsLoading));
            var renderer = panel.FindComponent<PlanningGanttSurface>().Instance;
            opening = panel.InvokeAsync(() => renderer.AddRequested.InvokeAsync(new(renderer.Presentation.Origin)));
        }
        var prefix = general ? "project-structure-task-create" : "project-structure-gantt-task";
        dialogHost.WaitForElement($"[data-testid='{prefix}-title']").Input("Native receipt task");
        await dialogHost.Find($"[data-testid='{prefix}-submit']").ClickAsync(new MouseEventArgs());
        var receipt = dialogHost.FindComponent<PlanningTaskFormActions>().Instance.Result;
        Assert.NotNull(receipt);
        Assert.True(receipt.RequiresReadback);
        Assert.False(receipt.CanRetry);
        Assert.False(opening.IsCompleted);
        var actual = Assert.Single((await workbench.GetStructureAsync(projectId)).Nodes, node => node.Title == "Native receipt task");
        Assert.Equal(actual.Id, Assert.Single(receipt.TaskIds));
        Assert.Contains(receipt.Phases, phase => phase.Phase == PlanningTaskPhase.Task && phase.State == PlanningTaskPhaseState.Committed);
        var submit = dialogHost.FindComponent<PlanningTaskFormActions>().Instance.Submitted;
        await dialogHost.InvokeAsync(() => submit.InvokeAsync());
        await dialogHost.Find("[data-testid='planning-task-readback']").ClickAsync(new MouseEventArgs());
        Assert.Equal(2, refreshCalls);
        Assert.Single((await workbench.GetStructureAsync(projectId)).Nodes, node => node.Title == "Native receipt task");
        await dialogHost.Find($"[data-testid='{prefix}-cancel']").ClickAsync(new MouseEventArgs());
        await opening.WaitAsync(TimeSpan.FromSeconds(10));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task General_native_creation_preserves_known_followup_receipt_and_unknown_acknowledgement(bool knownReceipt) {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var services = harness.Context.Services;
        var (projectId, owner) = await CreateProjectAsync(harness);
        var workbench = services.GetRequiredService<ProjectWorkbenchService>();
        var host = harness.Context.Render<DialogHost>();
        var creates = 0;
        var context = new ProjectStructureCanvasTaskDialogContext(projectId, [], async (draft, configure) => {
            creates++;
            var created = await CreateNodeAsync(workbench, projectId, draft, configure);
            var failure = new IOException("Injected acknowledgement or canvas-follow-up failure.");
            if (knownReceipt) {
                throw new ProjectStructureNodeCreatedWithFollowUpFailureException(created!, failure);
            }
            throw failure;
        }, async _ => { await workbench.GetStructureAsync(projectId); }, owner);
        var opening = host.InvokeAsync(() => services.GetRequiredService<ProjectStructureCanvasTaskDialogCoordinator>()
            .OpenCreateAsync(context, Request(projectId)));
        host.WaitForElement("[data-testid='project-structure-task-create-title']").Input("Native interrupted create");
        await host.Find("[data-testid='project-structure-task-create-submit']").ClickAsync(new MouseEventArgs());
        var receipt = host.FindComponent<PlanningTaskFormActions>().Instance.Result!;
        Assert.Equal(knownReceipt ? PlanningTaskSaveStatus.Partial : PlanningTaskSaveStatus.Unknown, receipt.Status);
        var actual = Assert.Single((await workbench.GetStructureAsync(projectId)).Nodes, node => node.Title == "Native interrupted create");
        Assert.Equal(knownReceipt ? new[] { actual.Id } : [], receipt.TaskIds);
        await host.Find("[data-testid='planning-task-readback']").ClickAsync(new MouseEventArgs());
        await host.InvokeAsync(() => host.FindComponent<PlanningTaskFormActions>().Instance.Submitted.InvokeAsync());
        Assert.Equal(1, creates);
        Assert.False(host.FindComponent<PlanningTaskFormActions>().Instance.Result!.CanRetry);
        await host.Find("[data-testid='project-structure-task-create-cancel']").ClickAsync(new MouseEventArgs());
        await opening.WaitAsync(TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task Retired_native_opening_cannot_create_in_original_or_replacement_project() {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var services = harness.Context.Services;
        var (projectId, owner) = await CreateProjectAsync(harness);
        var (replacementId, _) = await CreateProjectAsync(harness);
        var workbench = services.GetRequiredService<ProjectWorkbenchService>();
        var host = harness.Context.Render<DialogHost>();
        var current = true;
        var context = new ProjectStructureCanvasTaskDialogContext(projectId, [],
            (draft, configure) => CreateNodeAsync(workbench, projectId, draft, configure), _ => Task.CompletedTask, owner) {
            IsCurrent = () => current
        };
        var opening = host.InvokeAsync(() => services.GetRequiredService<ProjectStructureCanvasTaskDialogCoordinator>()
            .OpenCreateAsync(context, Request(projectId)));
        host.WaitForElement("[data-testid='project-structure-task-create-title']");
        current = false;
        await host.Find("[data-testid='project-structure-task-create-submit']").ClickAsync(new MouseEventArgs());
        Assert.False(opening.IsCompleted);
        Assert.Equal(PlanningTaskSaveStatus.Rejected, host.FindComponent<PlanningTaskFormActions>().Instance.Result!.Status);
        foreach (var id in new[] { projectId, replacementId }) {
            Assert.DoesNotContain((await workbench.GetStructureAsync(id)).Nodes, node => node.ObjectSubtype == "task");
        }
        await host.Find("[data-testid='project-structure-task-create-cancel']").ClickAsync(new MouseEventArgs());
        await opening.WaitAsync(TimeSpan.FromSeconds(10));
    }

    private static async Task<(Guid, ProjectStructureAgentContext)> CreateProjectAsync(ComponentTestHarness harness) {
        var services = harness.Context.Services;
        var result = await services.GetRequiredService<ProjectsService>().SaveAsync(new ProjectEditorModel { Name = $"Native task outcomes {Guid.NewGuid():N}" });
        Assert.True(result.IsSuccess);
        var admission = Assert.IsType<ProjectWriteAdmission>(await services.GetRequiredService<ProjectWriteAdmissionService>().CaptureAsync(result.Value));
        return (result.Value, new("native-task-outcome-test", "Component test", Environment.MachineName,
            string.Empty, string.Empty, "native-task-outcome-test") { ExpectedProjectAdmission = admission });
    }
    private static CanvasWorkbenchCreateActionRequest Request(Guid projectId) => new(ProjectStructureTaskActionIds.Create,
        $"project:{projectId}", 100, 100, $"project:{projectId}", "Native task", "Delivery", "Preserved notes", "child",
        ProjectStructureTaskActionIds.CreateMode, "task", null, []);
    private static async Task<ProjectStructureNode?> CreateNodeAsync(ProjectWorkbenchService owner, Guid projectId,
        CanvasWorkbenchCreateActionRequest draft, Func<ProjectObjectCreateRequest, ProjectObjectCreateRequest> configure)
        => await owner.CreateObjectAsync(projectId, configure(new(ProjectObjectType.WorkItem, draft.Title!, draft.Subtitle!,
            draft.Notes!, $"project:{projectId}", 100, 100, ObjectSubtype: "task")));
}
