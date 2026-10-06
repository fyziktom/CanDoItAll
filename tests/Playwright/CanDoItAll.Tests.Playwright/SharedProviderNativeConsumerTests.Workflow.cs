using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.TestLab;
using Microsoft.Playwright;

namespace CanDoItAll.Tests.Playwright;

public sealed partial class SharedProviderNativeConsumerTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    [Trait("Category", "ExternalSharedProviderUi")]
    public Task Final_image_saved_workflow_and_TestLab_preserve_accepted_and_incomplete_output(bool incomplete)
        => RunSavedWorkflowAsync(incomplete, SharedProviderConsumerClient.A);

    [Fact]
    [Trait("Category", "ExternalSharedProviderUi")]
    public Task Final_image_second_client_saved_workflow_keeps_its_native_output_and_source_route()
        => RunSavedWorkflowAsync(false, SharedProviderConsumerClient.B);

    private static async Task RunSavedWorkflowAsync(bool incomplete, SharedProviderConsumerClient client) {
        await using var fixture = await SharedProviderConsumerFixture.StartAsync(client);
        var profile = await ImportedResponsesAsync(fixture);
        var options = await fixture.Api.GetFromJsonAsync<WorkflowProviderOption[]>("api/workflows/provider-options", SharedProviderConsumerFixture.Json);
        var sharedOption = Assert.Single(options!, option => option.ProviderProfileId == profile.Id);
        Assert.True(sharedOption.IsSourceManaged);
        Assert.Equal(profile.ModelCatalog, sharedOption.ModelCatalog);
        var alternate = incomplete
            ? profile.ModelCatalog.First(model => model.Id != profile.DefaultModel && profile.SuggestedModels.Contains(model.Id))
            : profile.ModelCatalog.Single(model => model.Id == profile.DefaultModel);
        var marker = "PP2C_WORKFLOW_" + Guid.NewGuid().ToString("N");
        var projectName = "PP2C workflow " + marker;
        var projectId = await fixture.PostAsync<Guid>("api/projects", new ProjectEditorModel { Name = projectName });
        var output = "# Accepted native output\n" + marker;
        var instructions = marker + ". Produce one bounded Markdown result.";
        var component = await fixture.PostAsync<LlmCallComponent>("api/workflows/components", new LlmCallComponentSaveRequest(null,
            "PP2C bounded model " + marker, profile.Id, alternate.Id, WorkflowModality.Text,
            new(null, 150, false, ""), instructions, WorkflowValueShape.Text, WorkflowValueShape.Text, AgentPermissionsPolicy.Default));
        WorkflowNode Node(string id, WorkflowNodeKind kind) => new(new(id), kind, id, [],
            new(null, null, null, null, "", WorkflowValueShape.Text, WorkflowValueShape.Text));
        var start = Node("start", WorkflowNodeKind.Start);
        var model = Node("model", WorkflowNodeKind.LlmCall) with {
            Settings = new(component.Id, null, null, null, instructions, WorkflowValueShape.Text, WorkflowValueShape.Text)
        };
        var asset = Node("asset", WorkflowNodeKind.Executor) with {
            Settings = new(null, null, null, null, "", WorkflowValueShape.Text, new(WorkflowValueShapeKind.Json, "{}", "Asset receipt")) {
                ExecutorId = WorkflowExecutorIds.ProjectStructure,
                ExecutorSettingsJson = WorkflowExecutorJson.Serialize(new WorkflowProjectStructureExecutorSettings {
                    Operation = WorkflowProjectStructureOperation.CreateAsset, ProjectId = projectId, NodeId = $"project:{projectId:D}",
                    Title = "PP2C Workflow output", AssetKind = "md", ContentFromInput = true, ContentType = "text/markdown"
                }),
                ExecutionPolicy = WorkflowExecutorExecutionPolicy.Default with { CaptureOutputArtifact = true, TimeoutSeconds = 45 }
            }
        };
        var end = Node("end", WorkflowNodeKind.End) with {
            Settings = new(null, null, null, null, "", new(WorkflowValueShapeKind.Json, "{}", "Asset receipt"), new(WorkflowValueShapeKind.Json, "{}", "Asset receipt"))
        };
        WorkflowNode[] nodes = [start, model, asset, end];
        var edges = nodes.Zip(nodes.Skip(1), (left, right) => new WorkflowEdge(new(left.Id.Value + "-" + right.Id.Value),
            left.Id, null, right.Id, null, WorkflowEdgeKind.Direct, "") { Routing = WorkflowEdgeRouting.Always }).ToArray();
        var initial = await fixture.PostAsync<WorkflowDefinition>("api/workflows/definitions", new WorkflowDefinitionSaveRequest(null, null,
            "PP2C Workflow " + marker, "Native shared model to governed project asset.", WorkflowLifecycleStatus.Draft,
            new(start.Id, nodes, edges), new(WorkflowRuntimeBackendKind.InProcess, true, false, false, false)));
        var page = fixture.Page;
        await fixture.NavigateAsync($"/agents/workflows?workflowId={initial.Id.Value:D}");
        await page.GetByTestId("workflows-tab-editor").ClickAsync();
        await page.GetByTestId("workflow-canvas-editor").WaitForAsync();
        if (!await page.GetByTestId("workflow-canvas-selection-window").IsVisibleAsync()) {
            await page.GetByTestId("workflow-canvas-toggle-selection").ClickAsync();
        }
        await page.GetByTestId("workflow-canvas-select-node").Filter(new() { HasTextString = "model" }).ClickAsync();
        await page.GetByTestId("workflow-canvas-open-selected-node-details").ClickAsync();
        await page.GetByTestId("workflow-canvas-node-modal-provider").SelectOptionAsync(profile.Id.ToString("D"));
        await page.GetByTestId("workflow-canvas-node-modal-model").SelectOptionAsync(new SelectOptionValue {
            Label = incomplete ? alternate.DisplayName : $"Provider default ({alternate.DisplayName})"
        });
        Assert.DoesNotContain("sp1.", await page.GetByTestId("workflow-canvas-node-modal-model").InnerTextAsync(), StringComparison.Ordinal);
        await Assertions.Expect(page.GetByTestId("workflow-canvas-node-modal-model-override")).ToHaveCountAsync(0);
        await page.GetByTestId("workflow-canvas-node-modal-close").ClickAsync();
        await page.GetByTestId("workflow-canvas-node-details-modal").WaitForAsync(new() { State = WaitForSelectorState.Detached });
        await page.GetByTestId("workflow-canvas-save").ClickAsync();
        await page.GetByText("Workflow saved", new() { Exact = true }).WaitForAsync();
        WorkflowDefinition? saved = null;
        using (var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30))) {
            while (saved is null || saved.VersionId == initial.VersionId) {
                await Task.Delay(100, deadline.Token);
                saved = (await fixture.Api.GetFromJsonAsync<WorkflowDefinitionDetail>($"api/workflows/definitions/{initial.Id.Value:D}", SharedProviderConsumerFixture.Json, deadline.Token))!.Definition;
            }
        }
        Assert.NotEqual(initial.VersionId, saved.VersionId);
        var savedModel = Assert.Single(saved.Graph.Nodes, node => node.Id.Value == "model");
        Assert.Equal(profile.Id, savedModel.Settings.ProviderProfileId);
        Assert.Equal(alternate.Id, savedModel.Settings.Model);
        await fixture.NavigateAsync($"/agents/workflows?workflowId={saved.Id.Value:D}");
        await page.GetByTestId("workflows-tab-history").ClickAsync();
        var input = JsonSerializer.Serialize(new { prompt = marker, projectId, nodeId = $"project:{projectId:D}" });
        await page.GetByTestId("workflows-test-input").FillAsync(input);
        await page.GetByTestId("workflows-test-input").PressAsync("Tab");
        await fixture.ScriptAsync(alternate.DisplayName, marker, new { kind = incomplete ? "incomplete_text" : "text", text = output });
        await page.GetByTestId("workflows-run-test").ClickAsync();
        await Assertions.Expect(page.GetByTestId("workflows-preview-project-id")).ToHaveValueAsync(projectId.ToString("D"));
        await Assertions.Expect(page.GetByTestId("workflows-preview-node-id")).ToHaveValueAsync($"project:{projectId:D}");
        foreach (var simulation in await page.Locator("input[data-testid^='workflows-preview-simulate-']").AllAsync()) {
            await Assertions.Expect(simulation).Not.ToBeCheckedAsync();
        }
        await page.GetByTestId("workflows-preview-input-run").ClickAsync();
        await Assertions.Expect(page.GetByTestId("workflows-test-result")).ToContainTextAsync(new Regex("Succeeded|Failed"), new() { Timeout = 120_000 });
        var runs = await fixture.GetAsync($"api/workflows/runs?workflowId={saved.Id.Value:D}");
        var run = Assert.Single(runs.EnumerateArray());
        var runId = run.GetProperty("runId").GetGuid();
        Assert.Equal(saved.VersionId.Value, run.GetProperty("versionId").GetGuid());
        Assert.Equal((int)(incomplete ? WorkflowRunState.Failed : WorkflowRunState.Completed), run.GetProperty("state").GetInt32());
        var detail = await fixture.GetAsync($"api/workflows/runs/{runId:D}/detail");
        var tree = await TreeAsync(fixture, projectId);
        var assets = tree.Nodes.Where(node => node.Title == "PP2C Workflow output").ToArray();
        if (incomplete) {
            Assert.Empty(assets);
            Assert.DoesNotContain(detail.GetProperty("events").EnumerateArray(), item => item.GetProperty("nodeId").GetString() == "asset" &&
                item.GetProperty("kind").GetInt32() == (int)WorkflowEventKind.ExecutorCompleted);
        } else {
            var created = Assert.Single(assets);
            Assert.Equal(output, await ContentAsync(fixture, projectId, created.Id));
            Assert.Equal($"project:{projectId:D}", created.ParentId);
            await AssertStoredAssetAsync(fixture, projectId, created);
            Assert.NotEmpty(detail.GetProperty("artifacts").EnumerateArray());
            await RecordTestLabAsync(fixture, projectId, runId, saved.VersionId.Value, created.Id, output, run.GetProperty("updatedAtUtc").GetDateTimeOffset());
        }
        await fixture.AssertScriptCompleteAsync(1);
        await AssertRoutedAsync(fixture, profile, alternate.Id, marker);
        await AssertCanonicalHistoryAsync(fixture, profile, alternate.Id, CanDoItAll.AgentFramework.ProviderHistory.HistorySourceKind.Workflow,
            runId, runId.ToString("D"), "workflow-" + (incomplete ? "incomplete" : "accepted"));
        await fixture.EvidenceAsync("consumer-workflow-" + (incomplete ? "incomplete" : "accepted"), new {
            Client = client, fixture.Address,
            saved.Id, saved.VersionId, RunId = runId, ProjectId = projectId, ProviderId = profile.Id, Route = alternate.Id,
            ModelSettings = component.ModelSettings, Input = input, Incomplete = incomplete,
            OutputSha256 = SharedProviderConsumerFixture.Hash(output), Assets = assets.Select(node => new { node.Id, node.MediaRelativePath }),
            detail
        });
    }

    private static async Task RecordTestLabAsync(SharedProviderConsumerFixture fixture, Guid projectId, Guid runId, Guid versionId,
        string nodeId, string output, DateTimeOffset timestamp) {
        var page = fixture.Page;
        await fixture.NavigateAsync($"/test-lab?projectId={projectId:D}");
        await Assertions.Expect(page.GetByTestId("testlab-workspace")).ToHaveAttributeAsync("data-interactive", "true");
        await page.GetByTestId("testlab-title-input").FillAsync("PP2C actual shared Workflow proof");
        await page.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("^Runs") }).ClickAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Add run", Exact = true }).ClickAsync();
        await page.GetByTestId("testlab-run-timestamp").FillAsync(timestamp.ToString("O"));
        await page.GetByTestId("testlab-run-runner").FillAsync("Native final-image Workflow with deterministic external model");
        await page.GetByTestId("testlab-run-result").SelectOptionAsync(nameof(TestCaseStatus.Passed));
        var summary = $"Workflow {runId:D}; version {versionId:D}; asset {nodeId}; SHA256 {SharedProviderConsumerFixture.Hash(output)}";
        await page.GetByTestId("testlab-run-summary").FillAsync(summary);
        await page.GetByTestId("testlab-save-button").ClickAsync();
        await Assertions.Expect(page.GetByTestId("testlab-save-state")).ToHaveAttributeAsync("data-state", "Saved");
        var planId = Guid.Parse((await page.GetByTestId("testlab-save-state").GetAttributeAsync("data-plan-id"))!);
        await fixture.NavigateAsync($"/test-lab?planId={planId:D}");
        await Assertions.Expect(page.GetByTestId("testlab-save-state")).ToHaveAttributeAsync("data-plan-id", planId.ToString("D"));
        await page.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("^Runs") }).ClickAsync();
        await Assertions.Expect(page.GetByTestId("testlab-run-summary")).ToHaveValueAsync(summary);
        await fixture.ScreenshotAsync("consumer-workflow-testlab");
        await fixture.EvidenceAsync("consumer-workflow-testlab", new { planId, projectId, runId, versionId, nodeId, Sha256 = SharedProviderConsumerFixture.Hash(output) });
    }
}
