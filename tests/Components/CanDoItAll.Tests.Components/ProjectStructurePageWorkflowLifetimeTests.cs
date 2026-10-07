using Bunit;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workbench.Pages;
using CanDoItAll.Modules.Workbench.Pages.Components.ProjectStructure;
using CanDoItAll.SharedKernel;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.ProjectStructure;

public sealed partial class ProjectStructurePageWorkflowNodeTests {
    [Theory]
    [InlineData(WorkflowReplacement.OtherTarget, false)]
    [InlineData(WorkflowReplacement.OtherTarget, true)]
    [InlineData(WorkflowReplacement.SameTarget, false)]
    [InlineData(WorkflowReplacement.SameTarget, true)]
    [InlineData(WorkflowReplacement.ProjectRoundTrip, false)]
    [InlineData(WorkflowReplacement.ProjectRoundTrip, true)]
    public async Task Late_workflow_options_leave_the_successor_opening_untouched(WorkflowReplacement replacement, bool fail) {
        var gate = new WorkflowReadGate();
        await using var harness = await CreateHeldWorkflowHarnessAsync(gate);
        var services = harness.Context.Services;
        var projects = services.GetRequiredService<ProjectsService>();
        var projectId = await CreateProjectAsync(projects, "Workflow opening lifetime");
        var otherProject = await CreateProjectAsync(projects, "Other workflow project");
        var workbench = services.GetRequiredService<ProjectWorkbenchService>();
        var neighbor = await workbench.CreateObjectAsync(projectId, new(ProjectObjectType.Note, "Successor parent", "", "", $"project:{projectId}"));
        var cut = harness.Context.Render<ProjectStructurePage>(p => p.Add(page => page.ProjectId, projectId));
        WaitForCanvasWorkbench(cut);
        gate.Armed = true;
        gate.Failure = fail ? new ArgumentException("Original options were rejected.") : null;
        var pending = OpenWorkflowAsync(cut, $"project:{projectId}");
        try {
            await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(20));
            if (replacement == WorkflowReplacement.ProjectRoundTrip) {
                await cut.InvokeAsync(() => cut.Render(p => p.Add(page => page.ProjectId, otherProject)));
                WaitForCanvasWorkbench(cut);
                await cut.InvokeAsync(() => cut.Render(p => p.Add(page => page.ProjectId, projectId)));
                WaitForCanvasWorkbench(cut);
            }
            await OpenWorkflowAsync(cut, replacement == WorkflowReplacement.OtherTarget ? neighbor.Id : $"project:{projectId}");
            await cut.Find("[data-testid='project-structure-workflow-add-manual-json']").InputAsync("{\"successor\":true}");
            var successor = WorkflowDialogs(cut).WorkflowAddDialog;
            var selection = WaitForCanvasWorkbench(cut).Instance.Surface;
            gate.Release.TrySetResult();
            await pending;
            Assert.Same(successor, WorkflowDialogs(cut).WorkflowAddDialog);
            Assert.Same(selection, WaitForCanvasWorkbench(cut).Instance.Surface);
        } finally {
            gate.Release.TrySetResult();
            await pending;
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Late_workflow_submission_cannot_close_or_restore_over_a_successor(bool fail) {
        var gate = new WorkflowReadGate();
        await using var harness = await CreateHeldWorkflowHarnessAsync(gate);
        var services = harness.Context.Services;
        var projectId = await CreateProjectAsync(services.GetRequiredService<ProjectsService>(), "Workflow submission lifetime");
        var workflow = await CreateWorkflowAsync(services.GetRequiredService<IWorkflowCatalogService>(), "Lifetime workflow");
        var cut = harness.Context.Render<ProjectStructurePage>(p => p.Add(page => page.ProjectId, projectId));
        await OpenWorkflowAsync(cut, $"project:{projectId}");
        await cut.WaitForElement($"[data-testid='project-structure-workflow-add-option-{workflow.Id.Value:N}']").ClickAsync(new MouseEventArgs());
        gate.Armed = true;
        gate.Failure = fail ? new ArgumentException("Original submission was rejected before writing.") : null;
        var pending = cut.Find("[data-testid='project-structure-workflow-add-submit']").ClickAsync(new MouseEventArgs());
        try {
            await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(20));
            await cut.InvokeAsync(() => WorkflowDialogs(cut).CloseWorkflowAdd.InvokeAsync());
            await OpenWorkflowAsync(cut, $"project:{projectId}");
            await cut.Find("[data-testid='project-structure-workflow-add-manual-json']").InputAsync("{\"successor\":true}");
            var successor = WorkflowDialogs(cut).WorkflowAddDialog;
            var selection = WaitForCanvasWorkbench(cut).Instance.Surface;
            gate.Release.TrySetResult();
            await pending;
            Assert.Same(successor, WorkflowDialogs(cut).WorkflowAddDialog);
            Assert.Same(selection, WaitForCanvasWorkbench(cut).Instance.Surface);
            var persisted = await services.GetRequiredService<ProjectWorkbenchService>().GetStructureAsync(projectId);
            Assert.Equal(fail ? 0 : 1, persisted.Nodes.Count(node => node.ObjectType == ProjectObjectType.WorkflowDefinition));
        } finally {
            gate.Release.TrySetResult();
            await pending;
        }
    }

