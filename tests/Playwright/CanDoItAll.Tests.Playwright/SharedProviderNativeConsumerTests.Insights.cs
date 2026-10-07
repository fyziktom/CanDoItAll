using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;
using Microsoft.Playwright;
using static CanDoItAll.Tests.Playwright.Smoke.ProjectFilesUiJourney;

namespace CanDoItAll.Tests.Playwright;

public sealed partial class SharedProviderNativeConsumerTests {
    [Fact]
    [Trait("Category", "ExternalSharedProviderUi")]
    public Task Final_image_WB2_selection_reports_and_agent_files_keep_native_owners_and_exact_approvals() =>
        RunFileConsumerAsync(planning: true, insights: true);

    private static async Task AssertInsightsConsumersAsync(SharedProviderConsumerFixture fixture, Guid projectId, string taskId) {
        var beforeTree = await TreeAsync(fixture, projectId, includeMetadata: true);
        var before = Assert.Single(beforeTree.Nodes, node => node.Id == taskId);
        await fixture.NavigateAsync($"/projects/{projectId:D}/structure");
        var page = fixture.Page;
        await page.GetByRole(AriaRole.Tab, new() { Name = "Canvas", Exact = true }).ClickAsync();
        await ReadyFileCanvasAsync(page);
        await SelectFileNodeAsync(page, taskId, before.Title);
        var selection = page.GetByTestId("project-structure-selection-window");
        await selection.GetByRole(AriaRole.Button, new() { Name = "Edit", Exact = true }).ClickAsync();
        var editor = page.GetByTestId("project-structure-task-create-content");
        const string description = "WB2 native Selection editor preserves planning precision.";
        await editor.GetByTestId("project-structure-task-create-notes").FillAsync(description);
        await editor.GetByTestId("project-structure-task-create-submit").ClickAsync();
        await Assertions.Expect(editor).Not.ToBeVisibleAsync();
        var afterTree = await TreeAsync(fixture, projectId, includeMetadata: true);
        var after = Assert.Single(afterTree.Nodes, node => node.Id == taskId);
        var expectedMetadata = JsonNode.Parse(before.MetadataJson)!;
        expectedMetadata["workItem"]!["description"] = description;
        Assert.True(JsonNode.DeepEquals(expectedMetadata, JsonNode.Parse(after.MetadataJson)));
        Assert.Equal(before.StartUtc, after.StartUtc);
        Assert.Equal(before.EndUtc, after.EndUtc);
        Assert.Equal(before.ProgressPercent, after.ProgressPercent);
        Assert.Equal(before.Title, after.Title);
        Assert.Equal(JsonSerializer.Serialize(beforeTree.Nodes.Where(node => node.Id != taskId), SharedProviderConsumerFixture.Json),
            JsonSerializer.Serialize(afterTree.Nodes.Where(node => node.Id != taskId), SharedProviderConsumerFixture.Json));
        await page.GetByRole(AriaRole.Tab, new() { Name = "Manager Summary", Exact = true }).ClickAsync();
        var report = page.GetByTestId("project-manager-summary");
        await Assertions.Expect(report.GetByTestId("manager-summary-empty")).ToBeVisibleAsync();
        await report.GetByTestId("manager-summary-content-mode").SelectOptionAsync(nameof(ProjectManagerSummaryContentMode.HistoryAndFuture));
        await Assertions.Expect(report.GetByTestId("manager-summary-empty")).ToBeVisibleAsync();
        await report.GetByTestId("manager-summary-load").ClickAsync();
        var metrics = report.GetByTestId("manager-summary-metrics");
        await Assertions.Expect(metrics).ToBeVisibleAsync();
        await Assertions.Expect(metrics).ToContainTextAsync("6h 30m");
        var acceptedMetrics = await metrics.InnerTextAsync();
        var asOf = report.GetByTestId("manager-summary-header-actions").GetByText(new Regex("^As of "));
        var acceptedAsOf = await asOf.InnerTextAsync();
        await report.GetByTestId("manager-summary-range-year").ClickAsync();
        await Assertions.Expect(report.GetByText("Options changed", new() { Exact = true })).ToBeVisibleAsync();
        Assert.Equal(acceptedMetrics, await metrics.InnerTextAsync());
        Assert.Equal(acceptedAsOf, await asOf.InnerTextAsync());
        await report.GetByTestId("manager-summary-open-warnings").ClickAsync();
        var warnings = page.GetByTestId("manager-summary-warnings-dialog");
        await Assertions.Expect(warnings).ToContainTextAsync("EUR");
        await fixture.ScreenshotAsync("wb2-native-report-currencies");
        await warnings.GetByRole(AriaRole.Button, new() { Name = "Close", Exact = true }).ClickAsync();
        await report.GetByTestId("manager-summary-open-activity").ClickAsync();
        var activity = page.GetByTestId("manager-summary-activity-dialog");
        await Assertions.Expect(activity.GetByRole(AriaRole.Row)).ToHaveCountAsync(6);
        var rows = await activity.GetByRole(AriaRole.Row).AllTextContentsAsync();
        await fixture.ScreenshotAsync("wb2-native-agent-activity");
        await activity.GetByRole(AriaRole.Button, new() { Name = "Close", Exact = true }).ClickAsync();
        await page.GetByRole(AriaRole.Tab, new() { Name = "Gantt", Exact = true }).ClickAsync();
        await Assertions.Expect(page.GetByTestId("project-structure-gantt-chart")).ToBeVisibleAsync();
        await page.GetByRole(AriaRole.Tab, new() { Name = "Manager Summary", Exact = true }).ClickAsync();
        Assert.Equal(acceptedMetrics, await metrics.InnerTextAsync());
        await fixture.EvidenceAsync("wb2-native-insights", new { ProjectId = projectId, Before = before, After = after,
            AcceptedMetrics = acceptedMetrics, AcceptedAsOf = acceptedAsOf, ActivityRows = rows, DraftRangeDidNotReload = true });
        await AssertInsightsDescendantAsync(fixture, projectId);
    }

