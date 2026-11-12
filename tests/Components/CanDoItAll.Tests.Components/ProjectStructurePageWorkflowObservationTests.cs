using System.Text.Json;
using Bunit;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workbench.Pages;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.ProjectStructure;

public sealed partial class ProjectStructurePageWorkflowNodeTests {
    [Fact]
    public async Task Workflow_status_observes_terminal_failure_without_another_selection_or_submit() {
        await using var harness = await CreateHarnessAsync();
        var services = harness.Context.Services;
        var projectId = await CreateProjectAsync(services.GetRequiredService<ProjectsService>(), "Workflow live observation");
        var workflow = await CreateWorkflowAsync(services.GetRequiredService<IWorkflowCatalogService>(), "Observed workflow");
        var store = services.GetRequiredService<IWorkflowRunStore>();
        var now = DateTimeOffset.UtcNow;
        var run = new WorkflowRunSnapshot(WorkflowRunId.New(), workflow.Id, workflow.VersionId, WorkflowRunState.Running,
            WorkflowRuntimeBackendKind.InProcess, string.Empty, "Summarizing the input.", now, now);
        await store.SaveRunAsync(run);
        var workbench = services.GetRequiredService<ProjectWorkbenchService>();
        var node = await workbench.CreateObjectAsync(projectId, new(ProjectObjectType.WorkflowDefinition,
            workflow.Name, string.Empty, string.Empty, $"project:{projectId}", MetadataJson:
            ProjectObjectMetadataSerializer.Serialize(new ProjectObjectMetadataEnvelope {
                Workflow = new ProjectWorkflowNodeMetadata {
                    WorkflowId = workflow.Id, WorkflowVersionId = workflow.VersionId, LastRunId = run.RunId,
                    LastRunState = WorkflowRunState.Running, LastRunSummary = run.Summary
                }
            })));
        var cut = harness.Context.Render<ProjectStructurePage>(parameters => parameters.Add(page => page.ProjectId, projectId));
        var canvas = WaitForCanvasWorkbench(cut);
        await cut.InvokeAsync(() => canvas.Instance.OnSelectionChanged(node.Id, JsonSerializer.Serialize(new[] { node.Id })));
        await OpenStartAsync(cut, node);
        Assert.Equal(WorkflowRunState.Running, WorkflowDialogs(cut).WorkflowStartDialog!.Status!.State);

        await store.SaveRunAsync(run with { State = WorkflowRunState.Failed, Summary = "Output budget exhausted.", UpdatedAtUtc = now.AddSeconds(1) });

        cut.WaitForAssertion(() => {
            Assert.Equal(WorkflowRunState.Failed, WorkflowDialogs(cut).WorkflowStartDialog!.Status!.State);
            Assert.Contains("Output budget exhausted.", cut.Markup, StringComparison.Ordinal);
        }, TimeSpan.FromSeconds(10));
        Assert.Empty(cut.Instance.AuthoringOutcomes);
    }
}
