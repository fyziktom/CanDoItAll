using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workbench.Pages;
using CanDoItAll.SharedKernel;
using CanDoItAll.Workbench.Planning.UI;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace CanDoItAll.Tests.Components.ProjectStructure;

[Trait("Category", "HostPlatform")]
public sealed class ProjectStructureTaskComparisonTests(ITestOutputHelper output) {
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task Native_canvas_Task_comparison_preserves_later_due_dates_and_retains_a_deleted_original_draft(bool dueChangedAfterApply, bool deletedDuringReview) {
        await using var harness = await ComponentTestHarness.CreateAsync();
        output.WriteLine($"Owned Task comparison fixture root: {harness.RootPath}");
        harness.Context.JSInterop.Setup<bool>("CanDoItAll.canvasWorkbench.create", _ => true).SetResult(true);
        var services = harness.Context.Services;
        var projects = services.GetRequiredService<ProjectsService>();
        var workbench = services.GetRequiredService<ProjectWorkbenchService>();
        var project = (await projects.SaveAsync(new() { Name = "Native Task comparison" })).Value;
        var admission = (await workbench.GetStructureAsync(project)).ExpectedProjectAdmission!;
        var owner = new ProjectStructureAgentContext("component-tests", "Native Task comparison",
            Environment.MachineName, AppContext.BaseDirectory, "", "native-task-comparison") { ExpectedProjectAdmission = admission };
        var start = new DateTimeOffset(2026, 11, 2, 12, 0, 0, TimeSpan.Zero);
        var created = await services.GetRequiredService<ProjectStructureTaskCreationService>().CreateAsync(project,
            new("Original Task", start, start.AddHours(8), Estimate: new(8, ProjectWorkItemEffortUnit.Hours, null, "")) {
                ExpectedProjectAdmission = admission
            }, owner);
        var original = Assert.Single((await workbench.GetStructureAsync(project)).Nodes, node => node.Id == created.TaskNodeId);
        services.GetRequiredService<NavigationManager>().NavigateTo($"/projects/{project:D}/structure");
        var dialogs = services.GetRequiredService<DialogService>();
        var host = harness.Context.Render<DialogHost>();
        var page = harness.Context.Render<ProjectStructurePage>(parameters => parameters.Add(component => component.ProjectId, project));
        page.WaitForElement("[data-testid='project-structure-canvas-loaded']");
        var canvas = page.FindComponent<CanvasWorkbench>().Instance;
        var opening = page.InvokeAsync(() => canvas.OnContextAction(original.Id, "edit", 0, 0));
        try {
            host.WaitForElement("[data-testid='project-structure-task-create-notes']");
            await host.InvokeAsync(() => host.Find("[data-testid='project-structure-task-create-notes']").InputAsync(new() { Value = "Local task notes retained" }));
            await using var peerScope = services.CreateAsyncScope();
            var peer = peerScope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();
            var peerNode = await peer.UpdateObjectAsync(project, original.Id,
                new("Peer Task title", original.Subtitle, original.Notes, original.StartUtc, original.EndUtc, original.MetadataJson,
                    original.DurationSeconds, original.NodeReferences) { ExpectedProjectAdmission = admission, ExpectedNode = original });
            Assert.NotNull(peerNode);

            await host.InvokeAsync(() => host.Find("[data-testid='planning-task-compare']").ClickAsync(new()));
            host.WaitForElement("[data-testid='planning-task-comparison-dialog']");
            Assert.Contains("Original Task", host.Markup, StringComparison.Ordinal);
            Assert.Contains("Peer Task title", host.Markup, StringComparison.Ordinal);
            if (deletedDuringReview) {
                var deleting = await peer.GetStructureAsync(project);
                Assert.Equal(1, await peer.DeleteObjectAsync(project, original.Id, mutationOwner: owner with {
                    ExpectedGraph = ProjectStructureGraphStateGuard.Capture(deleting)
                }));
                await TaskDialogTestDriver.ClickAsync(host, "planning-task-compare-apply");
                Assert.DoesNotContain((await workbench.GetStructureAsync(project)).Nodes, node => node.Id == original.Id);
                Assert.Equal("Local task notes retained", host.Find("[data-testid='project-structure-task-create-notes']").GetAttribute("value"));
                Assert.Contains("deleted", host.Find("[data-testid='planning-task-compare-status']").TextContent, StringComparison.Ordinal);
                Assert.DoesNotContain("Peer Task title", host.Markup, StringComparison.Ordinal);
                Assert.True(host.Find("[data-testid='planning-task-compare']").HasAttribute("disabled"));
                return;
            }
            await host.InvokeAsync(() => host.Find("[data-testid='planning-task-compare-apply']").ClickAsync(new()));

            Assert.Equal("Peer Task title", host.Find("[data-testid='project-structure-task-create-title']").GetAttribute("value"));
            Assert.Equal("Local task notes retained", host.Find("[data-testid='project-structure-task-create-notes']").GetAttribute("value"));
            var beforeSave = Assert.Single((await workbench.GetStructureAsync(project)).Nodes, node => node.Id == original.Id);
            Assert.Equal(original.Notes, beforeSave.Notes);
            Assert.Equal(peerNode.MetadataJson, beforeSave.MetadataJson);
            if (dueChangedAfterApply) {
                var metadata = ProjectObjectMetadataSerializer.Parse(beforeSave.MetadataJson);
                metadata.WorkItem!.DueUtc = start.AddDays(2);
                Assert.NotNull(await peer.UpdateObjectAsync(project, original.Id,
                    new(beforeSave.Title, beforeSave.Subtitle, beforeSave.Notes, beforeSave.StartUtc, beforeSave.EndUtc,
                        ProjectObjectMetadataSerializer.Serialize(metadata), beforeSave.DurationSeconds, beforeSave.NodeReferences) {
                        ExpectedProjectAdmission = admission, ExpectedNode = beforeSave
                    }));
            }
            await host.InvokeAsync(() => host.Find("[data-testid='project-structure-task-create-submit']").ClickAsync(new()));
            if (dueChangedAfterApply) {
                var afterRejected = Assert.Single((await workbench.GetStructureAsync(project)).Nodes, node => node.Id == original.Id);
                Assert.Equal(start.AddDays(2), ProjectObjectMetadataSerializer.Parse(afterRejected.MetadataJson).WorkItem!.DueUtc);
                Assert.Equal(original.Notes, afterRejected.Notes);
                Assert.Equal(PlanningTaskSaveStatus.Rejected, host.FindComponent<PlanningStructureTaskEditor>().Instance.ReadCloseRequest().Outcome!.Status);
                Assert.Equal("Local task notes retained", host.Find("[data-testid='project-structure-task-create-notes']").GetAttribute("value"));
                await host.InvokeAsync(() => host.Find("[data-testid='planning-task-compare']").ClickAsync(new()));
                await host.InvokeAsync(() => host.Find("[data-testid='planning-task-compare-apply']").ClickAsync(new()));
                await host.InvokeAsync(() => host.Find("[data-testid='project-structure-task-create-submit']").ClickAsync(new()));
            }
            await opening;

            var saved = Assert.Single((await workbench.GetStructureAsync(project)).Nodes, node => node.Id == original.Id);
            Assert.Equal("Peer Task title", saved.Title);
            Assert.Equal("Local task notes retained", saved.Notes);
            if (dueChangedAfterApply) {
                Assert.Equal(start.AddDays(2), ProjectObjectMetadataSerializer.Parse(saved.MetadataJson).WorkItem!.DueUtc);
            }
        } finally {
            await TaskDialogTestDriver.CloseAsync(host, dialogs, opening);
        }
    }
}