    private static async Task AssertInsightsDescendantAsync(SharedProviderConsumerFixture fixture, Guid projectId) {
        var childId = await CreatePlanningProjectAsync(fixture, "WB2 descendant " + Guid.NewGuid().ToString("N"));
        var page = fixture.Page;
        await fixture.NavigateAsync($"/projects/{childId:D}/structure");
        await page.GetByRole(AriaRole.Tab, new() { Name = "Gantt", Exact = true }).ClickAsync();
        var start = new DateTimeOffset(DateTime.UtcNow.Date.AddDays(1).AddHours(9), TimeSpan.Zero);
        foreach (var priced in new[] { true, false }) {
            await page.GetByRole(AriaRole.Button, new() { Name = "Add task", Exact = true }).ClickAsync();
            var editor = page.GetByTestId("project-structure-gantt-task-dialog-content");
            await editor.GetByTestId("project-structure-gantt-task-title").FillAsync(priced ? "WB2 known zero" : "WB2 unpriced");
            await editor.GetByTestId("project-structure-gantt-task-start").FillAsync(start.ToString("O", CultureInfo.InvariantCulture));
            await editor.GetByTestId("project-structure-gantt-task-start").PressAsync("Tab");
            await editor.GetByTestId("project-structure-gantt-task-end").FillAsync(start.AddHours(priced ? 1 : 2).ToString("O", CultureInfo.InvariantCulture));
            await editor.GetByTestId("project-structure-gantt-task-end").PressAsync("Tab");
            await editor.GetByTestId("project-structure-gantt-task-estimate-cost").FillAsync(priced ? "0" : "");
            await editor.GetByTestId("project-structure-gantt-task-estimate-currency").FillAsync("USD");
            await editor.GetByTestId("project-structure-gantt-task-submit").ClickAsync();
            await Assertions.Expect(editor).Not.ToBeVisibleAsync();
            start = start.AddHours(2);
        }
        var child = await TreeAsync(fixture, childId, includeMetadata: true);
        await fixture.EvidenceAsync("wb2-native-child-before-hierarchy", child);
        Assert.Equal(0m, PlanningWorkItem(Assert.Single(child.Nodes, node => node.Title == "WB2 known zero")).ExpectedCostAmount);
        Assert.Null(PlanningWorkItem(Assert.Single(child.Nodes, node => node.Title == "WB2 unpriced")).ExpectedCostAmount);
        var parent = await TreeAsync(fixture, projectId, includeMetadata: true);
        var scheduledTasks = parent.Nodes.Concat(child.Nodes)
            .Where(node => node.ObjectType == ProjectObjectType.WorkItem && node.StartUtc.HasValue && node.EndUtc.HasValue).ToArray();
        var leadTime = scheduledTasks.Max(node => node.EndUtc)!.Value - scheduledTasks.Min(node => node.StartUtc)!.Value;
        var scheduledHours = scheduledTasks.Sum(node => (node.EndUtc!.Value - node.StartUtc!.Value).TotalHours);
        Assert.Equal(TimeSpan.FromHours(28), leadTime);
        Assert.Equal(9.5, scheduledHours);
        await fixture.NavigateAsync($"/projects/{projectId:D}/structure");
        await page.GetByRole(AriaRole.Tab, new() { Name = "Canvas", Exact = true }).ClickAsync();
        await ReadyFileCanvasAsync(page);
        var root = Assert.Single((await TreeAsync(fixture, projectId)).Nodes, node => node.Id == $"project:{projectId:D}");
        await SelectFileNodeAsync(page, root.Id, root.Title);
        await page.GetByTestId("project-structure-selection-window").GetByRole(AriaRole.Button, new() { Name = "Add subproject", Exact = true }).ClickAsync();
        var hierarchy = page.GetByTestId("project-structure-hierarchy-dialog");
        await hierarchy.GetByTestId("project-structure-hierarchy-project-select").SelectOptionAsync(childId.ToString("D"));
        await hierarchy.GetByTestId("project-structure-hierarchy-submit").ClickAsync();
        await Assertions.Expect(hierarchy).Not.ToBeVisibleAsync();
        var attachedChild = await TreeAsync(fixture, childId, includeMetadata: true);
        Assert.Contains(attachedChild.Nodes, node => node.ProjectRole == ProjectStructureProjectRole.ParentProject && node.RelatedProjectId == projectId);
        Assert.Equal(JsonSerializer.Serialize(child.Nodes.Where(node => node.ObjectType == ProjectObjectType.WorkItem), SharedProviderConsumerFixture.Json),
            JsonSerializer.Serialize(attachedChild.Nodes.Where(node => node.ObjectType == ProjectObjectType.WorkItem), SharedProviderConsumerFixture.Json));
        await page.GetByRole(AriaRole.Tab, new() { Name = "Manager Summary", Exact = true }).ClickAsync();
        var report = page.GetByTestId("project-manager-summary");
        await report.GetByTestId("manager-summary-content-mode").SelectOptionAsync(nameof(ProjectManagerSummaryContentMode.HistoryAndFuture));
        await report.GetByTestId("manager-summary-project-scope").SelectOptionAsync(nameof(ProjectManagerSummaryScope.ProjectAndDescendants));
        await report.GetByTestId("manager-summary-load").ClickAsync();
        await Assertions.Expect(report.GetByTestId("manager-summary-metrics")).ToContainTextAsync("1d 4h");
        await report.GetByTestId("manager-summary-open-warnings").ClickAsync();
        await Assertions.Expect(page.GetByTestId("manager-summary-warnings-dialog")).ToContainTextAsync("EUR");
        await fixture.ScreenshotAsync("wb2-native-descendant-coverage");
        var childAfter = await TreeAsync(fixture, childId, includeMetadata: true);
        Assert.Equal(JsonSerializer.Serialize(attachedChild.Nodes, SharedProviderConsumerFixture.Json),
            JsonSerializer.Serialize(childAfter.Nodes, SharedProviderConsumerFixture.Json));
        await fixture.EvidenceAsync("wb2-native-descendants", new { ProjectId = projectId, ChildProjectId = childId, LeadTime = leadTime, ScheduledHours = scheduledHours,
            Child = child, AttachedChild = attachedChild, AcceptedMetrics = await report.GetByTestId("manager-summary-metrics").InnerTextAsync(),
            Warnings = await page.GetByTestId("manager-summary-warnings-dialog").InnerTextAsync() });
    }
}
