using System.Net.Http.Json;
using System.Text.Json;
using CanDoItAll.AgentFramework.Models;
using Microsoft.Playwright;

namespace CanDoItAll.Tests.Playwright;

public sealed partial class SharedProviderNativeConsumerTests {
    [Fact]
    [Trait("Category", "ExternalSharedProviderUi")]
    public async Task Final_image_native_navigation_keeps_tab_drafts_and_retires_them_across_browser_history() {
        await using var fixture = await SharedProviderConsumerFixture.StartAsync();
        var first = await AuthorNativeDefinitionAsync(fixture, human: false);
        var second = await AuthorNativeDefinitionAsync(fixture, human: true);
        var page = fixture.Page;
        var firstRoute = $"/agents/workflows?workflowId={first.Id.Value:D}";
        var secondRoute = $"/agents/workflows?workflowId={second.Id.Value:D}";
        await fixture.NavigateAsync(firstRoute);
        await ExpectEditorAsync(first.Name);
        var unsavedName = first.Name + " local draft";
        await page.GetByTestId("workflow-canvas-name").FillAsync(unsavedName);
        foreach (var tab in new[] { "dashboard", "workflows", "history", "analytics" }) {
            await page.GetByTestId("workflows-tab-" + tab).ClickAsync();
            await ExpectEditorAsync(unsavedName);
        }
        await fixture.NavigateAsync(secondRoute);
        await ExpectEditorAsync(second.Name);
        await SharedProviderTwoInstanceUiAcceptanceTests.NavigateAsync(page, fixture.Settings.Clients[0] + firstRoute,
            _ => page.GoBackAsync());
        await Assertions.Expect(page).ToHaveURLAsync(fixture.Settings.Clients[0] + firstRoute);
        await ExpectEditorAsync(first.Name);
        await SharedProviderTwoInstanceUiAcceptanceTests.NavigateAsync(page, fixture.Settings.Clients[0] + secondRoute,
            _ => page.GoForwardAsync());
        await Assertions.Expect(page).ToHaveURLAsync(fixture.Settings.Clients[0] + secondRoute);
        await ExpectEditorAsync(second.Name);
        Assert.Equal(first.VersionId, (await ReadNativeDefinitionAsync(fixture, first.Id)).VersionId);
        Assert.Equal(second.VersionId, (await ReadNativeDefinitionAsync(fixture, second.Id)).VersionId);
        await fixture.ScreenshotAsync("wf1-native-browser-history-1920");
        await fixture.EvidenceAsync("wf1-native-browser-history", new {
            First = first.Id, FirstVersion = first.VersionId, Second = second.Id, SecondVersion = second.VersionId,
            OrdinaryTabsRetainDraft = true, TargetChangeRetiresDraft = true, BackRestoresNativeDefinition = true,
            ForwardRestoresNativeDefinition = true, AdditionalVersions = 0
        });

        async Task ExpectEditorAsync(string name) {
            await page.GetByTestId("workflows-tab-editor").ClickAsync();
            await Assertions.Expect(page.GetByTestId("workflow-canvas-name")).ToHaveValueAsync(name);
        }
    }

    [Fact]
    [Trait("Category", "ExternalSharedProviderUi")]
    public async Task Final_image_canvas_save_and_human_response_keep_exact_definition_and_single_run() {
        await using var fixture = await SharedProviderConsumerFixture.StartAsync();
        var definition = await AuthorNativeDefinitionAsync(fixture, human: true);
        var page = fixture.Page;
        await page.GetByTestId("workflows-tab-history").ClickAsync();
        await page.GetByTestId("workflows-test-input").FillAsync("{\"message\":\"WF1 held input\"}");
        await page.GetByTestId("workflows-test-input").PressAsync("Tab");
        await page.GetByTestId("workflows-run-test").ClickAsync();
        var waiting = await AwaitWorkflowStateAsync(fixture, definition, WorkflowRunState.WaitingForInput);
        var runId = waiting.GetProperty("runId").GetGuid();
        var detail = await fixture.GetAsync($"api/workflows/runs/{runId:D}/detail");
        Assert.Single(detail.GetProperty("pendingExternalRequests").EnumerateArray());
        await fixture.NavigateAsync($"/agents/workflows?workflowId={definition.Id.Value:D}&runId={runId:D}");
        await page.GetByTestId("workflows-tab-history").ClickAsync();
        await page.GetByTestId("workflows-pending-response").FillAsync("{\"answer\":\"WF1 human accepted\"}");
        await page.GetByTestId("workflows-pending-response").PressAsync("Tab");
        await page.GetByTestId("workflows-respond-request").ClickAsync();
        var completed = await AwaitWorkflowStateAsync(fixture, definition, WorkflowRunState.Completed);
        Assert.Equal(runId, completed.GetProperty("runId").GetGuid());
        var final = await fixture.GetAsync($"api/workflows/runs/{runId:D}/detail");
        Assert.Empty(final.GetProperty("pendingExternalRequests").EnumerateArray());
        await fixture.ScreenshotAsync("wf1-human-completed-1920");
        await fixture.EvidenceAsync("wf1-human-response", new {
            WorkflowId = definition.Id.Value, VersionId = definition.VersionId.Value, RunId = runId,
            PendingBefore = 1, PendingAfter = 0, State = WorkflowRunState.Completed
        });
    }