    [Fact]
    public async Task Duplicate_workflow_submit_dispatch_admits_one_native_write() {
        var gate = new WorkflowReadGate();
        await using var harness = await CreateHeldWorkflowHarnessAsync(gate);
        var services = harness.Context.Services;
        var projectId = await CreateProjectAsync(services.GetRequiredService<ProjectsService>(), "Workflow duplicate dispatch");
        var workflow = await CreateWorkflowAsync(services.GetRequiredService<IWorkflowCatalogService>(), "Single workflow");
        var cut = harness.Context.Render<ProjectStructurePage>(p => p.Add(page => page.ProjectId, projectId));
        await OpenWorkflowAsync(cut, $"project:{projectId}");
        await cut.WaitForElement($"[data-testid='project-structure-workflow-add-option-{workflow.Id.Value:N}']").ClickAsync(new MouseEventArgs());
        var submit = WorkflowDialogs(cut).ExecuteWorkflowAdd;
        gate.Armed = true;
        var pending = cut.InvokeAsync(() => submit.InvokeAsync());
        try {
            await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(20));
            var duplicate = cut.InvokeAsync(() => submit.InvokeAsync());
            gate.Release.TrySetResult();
            await Task.WhenAll(pending, duplicate);
            var persisted = await services.GetRequiredService<ProjectWorkbenchService>().GetStructureAsync(projectId);
            Assert.Single(persisted.Nodes, node => node.ObjectType == ProjectObjectType.WorkflowDefinition);
        } finally {
            gate.Release.TrySetResult();
            await pending;
        }
    }

    [Fact]
    public async Task Workflow_preview_keeps_invalid_raw_json_correctable() {
        await using var harness = await CreateHarnessAsync();
        var projectId = await CreateProjectAsync(harness.Context.Services.GetRequiredService<ProjectsService>(), "Workflow raw input");
        var cut = harness.Context.Render<ProjectStructurePage>(p => p.Add(page => page.ProjectId, projectId));
        await OpenWorkflowAsync(cut, $"project:{projectId}");
        const string raw = "{\"unfinished\":";
        await cut.Find("[data-testid='project-structure-workflow-add-manual-json']").InputAsync(raw);
        Assert.Equal(raw, WorkflowDialogs(cut).WorkflowAddDialog!.InputSettings.ManualInputJson);
        Assert.Equal(raw, cut.Find("[data-testid='project-structure-workflow-add-manual-json']").GetAttribute("value"));
    }

    private static Task<ComponentTestHarness> CreateHeldWorkflowHarnessAsync(WorkflowReadGate gate)
        => CreateHarnessAsync(services => services.AddSingleton<IDbContextFactory<WorkbenchDbContext>>(provider =>
            new HeldWorkbenchFactory(new PooledDbContextFactory<WorkbenchDbContext>(provider.GetRequiredService<DbContextOptions<WorkbenchDbContext>>()), gate)));

    private static Task OpenWorkflowAsync(IRenderedComponent<ProjectStructurePage> cut, string parent) {
        var canvas = WaitForCanvasWorkbench(cut);
        return cut.InvokeAsync(() => canvas.Instance.OnContextAction(parent, "add-workflow", 0, 0));
    }

    private static ProjectStructureCanvasDialogs WorkflowDialogs(IRenderedComponent<ProjectStructurePage> cut)
        => cut.FindComponent<ProjectStructureCanvasDialogs>().Instance;

    public enum WorkflowReplacement { OtherTarget, SameTarget, ProjectRoundTrip }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Original_start_options_or_submission_cannot_repaint_a_new_opening(bool submit, bool fail) {
        var gate = new WorkflowReadGate();
        await using var harness = await CreateHeldWorkflowHarnessAsync(gate);
        var (cut, node) = await CreateStartWorkflowAsync(harness);
        if (submit) {
            await OpenStartAsync(cut, node);
        }
        gate.Armed = true;
        gate.Failure = fail ? new ArgumentException("Original workflow start rejected.") : null;
        var pending = submit
            ? cut.Find("[data-testid='project-structure-workflow-start-submit']").ClickAsync(new MouseEventArgs())
            : OpenStartAsync(cut, node);
        try {
            await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(20));
            await OpenStartAsync(cut, node);
            var successor = WorkflowDialogs(cut).WorkflowStartDialog;
            gate.Release.TrySetResult();
            await pending;
            Assert.Same(successor, WorkflowDialogs(cut).WorkflowStartDialog);
            if (submit && !fail) {
                var result = Assert.Single(cut.Instance.AuthoringOutcomes, item => item.Operation == ProjectStructureAuthoringOperation.StartWorkflow);
                Assert.NotNull(result.WorkflowStart);
                Assert.NotEqual(successor!.IntentId, result.WorkflowStart.IntentId);
                Assert.Equal(node.Id, result.WorkflowStart.NodeId);
            }
        } finally {
            gate.Release.TrySetResult();
            await pending;
        }
    }

    [Fact]
    public async Task Workflow_start_duplicate_dispatch_and_observation_keep_the_original_run() {
        var gate = new WorkflowReadGate();
        await using var harness = await CreateHeldWorkflowHarnessAsync(gate);
        var (cut, node) = await CreateStartWorkflowAsync(harness);
        await OpenStartAsync(cut, node);
        var intent = WorkflowDialogs(cut).WorkflowStartDialog!.IntentId;
        var submit = WorkflowDialogs(cut).ExecuteWorkflowStart;
        gate.Armed = true;
        var pending = cut.InvokeAsync(() => submit.InvokeAsync());
        try {
            await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(20));
            var duplicate = cut.InvokeAsync(() => submit.InvokeAsync());
            gate.Release.TrySetResult();
            await Task.WhenAll(pending, duplicate);
            var accepted = Assert.Single(cut.Instance.AuthoringOutcomes, item => item.Operation == ProjectStructureAuthoringOperation.StartWorkflow).WorkflowStart;
            Assert.NotNull(accepted);
            Assert.Equal(intent, accepted.IntentId);
            await cut.InvokeAsync(() => WorkflowDialogs(cut).ExecuteWorkflowStart.InvokeAsync());
            var receipts = cut.Instance.AuthoringOutcomes.Where(item => item.Operation == ProjectStructureAuthoringOperation.StartWorkflow).ToArray();
            Assert.Equal(2, receipts.Length);
            Assert.All(receipts, result => Assert.Equal(accepted.RunId, result.WorkflowStart!.RunId));
            Assert.All(receipts, result => Assert.Equal(accepted.WorkflowVersionId, result.WorkflowStart!.WorkflowVersionId));
            await cut.InvokeAsync(() => WorkflowDialogs(cut).CloseWorkflowStart.InvokeAsync());
            await OpenStartAsync(cut, node);
            Assert.NotEqual(intent, WorkflowDialogs(cut).WorkflowStartDialog!.IntentId);
        } finally {
            gate.Release.TrySetResult();
            await pending;
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Accepted_workflow_create_is_retained_when_surface_observation_fails(bool replace) {
        var gate = new WorkflowReadGate();
        await using var harness = await CreateHeldWorkflowHarnessAsync(gate);
        var services = harness.Context.Services;
        var projectId = await CreateProjectAsync(services.GetRequiredService<ProjectsService>(), "Workflow accepted observation");
        var workflow = await CreateWorkflowAsync(services.GetRequiredService<IWorkflowCatalogService>(), "Accepted workflow");
        var cut = harness.Context.Render<ProjectStructurePage>(p => p.Add(page => page.ProjectId, projectId));
        await OpenWorkflowAsync(cut, $"project:{projectId}");
        await cut.WaitForElement($"[data-testid='project-structure-workflow-add-option-{workflow.Id.Value:N}']").ClickAsync(new MouseEventArgs());
        gate.IsReady = () => cut.Instance.AuthoringOutcomes.Any(item => item.Operation == ProjectStructureAuthoringOperation.AddWorkflow &&
            item.Kind == ProjectStructureAuthoringResultKind.Committed);
        gate.Failure = new IOException("The observation transport failed after acceptance.");
        gate.Armed = true;
        var submit = WorkflowDialogs(cut).ExecuteWorkflowAdd;
        var pending = cut.InvokeAsync(() => submit.InvokeAsync());
        try {
            await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(20));
            var accepted = Assert.Single(cut.Instance.AuthoringOutcomes, item => item.Operation == ProjectStructureAuthoringOperation.AddWorkflow);
            var createdId = Assert.Single(accepted.NodeIds);
            if (replace) {
                await OpenWorkflowAsync(cut, $"project:{projectId}");
            }
            var successor = WorkflowDialogs(cut).WorkflowAddDialog;
            gate.Release.TrySetResult();
            await pending;
            if (replace) {
                Assert.Same(successor, WorkflowDialogs(cut).WorkflowAddDialog);
            } else {
                Assert.Null(WorkflowDialogs(cut).WorkflowAddDialog);
            }
            await cut.InvokeAsync(() => submit.InvokeAsync());
            var stored = await services.GetRequiredService<ProjectWorkbenchService>().GetStructureAsync(projectId);
            Assert.Equal(createdId, Assert.Single(stored.Nodes, n => n.ObjectType == ProjectObjectType.WorkflowDefinition).Id);
            Assert.All(cut.Instance.AuthoringOutcomes.Where(item => item.Operation == ProjectStructureAuthoringOperation.AddWorkflow),
                item => Assert.Equal(ProjectStructureAuthoringResultKind.Committed, item.Kind));
        } finally {
            gate.Release.TrySetResult();
            await pending;
        }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Workflow_retry_observes_unknown_effects_but_allows_known_rejections(bool start, bool unknown) {
        var gate = new WorkflowReadGate();
        await using var harness = await CreateHeldWorkflowHarnessAsync(gate);
        var (cut, node) = await CreateStartWorkflowAsync(harness);
        if (start) {
            await OpenStartAsync(cut, node);
        } else {
            await OpenWorkflowAsync(cut, $"project:{node.ProjectId}");
            var add = WorkflowDialogs(cut).WorkflowAddDialog;
            Assert.True(add is { Options.Count: > 0 }, add?.Error ?? $"No add opening for node project {node.ProjectId} on page {cut.Instance.ProjectId}.");
            var workflow = ProjectObjectMetadataSerializer.Parse(node.MetadataJson).Workflow!;
            await cut.Find($"[data-testid='project-structure-workflow-add-option-{workflow.WorkflowId!.Value.Value:N}']").ClickAsync(new MouseEventArgs());
        }
        var submit = start ? WorkflowDialogs(cut).ExecuteWorkflowStart : WorkflowDialogs(cut).ExecuteWorkflowAdd;
        gate.Failure = unknown ? new IOException("Transport ended without an effect receipt.") : new ArgumentException("Rejected before admission.");
        gate.Armed = true;
        var pending = cut.InvokeAsync(() => submit.InvokeAsync());
        try {
            await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(20));
        } finally {
            gate.Release.TrySetResult();
            await pending;
        }
        Assert.Equal(unknown, start ? WorkflowDialogs(cut).WorkflowStartDialog!.RequiresObservation : WorkflowDialogs(cut).WorkflowAddDialog!.RequiresObservation);
        await cut.InvokeAsync(() => submit.InvokeAsync());
        var stored = await harness.Context.Services.GetRequiredService<ProjectWorkbenchService>().GetStructureAsync(node.ProjectId);
        if (start) {
            var result = WorkflowDialogs(cut).WorkflowStartDialog!;
            Assert.Equal(unknown, result.AcceptedStart is null);
            Assert.True(result.RequiresObservation);
        } else {
            Assert.Equal(unknown ? 1 : 2, stored.Nodes.Count(item => item.ObjectType == ProjectObjectType.WorkflowDefinition));
        }
    }

    [Fact]
    public async Task Workflow_creation_rejects_a_replacement_project_with_the_same_public_id() {
        await using var harness = await CreateHarnessAsync();
        var services = harness.Context.Services;
        var projects = services.GetRequiredService<ProjectsService>();
        var projectId = await CreateProjectAsync(projects, "Original Workflow project");
        var workflow = await CreateWorkflowAsync(services.GetRequiredService<IWorkflowCatalogService>(), "Replacement guard");
        var cut = harness.Context.Render<ProjectStructurePage>(p => p.Add(page => page.ProjectId, projectId));
        await OpenWorkflowAsync(cut, $"project:{projectId}");
        await cut.Find($"[data-testid='project-structure-workflow-add-option-{workflow.Id.Value:N}']").ClickAsync(new MouseEventArgs());
        var original = WorkflowDialogs(cut).WorkflowAddDialog!;
        await projects.DeleteAsync(projectId, expectedProjectAdmission: original.OpenedSurface!.ExpectedProjectAdmission);
        Assert.True((await projects.CreateAsync(projectId, new() { Name = "Replacement project" })).IsSuccess);
        await cut.InvokeAsync(() => WorkflowDialogs(cut).ExecuteWorkflowAdd.InvokeAsync());
        var replacement = await services.GetRequiredService<ProjectWorkbenchService>().GetStructureAsync(projectId);
        Assert.NotEqual(original.OpenedSurface.ExpectedProjectAdmission, replacement.ExpectedProjectAdmission);
        Assert.DoesNotContain(replacement.Nodes, item => item.ObjectType == ProjectObjectType.WorkflowDefinition);
        Assert.DoesNotContain(cut.Instance.AuthoringOutcomes, item => item.Kind == ProjectStructureAuthoringResultKind.Committed);
    }

    [Fact]
    public async Task Independent_workflow_views_can_submit_without_sharing_opening_state() {
        var gate = new WorkflowReadGate();
        await using var harness = await CreateHeldWorkflowHarnessAsync(gate);
        var services = harness.Context.Services;
        var projects = services.GetRequiredService<ProjectsService>();
        var firstProject = await CreateProjectAsync(projects, "First Workflow view");
        var secondProject = await CreateProjectAsync(projects, "Second Workflow view");
        var workflow = await CreateWorkflowAsync(services.GetRequiredService<IWorkflowCatalogService>(), "Independent views");
        var first = harness.Context.Render<ProjectStructurePage>(p => p.Add(page => page.ProjectId, firstProject));
        var second = harness.Context.Render<ProjectStructurePage>(p => p.Add(page => page.ProjectId, secondProject));
        await OpenWorkflowAsync(first, $"project:{firstProject}");
        await OpenWorkflowAsync(second, $"project:{secondProject}");
        foreach (var page in new[] { first, second }) {
            await page.Find($"[data-testid='project-structure-workflow-add-option-{workflow.Id.Value:N}']").ClickAsync(new MouseEventArgs());
        }
        gate.Armed = true;
        var pending = first.InvokeAsync(() => WorkflowDialogs(first).ExecuteWorkflowAdd.InvokeAsync());
        try {
            await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(20));
            await second.InvokeAsync(() => WorkflowDialogs(second).ExecuteWorkflowAdd.InvokeAsync()).WaitAsync(TimeSpan.FromSeconds(20));
            var accepted = Assert.Single(second.Instance.AuthoringOutcomes, item => item.Operation == ProjectStructureAuthoringOperation.AddWorkflow);
            var secondSurface = WaitForCanvasWorkbench(second).Instance.Surface;
            gate.Release.TrySetResult();
            await pending;
            Assert.Equal(ProjectStructureAuthoringResultKind.Committed, accepted.Kind);
            Assert.Same(secondSurface, WaitForCanvasWorkbench(second).Instance.Surface);
            var stored = await services.GetRequiredService<ProjectWorkbenchService>().GetStructureAsync(firstProject);
            Assert.Single(stored.Nodes, item => item.ObjectType == ProjectObjectType.WorkflowDefinition);
        } finally {
            gate.Release.TrySetResult();
            await pending;
        }
    }

    private static async Task<(IRenderedComponent<ProjectStructurePage> Page, ProjectStructureNode Node)> CreateStartWorkflowAsync(ComponentTestHarness harness) {
        var services = harness.Context.Services;
        var projectId = await CreateProjectAsync(services.GetRequiredService<ProjectsService>(), "Workflow native start");
        var workflow = await CreateWorkflowAsync(services.GetRequiredService<IWorkflowCatalogService>(), "Start to end workflow");
        var cut = harness.Context.Render<ProjectStructurePage>(p => p.Add(page => page.ProjectId, projectId));
        await OpenWorkflowAsync(cut, $"project:{projectId}");
        var dialog = WorkflowDialogs(cut).WorkflowAddDialog;
        Assert.True(dialog is { Options.Count: > 0 }, dialog?.Error ?? "The native opening did not produce a current dialog.");
        await cut.WaitForElement($"[data-testid='project-structure-workflow-add-option-{workflow.Id.Value:N}']").ClickAsync(new MouseEventArgs());
        await cut.Find("[data-testid='project-structure-workflow-add-submit']").ClickAsync(new MouseEventArgs());
        var stored = await services.GetRequiredService<ProjectWorkbenchService>().GetStructureAsync(projectId);
        return (cut, Assert.Single(stored.Nodes, n => n.ObjectType == ProjectObjectType.WorkflowDefinition));
    }

    private static Task OpenStartAsync(IRenderedComponent<ProjectStructurePage> cut, ProjectStructureNode node) {
        var canvas = WaitForCanvasWorkbench(cut);
        return cut.InvokeAsync(() => canvas.Instance.OnContextAction(node.Id, "start-workflow", 0, 0));
    }
}
