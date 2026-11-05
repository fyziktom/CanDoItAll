using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Components.Gantt;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workbench.Pages;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace CanDoItAll.Tests.Components.ProjectStructure;

[Trait("Category", "HostPlatform")]
public sealed class ProjectStructureGanttTaskComparisonTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Native_Gantt_Task_survives_same_Project_refresh_and_compares_again_when_its_schedule_changes(bool thirdWriterDuringReview) {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var services = harness.Context.Services;
        var workbench = services.GetRequiredService<ProjectWorkbenchService>();
        var project = (await services.GetRequiredService<ProjectsService>().SaveAsync(new() { Name = "Native Gantt Task comparison" })).Value;
        var admission = (await workbench.GetStructureAsync(project)).ExpectedProjectAdmission!;
        var owner = new ProjectStructureAgentContext("component-tests", "Native Gantt comparison", Environment.MachineName,
            AppContext.BaseDirectory, "", "native-gantt-task-comparison") { ExpectedProjectAdmission = admission };
        var start = new DateTimeOffset(2026, 11, 2, 12, 0, 0, TimeSpan.Zero).AddTicks(1234567);
        var created = await services.GetRequiredService<ProjectStructureTaskCreationService>().CreateAsync(project,
            new("Original Gantt Task", start, start.AddHours(8), Estimate: new(8, ProjectWorkItemEffortUnit.Hours, null, "")) {
                ExpectedProjectAdmission = admission
            }, owner);
        var initial = await workbench.GetStructureAsync(project);
        var original = Assert.Single(initial.Nodes, node => node.Id == created.TaskNodeId);
        var metadata = JsonNode.Parse(original.MetadataJson)!.AsObject();
        metadata["futureTaskConfig"] = new JsonObject { ["keep"] = true };
        metadata["workItem"]!.AsObject()["futureEffortPolicy"] = new JsonObject { ["keep"] = true };
        Assert.NotNull(await workbench.UpdateObjectAsync(project, original.Id,
            new(original.Title, original.Subtitle, original.Notes, original.StartUtc, original.EndUtc, metadata.ToJsonString(),
                original.DurationSeconds, original.NodeReferences) { ExpectedProjectAdmission = admission, ExpectedNode = original }));
        initial = await workbench.GetStructureAsync(project);
        original = Assert.Single(initial.Nodes, node => node.Id == created.TaskNodeId);
        start = original.StartUtc!.Value;
        var dialogs = services.GetRequiredService<DialogService>();
        var host = harness.Context.Render<DialogHost>();
        IRenderedComponent<ProjectStructureGanttPanel>? panel = null;
        var refreshes = 0;
        Func<Func<CancellationToken, ValueTask>, Task> read = async authorizeRead => {
            await authorizeRead(CancellationToken.None);
            refreshes++;
            var current = await workbench.GetStructureAsync(project);
            await authorizeRead(CancellationToken.None);
            var currentPanel = panel ?? throw new InvalidOperationException("The original Gantt host must be mounted before its reload callback.");
            await currentPanel.InvokeAsync(() => currentPanel.Render(parameters => parameters.Add(component => component.Surface, current)));
        };
        panel = harness.Context.Render<ProjectStructureGanttPanel>(parameters => parameters.Add(component => component.ProjectId, project)
            .Add(component => component.Surface, initial)
            .Add(component => component.ReadAuthoritativeProject, read));
        panel.WaitForAssertion(() => Assert.Single(panel.FindComponent<GanttChart>().Instance.Tasks));
        var chart = panel.FindComponent<GanttChart>().Instance;
        var opening = panel.InvokeAsync(() => chart.TaskDoubleClicked.InvokeAsync(new GanttTaskId(original.Id)));
        try {
            host.WaitForElement("[data-testid='project-structure-gantt-task-duration']");
            await host.InvokeAsync(() => host.Find("[data-testid='project-structure-gantt-task-duration']").InputAsync(new() { Value = "12" }));
            await using var peerScope = services.CreateAsyncScope();
            var peer = peerScope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();
            var peerNode = (await peer.UpdateObjectAsync(project, original.Id,
                new("Peer Gantt title", original.Subtitle, original.Notes, original.StartUtc, original.EndUtc,
                    original.MetadataJson, original.DurationSeconds, original.NodeReferences) { ExpectedProjectAdmission = admission, ExpectedNode = original }))!;
            var refreshed = await workbench.GetStructureAsync(project);
            await panel.InvokeAsync(() => panel.Render(parameters => parameters.Add(component => component.Surface, refreshed)));
            Assert.Equal("12", host.Find("[data-testid='project-structure-gantt-task-duration']").GetAttribute("value"));
            await TaskDialogTestDriver.ClickAsync(host, "planning-task-compare");
            host.WaitForElement("[data-testid='planning-task-field-title']");
            Assert.Contains("Peer Gantt title", host.Markup, StringComparison.Ordinal);
            if (thirdWriterDuringReview) {
                Assert.NotNull(await peer.UpdateObjectAsync(project, original.Id,
                    new(peerNode.Title, peerNode.Subtitle, peerNode.Notes, start, start.AddHours(10), peerNode.MetadataJson,
                        10 * 3600, peerNode.NodeReferences) { ExpectedProjectAdmission = admission, ExpectedNode = peerNode }));
                await TaskDialogTestDriver.ClickAsync(host, "planning-task-compare-apply");
                Assert.Contains("saved Task changed again", host.Markup, StringComparison.Ordinal);
                Assert.Equal("Original Gantt Task", host.Find("[data-testid='project-structure-gantt-task-title']").GetAttribute("value"));
                Assert.Equal("12", host.Find("[data-testid='project-structure-gantt-task-duration']").GetAttribute("value"));
                await TaskDialogTestDriver.ClickAsync(host, "planning-task-compare-refresh");
                await host.InvokeAsync(() => host.FindAll("[data-testid='planning-task-field-delivery'] button")
                    .Single(button => button.TextContent.Trim() == "Use my draft").ClickAsync(new()));
            }
            await TaskDialogTestDriver.ClickAsync(host, "planning-task-compare-apply");
            Assert.Equal("Peer Gantt title", host.Find("[data-testid='project-structure-gantt-task-title']").GetAttribute("value"));
            Assert.Equal("12", host.Find("[data-testid='project-structure-gantt-task-duration']").GetAttribute("value"));
            var beforeSave = Assert.Single((await workbench.GetStructureAsync(project)).Nodes, node => node.Id == original.Id);
            Assert.Equal(start.AddHours(thirdWriterDuringReview ? 10 : 8), beforeSave.EndUtc);
            await TaskDialogTestDriver.ClickAsync(host, "project-structure-gantt-task-submit");
            await opening.WaitAsync(TimeSpan.FromSeconds(5));
            var saved = Assert.Single((await workbench.GetStructureAsync(project)).Nodes, node => node.Id == original.Id);
            Assert.Equal("Peer Gantt title", saved.Title);
            Assert.Equal(start, saved.StartUtc);
            Assert.Equal(start.AddHours(12), saved.EndUtc);
            using var savedJson = JsonDocument.Parse(saved.MetadataJson);
            Assert.True(savedJson.RootElement.GetProperty("futureTaskConfig").GetProperty("keep").GetBoolean());
            Assert.True(savedJson.RootElement.GetProperty("workItem").GetProperty("futureEffortPolicy").GetProperty("keep").GetBoolean());
            Assert.Equal(1, refreshes);
        } finally {
            await TaskDialogTestDriver.CloseAsync(host, dialogs, opening);
        }
    }
}
