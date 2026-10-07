using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;
using Microsoft.Playwright;

namespace CanDoItAll.Tests.Playwright;

public sealed partial class SharedProviderNativeConsumerTests {
    private const string PlanningTaskTitle = "WB1 operator delivery";
    private const string PlanningAgentTitle = "WB1 agent reviewed delivery";
    private const string PlanningMeetingTitle = "WB1 dated review";

    [Fact]
    [Trait("Category", "ExternalSharedProviderUi")]
    public Task Final_image_WB1_operator_task_and_agent_files_keep_native_planning_and_exact_approvals() =>
        RunFileConsumerAsync(planning: true);

    private static async Task<Guid> CreatePlanningProjectAsync(SharedProviderConsumerFixture fixture, string name) {
        await fixture.NavigateAsync("/projects");
        var page = fixture.Page;
        await Assertions.Expect(page.GetByTestId("projects-workspace")).ToHaveAttributeAsync("data-interactive", "true");
        await page.GetByTestId("projects-new-button").ClickAsync();
        var editor = page.GetByTestId("projects-editor-modal");
        await editor.GetByTestId("project-name-input").FillAsync(name);
        await editor.GetByTestId("project-phase-input").FillAsync("WB1 native planning");
        await editor.GetByRole(AriaRole.Button, new() { Name = "5. Review", Exact = true }).ClickAsync();
        await editor.GetByTestId("project-save-button").ClickAsync();
        await Assertions.Expect(editor).ToContainTextAsync("Project saved", new() { Timeout = 60_000 });
        var projects = await fixture.Api.GetFromJsonAsync<ProjectSummary[]>("api/projects", SharedProviderConsumerFixture.ReadJson);
        var project = Assert.Single(projects!, item => item.Name == name);
        await fixture.EvidenceAsync("wb1-operator-project", new { project.Id, project.Name, CreatedThroughProjectsUi = true });
        return project.Id;
    }