    [Fact]
    [Trait("Category", "ExternalSharedProviderUi")]
    public async Task Final_image_ui_authored_workflow_is_delivered_by_actual_Quartz_schedule() {
        await using var fixture = await SharedProviderConsumerFixture.StartAsync();
        var draft = await AuthorNativeDefinitionAsync(fixture, human: false);
        var published = await fixture.PostAsync<WorkflowDefinition>(
            $"api/workflows/definitions/{draft.Id.Value:D}/publish?expectedVersionId={draft.VersionId.Value:D}", new { });
        var page = fixture.Page;
        await fixture.NavigateAsync("/scheduler");
        await page.GetByTestId("scheduler-tab-new").ClickAsync();
        await page.GetByTestId("scheduler-name").FillAsync(published.Name);
        await page.GetByTestId("scheduler-target-open").ClickAsync();
        await page.GetByTestId("scheduler-target-search").FillAsync(published.Name);
        await page.GetByTestId("scheduler-target-search").PressAsync("Tab");
        await page.GetByTestId("scheduler-target-card").Filter(new() { HasTextString = published.Name }).ClickAsync();
        await Assertions.Expect(page.GetByTestId("scheduler-target-selected")).ToContainTextAsync(published.Name);
        await page.GetByTestId("scheduler-input-message").FillAsync("WF1 scheduled input");
        await page.GetByTestId("scheduler-timezone").FillAsync("UTC");
        var fireAt = DateTimeOffset.UtcNow.AddSeconds(45);
        await page.GetByTestId("scheduler-cron").FillAsync($"{fireAt.Second} {fireAt.Minute} {fireAt.Hour} {fireAt.Day} {fireAt.Month} ? {fireAt.Year}");
        await page.GetByTestId("scheduler-save").ClickAsync();
        await Assertions.Expect(page.GetByTestId("scheduler-draft-receipt")).ToContainTextAsync("Committed");
        var receipt = await page.GetByTestId("scheduler-draft-receipt").InnerTextAsync();
        var run = await AwaitWorkflowStateAsync(fixture, published, WorkflowRunState.Completed);
        await page.GetByTestId("scheduler-tab-history").ClickAsync();
        await page.GetByTestId("scheduler-refresh").ClickAsync();
        await Assertions.Expect(page.GetByTestId("scheduler-history")).ToContainTextAsync(published.Name);
        await fixture.ScreenshotAsync("wf1-quartz-delivery-1920");
        await fixture.EvidenceAsync("wf1-quartz-delivery", new {
            WorkflowId = published.Id.Value, VersionId = published.VersionId.Value, FireAt = fireAt,
            RunId = run.GetProperty("runId").GetGuid(), Receipt = receipt,
            History = await page.GetByTestId("scheduler-history").InnerTextAsync()
        });
        await page.GetByTestId("scheduler-tab-schedules").ClickAsync();
        var plan = page.GetByTestId("scheduler-plan-card").Filter(new() { HasTextString = published.Name });
        await plan.GetByRole(AriaRole.Button, new() { Name = "Pause", Exact = true }).ClickAsync();
        await Assertions.Expect(plan.GetByRole(AriaRole.Button, new() { Name = "Resume", Exact = true })).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByTestId("scheduler-plan-receipt")).ToContainTextAsync("Committed");
    }

    private static async Task<WorkflowDefinition> AuthorNativeDefinitionAsync(SharedProviderConsumerFixture fixture, bool human) {
        var name = "WF1 " + (human ? "human " : "scheduled ") + Guid.NewGuid().ToString("N");
        var shape = new WorkflowValueShape(WorkflowValueShapeKind.Json, "{\"type\":\"object\"}", "Structured input");
        WorkflowNode Node(string id, WorkflowNodeKind kind, double x) => new(new(id), kind, id, [],
            new(null, null, null, null, "Preserve the input.", shape, shape)) { CanvasX = x, CanvasY = 100 };
        var start = Node("start", WorkflowNodeKind.Start, 0);
        var end = Node("end", WorkflowNodeKind.End, 1000);
        WorkflowNode[] nodes = human ? [start, Node("human", WorkflowNodeKind.HumanInput, 500), end] : [start, end];
        var edges = nodes.Zip(nodes.Skip(1), (left, right) => new WorkflowEdge(new(left.Id.Value + "-" + right.Id.Value),
            left.Id, null, right.Id, null, WorkflowEdgeKind.Direct, "") { Routing = WorkflowEdgeRouting.Always }).ToArray();
        var initial = await fixture.PostAsync<WorkflowDefinition>("api/workflows/definitions", new WorkflowDefinitionSaveRequest(null, null,
            name, "WF1 exact native authoring consumer", WorkflowLifecycleStatus.Draft, new(start.Id, nodes, edges),
            new(WorkflowRuntimeBackendKind.InProcess, true, false, false, false)) {
            InputParameters = [new("message", "Message", WorkflowInputParameterKind.Text, true, "Retained native input", "$.message",
                "WF1 default", WorkflowInputParameterOptionSource.None, null, null, "Enter message")]
        });
        var page = fixture.Page;
        await fixture.NavigateAsync($"/agents/workflows?workflowId={initial.Id.Value:D}");
        await page.GetByTestId("workflows-tab-editor").ClickAsync();
        await page.GetByTestId("workflow-canvas-name").FillAsync(name + " authored");
        await page.GetByTestId("workflows-tab-dashboard").ClickAsync();
        await page.GetByTestId("workflows-tab-editor").ClickAsync();
        await Assertions.Expect(page.GetByTestId("workflow-canvas-name")).ToHaveValueAsync(name + " authored");
        await page.GetByTestId("workflow-canvas-save").ClickAsync();
        await page.GetByText("Workflow saved", new() { Exact = true }).WaitForAsync();
        var saved = (await fixture.Api.GetFromJsonAsync<WorkflowDefinitionDetail>(
            $"api/workflows/definitions/{initial.Id.Value:D}", SharedProviderConsumerFixture.Json))!.Definition;
        Assert.NotEqual(initial.VersionId, saved.VersionId);
        Assert.Equal(name + " authored", saved.Name);
        Assert.Equal(JsonSerializer.Serialize(initial.Graph), JsonSerializer.Serialize(saved.Graph));
        Assert.Equal(JsonSerializer.Serialize(initial.InputParameters), JsonSerializer.Serialize(saved.InputParameters));
        await fixture.EvidenceAsync("wf1-native-authoring-" + (human ? "human" : "scheduled"), new {
            WorkflowId = saved.Id.Value, OriginalVersion = initial.VersionId.Value, SavedVersion = saved.VersionId.Value,
            GraphSha256 = SharedProviderConsumerFixture.Hash(JsonSerializer.Serialize(saved.Graph)), Inputs = saved.InputParameters
        });
        return saved;
    }

    private static async Task<JsonElement> AwaitWorkflowStateAsync(SharedProviderConsumerFixture fixture, WorkflowDefinition definition, WorkflowRunState expected) {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(120));
        while (true) {
            var runs = await fixture.GetAsync($"api/workflows/runs?workflowId={definition.Id.Value:D}");
            if (runs.GetArrayLength() > 0) {
                var run = Assert.Single(runs.EnumerateArray());
                Assert.Equal(definition.VersionId.Value, run.GetProperty("versionId").GetGuid());
                if (run.GetProperty("state").GetInt32() == (int)expected) {
                    return run.Clone();
                }
                Assert.NotEqual((int)WorkflowRunState.Failed, run.GetProperty("state").GetInt32());
            }
            await Task.Delay(250, deadline.Token);
        }
    }
}