    private static async Task<ProjectStructureNodeSummary> CreatePlanningTaskAsync(SharedProviderConsumerFixture fixture, Guid projectId) {
        var start = new DateTimeOffset(DateTime.UtcNow.Date.AddHours(9), TimeSpan.Zero);
        var end = start.AddHours(6.5);
        await fixture.NavigateAsync($"/projects/{projectId:D}/structure");
        var page = fixture.Page;
        await page.GetByRole(AriaRole.Tab, new() { Name = "Gantt", Exact = true }).ClickAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Add task", Exact = true }).ClickAsync();
        var editor = page.GetByTestId("project-structure-gantt-task-dialog-content");
        await editor.GetByTestId("project-structure-gantt-task-title").FillAsync(PlanningTaskTitle);
        await editor.GetByTestId("project-structure-gantt-task-start").FillAsync(start.ToString("O", CultureInfo.InvariantCulture));
        await editor.GetByTestId("project-structure-gantt-task-start").PressAsync("Tab");
        await Assertions.Expect(editor.GetByTestId("project-structure-gantt-task-end"))
            .ToHaveValueAsync(start.AddHours(8).ToString("yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture));
        await editor.GetByTestId("project-structure-gantt-task-end").FillAsync(end.ToString("O", CultureInfo.InvariantCulture));
        await editor.GetByTestId("project-structure-gantt-task-end").PressAsync("Tab");
        await Assertions.Expect(editor.GetByTestId("project-structure-gantt-task-duration")).ToHaveValueAsync("6.5");
        await editor.GetByTestId("project-structure-gantt-task-estimate-effort").FillAsync("3.1256789");
        await editor.GetByTestId("project-structure-gantt-task-estimate-cost").FillAsync("125.123456789");
        await editor.GetByTestId("project-structure-gantt-task-estimate-currency").FillAsync("EUR");
        await editor.GetByTestId("project-structure-gantt-task-submit").ClickAsync();
        await Assertions.Expect(editor).Not.ToBeVisibleAsync();
        var task = Assert.Single((await TreeAsync(fixture, projectId, includeMetadata: true)).Nodes, node => node.Title == PlanningTaskTitle);
        Assert.Equal(ProjectObjectType.WorkItem, task.ObjectType);
        Assert.Equal("task", task.ObjectSubtype);
        Assert.Equal(start, task.StartUtc);
        Assert.Equal(end, task.EndUtc);
        var work = PlanningWorkItem(task);
        Assert.Equal(3.1256789m, work.ExpectedEffortHours);
        Assert.Equal(125.123456789m, work.ExpectedCostAmount);
        Assert.Equal("EUR", work.ExpectedCostCurrencyCode);
        var meeting = await fixture.PostAsync<ProjectStructureNodeSummary>($"api/project-structure/projects/{projectId:D}/nodes",
            new ProjectStructureNodeCreateInput(ProjectObjectType.Meeting, PlanningMeetingTitle, "", "", $"project:{projectId:D}",
                StartUtc: end.AddHours(1), EndUtc: end.AddHours(2)));
        Assert.Equal(end.AddHours(1), meeting.StartUtc);
        await fixture.EvidenceAsync("wb1-operator-task", new { ProjectId = projectId, Task = task, Meeting = meeting });
        return task;
    }

    private static ProjectWorkItemMetadata PlanningWorkItem(ProjectStructureNodeSummary task) =>
        Assert.IsType<ProjectWorkItemMetadata>(JsonSerializer.Deserialize<ProjectObjectMetadataEnvelope>(task.MetadataJson!,
            SharedProviderConsumerFixture.ReadJson)!.WorkItem);

    private static async Task UpdatePlanningTaskAsync(SharedProviderConsumerFixture fixture, ILocator chat, ProviderProfile profile,
        string route, string marker, Guid agentId, Guid projectId, ProjectStructureNodeSummary original) {
        var before = await TreeAsync(fixture, projectId, includeMetadata: true);
        var task = Assert.Single(before.Nodes, node => node.Id == original.Id);
        var work = PlanningWorkItem(task);
        var estimate = new ProjectTaskEstimate(work.ExpectedEffortHours, work.ExpectedEffortUnit,
            work.ExpectedCostAmount, work.ExpectedCostCurrencyCode);
        var execution = new ProjectTaskExecutionSnapshot(work.ExecutionState, work.ActualStartedAtUtc, work.ActualEndedAtUtc);
        var request = new ProjectStructureTaskUpdateAgentInput(task.Id, task.Title, PlanningAgentTitle, task.ProgressPercent, 37,
            estimate, estimate, null, false, null, execution, execution, work.ExpectedCostBasis, work.DirectAssignmentRevision);
        var arguments = JsonSerializer.SerializeToElement(new { projectId, request }, SharedProviderConsumerFixture.ReadJson);
        await fixture.ScriptAsync(profile.GetModelDisplayName(route), marker,
            ToolsStep(Call(ProjectStructureToolPolicy.ProjectTaskUpdate, arguments)), TextStep("The selected native task was reviewed."));
        var run = await FileTurnAsync(fixture, chat, agentId, projectId, task.Id,
            "Rename the selected task to WB1 agent reviewed delivery and report 37 percent progress, retaining all other task fields.",
            validate: (payload, accepted) => {
                if (payload.ToolName != ProjectStructureToolPolicy.ProjectTaskUpdate) {
                    return FileProposalRefusal.UnexpectedTool;
                }
                if (accepted.Contains(payload.ToolName)) {
                    return FileProposalRefusal.DuplicateEffect;
                }
                using var actual = JsonDocument.Parse(payload.ArgumentsJson);
                return JsonElement.DeepEquals(arguments, actual.RootElement) ? FileProposalRefusal.None : FileProposalRefusal.WrongTarget;
            }, planning: true);
        await fixture.AssertScriptCompleteAsync(2);
        Assert.Contains(run.GetProperty("receipts").EnumerateArray(), receipt =>
            receipt.GetProperty("toolName").GetString() == ProjectStructureToolPolicy.ProjectTaskUpdate &&
            receipt.GetProperty("invocationOutcome").GetInt32() == (int)AgentToolInvocationOutcome.Succeeded);
        var after = await TreeAsync(fixture, projectId, includeMetadata: true);
        var updated = Assert.Single(after.Nodes, node => node.Id == task.Id);
        Assert.Equal(PlanningAgentTitle, updated.Title);
        Assert.Equal(37, updated.ProgressPercent);
        Assert.Equal(task.StartUtc, updated.StartUtc);
        Assert.Equal(task.EndUtc, updated.EndUtc);
        Assert.Equal(task.MetadataJson, updated.MetadataJson);
        Assert.Equal(JsonSerializer.Serialize(before.Nodes.Where(node => node.Id != task.Id), SharedProviderConsumerFixture.Json),
            JsonSerializer.Serialize(after.Nodes.Where(node => node.Id != task.Id), SharedProviderConsumerFixture.Json));
        await fixture.EvidenceAsync("wb1-agent-task-readback", new { Before = task, After = updated, RunId = run.GetProperty("runId") });
    }

    private static async Task AssertPlanningConsumersAsync(SharedProviderConsumerFixture fixture, Guid projectId,
        ProjectStructureNodeSummary original) {
        await fixture.NavigateAsync($"/projects/{projectId:D}/structure");
        var page = fixture.Page;
        await page.GetByRole(AriaRole.Tab, new() { Name = "Gantt", Exact = true }).ClickAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = PlanningAgentTitle, Exact = true })).ToBeVisibleAsync();
        await fixture.ScreenshotAsync("wb1-agent-gantt");
        await fixture.NavigateAsync($"/projects/{projectId:D}/calendar");
        await page.GetByRole(AriaRole.Button, new() { Name = "List", Exact = true }).ClickAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Row).Filter(new() { HasText = PlanningAgentTitle })).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Row).Filter(new() { HasText = PlanningMeetingTitle })).ToBeVisibleAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Open downloads menu", Exact = true }).ClickAsync();
        var download = await page.RunAndWaitForDownloadAsync(() =>
            page.GetByRole(AriaRole.Menuitem, new() { Name = "Download CSV", Exact = true }).ClickAsync());
        var path = Path.Combine(fixture.Settings.Evidence, "wb1-native-calendar.csv");
        await download.SaveAsAsync(path);
        var csv = await File.ReadAllTextAsync(path);
        Assert.Contains(PlanningAgentTitle, csv, StringComparison.Ordinal);
        Assert.Contains(PlanningMeetingTitle, csv, StringComparison.Ordinal);
        Assert.DoesNotContain(PlanningTaskTitle, csv, StringComparison.Ordinal);
        await fixture.ScreenshotAsync("wb1-agent-calendar");
        await fixture.NavigateAsync("/projects");
        await page.GoBackAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Row).Filter(new() { HasText = PlanningAgentTitle })).ToBeVisibleAsync();
        var final = Assert.Single((await TreeAsync(fixture, projectId, includeMetadata: true)).Nodes, node => node.Id == original.Id);
        Assert.Equal(original.MetadataJson, final.MetadataJson);
        Assert.Equal(original.StartUtc, final.StartUtc);
        Assert.Equal(original.EndUtc, final.EndUtc);
        await fixture.EvidenceAsync("wb1-native-calendar-export", new { ProjectId = projectId, TaskId = final.Id,
            CsvSha256 = SharedProviderConsumerFixture.Hash(csv), SavedListViewReopened = true, ReadOnlyActionsPreservedTask = true });
    }
}
