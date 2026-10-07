using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Security;
using CanDoItAll.Modules.TestLab;
using CanDoItAll.Modules.Workbench;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;

using static CanDoItAll.Tests.Playwright.Smoke.AgentUiJourneySupport;
using static CanDoItAll.Tests.Playwright.Smoke.ProjectFilesUiJourney;

namespace CanDoItAll.Tests.Playwright.Smoke;

public sealed class WorkflowAssetUiTests {
    [Fact]
    [Trait("Category", "LiveAgent")]
    public async Task Workflow_UI_executes_real_model_and_preserves_generated_project_asset() {
        if (!IsLiveValidationEnabled()) {
            await UiEvidence.WriteNotRunAsync("ui-live-workflow-asset", "The dedicated live gates are closed.");
            return;
        }
        await WorkflowAssetJourneyAsync(live: true);
    }

    [Fact]
    [Trait("Category", "Playwright")]
    [Trait("Category", "HostPlatform")]
    public Task Workflow_UI_real_runtime_preserves_asset_with_only_external_model_scripted()
        => WorkflowAssetJourneyAsync(live: false);

    [Fact]
    [Trait("Category", "Playwright")]
    [Trait("Category", "HostPlatform")]
    public Task Workflow_UI_rejects_http_success_with_incomplete_model_output_before_creating_an_asset()
        => WorkflowAssetJourneyAsync(live: false, incomplete: true);

    private static async Task WorkflowAssetJourneyAsync(bool live, bool incomplete = false) {
        var evidence = new UiEvidence(live ? "ui-live-workflow-asset" : "ui-deterministic-workflow-asset") {
            Execution = live ? "live" : "deterministic-external-model"
        };
        var marker = "workflow-" + Guid.NewGuid().ToString("D");
        var output = "# Generated proof\n" + marker;
        await using var scripted = live ? null : await WorkflowResponseFixture.StartAsync(output, incomplete);
        await using var host = await LiveUiHost.StartAsync();
        try {
            Guid providerId;
            if (live) {
                var agent = await host.SeedAsync(SelectOrdinaryPlannerAsync);
                providerId = agent.ProviderProfileId!.Value;
                await evidence.DescribeProviderAsync(host, agent);
            } else {
                providerId = await host.SeedAsync(async services => {
                    var secret = await services.GetRequiredService<SecretService>().SaveAsync(new SecretEditorModel {
                        Name = "Synthetic Workflow fixture credential", Kind = SecretKind.ApiKey,
                        SecretValue = "local-fixture-credential", Scope = "workspace"
                    });
                    Assert.True(secret.IsSuccess);
                    return await services.GetRequiredService<IAgentFrameworkWorkspaceService>().SaveProviderAsync(new ProviderProfileEditorModel {
                        Name = "Scripted external Workflow provider", Kind = ProviderKind.OpenAi, Transport = ProviderTransportKind.Responses,
                        BaseUrl = scripted!.BaseUrl, ApiKeyEnvironmentVariable = $"secret:{secret.Value:D}",
                        DefaultModel = ManagedSeedProviderFallbacks.OpenAiDefaultModel,
                        SuggestedModels = [ManagedSeedProviderFallbacks.OpenAiDefaultModel], SupportsStreaming = false, SupportsTools = false,
                        ModelPrices = [new() { Model = ManagedSeedProviderFallbacks.OpenAiDefaultModel, InputPerMillionTokensUsd = 0,
                            OutputPerMillionTokensUsd = 0, TariffKind = ProviderTariffKind.ExplicitFree }]
                    });
                });
            }
            var page = await host.NewPageAsync();
            var oracle = CrmHrBrowserOracle.Attach(page);
            Guid projectId = await ProjectsPortfolioUiJourney.CreateAndEditAsync(host, page, oracle);
            var definition = await host.SeedAsync(async services => {
                var instructions = "Return exactly this Markdown, with no additional text or fences: " + output;
                var component = await services.GetRequiredService<IWorkflowComponentLibraryService>().SaveComponentAsync(new(null,
                    "Bounded file proof", providerId, ManagedSeedProviderFallbacks.OpenAiDefaultModel, WorkflowModality.Text,
                    new(null, 150, false, ""), instructions, WorkflowValueShape.Text, WorkflowValueShape.Text,
                    AgentPermissionsPolicy.Default));
                WorkflowNode Node(string id, WorkflowNodeKind kind) => new(new(id), kind, id, [],
                    new(null, null, null, null, "", WorkflowValueShape.Text, WorkflowValueShape.Text));
                var start = Node("start", WorkflowNodeKind.Start);
                var model = Node("model", WorkflowNodeKind.LlmCall) with {
                    Settings = new(component.Id, null, null, null, instructions, WorkflowValueShape.Text, WorkflowValueShape.Text)
                };
                var asset = Node("asset", WorkflowNodeKind.Executor) with { Settings = new(null, null, null, null, "",
                    WorkflowValueShape.Text, new(WorkflowValueShapeKind.Json, "{}", "Asset receipt")) {
                        ExecutorId = WorkflowExecutorIds.ProjectStructure,
                        ExecutorSettingsJson = WorkflowExecutorJson.Serialize(new WorkflowProjectStructureExecutorSettings {
                            Operation = WorkflowProjectStructureOperation.CreateAsset, ProjectId = projectId,
                            NodeId = $"project:{projectId:D}", Title = "Workflow generated proof", AssetKind = "md",
                            ContentFromInput = true, ContentType = "text/markdown"
                        }),
                        ExecutionPolicy = WorkflowExecutorExecutionPolicy.Default with { CaptureOutputArtifact = true, TimeoutSeconds = 45 }
                    }
                };
                var end = Node("end", WorkflowNodeKind.End) with { Settings = new(null, null, null, null, "",
                    new(WorkflowValueShapeKind.Json, "{}", "Asset receipt"), new(WorkflowValueShapeKind.Json, "{}", "Asset receipt")) };
                WorkflowNode[] nodes = [start, model, asset, end];
                var edges = nodes.Zip(nodes.Skip(1), (left, right) => new WorkflowEdge(new(left.Id.Value + "-" + right.Id.Value),
                    left.Id, null, right.Id, null, WorkflowEdgeKind.Direct, "") { Routing = WorkflowEdgeRouting.Always }).ToArray();
                var saved = await services.GetRequiredService<IWorkflowCatalogService>().SaveDefinitionAsync(new(null, null,
                    "Bounded Workflow proof " + marker, "Actual model to managed project asset.", WorkflowLifecycleStatus.Draft,
                    new(start.Id, nodes, edges), new(WorkflowRuntimeBackendKind.InProcess, true, false, false, false)));
                return saved;
            });
            evidence.Targets["workflowId"] = definition.Id.Value;
            evidence.Targets["versionId"] = definition.VersionId.Value;
            evidence.Targets["projectId"] = projectId;
            await oracle.NavigateAsync($"{host.BaseUrl}/agents/workflows?workflowId={definition.Id.Value:D}");
            await PlaywrightAppFixture.CompleteDatabaseStartupAsync(page);
            await page.WaitForFunctionAsync("() => typeof databaseSwitchListeners !== 'undefined' && databaseSwitchListeners.size === 1");
            await page.GetByTestId("workflows-tab-history").ClickAsync();
            var input = JsonSerializer.Serialize(new { prompt = "Generate the requested harmless marker.", marker, projectId, nodeId = $"project:{projectId:D}" });
            await page.GetByTestId("workflows-test-input").FillAsync(input);
            await page.GetByTestId("workflows-test-input").PressAsync("Tab");
            await Assertions.Expect(page.GetByTestId("workflows-run-test")).ToBeEnabledAsync();
            if (live && IsRehearsal()) {
                evidence.Execution = "rehearsal";
                evidence.Passed = true;
                return;
            }
            await page.GetByTestId("workflows-run-test").ClickAsync();
            await Assertions.Expect(page.GetByTestId("workflows-preview-project-id")).ToHaveValueAsync(projectId.ToString("D"));
            await Assertions.Expect(page.GetByTestId("workflows-preview-node-id")).ToHaveValueAsync($"project:{projectId:D}");
            var simulations = page.Locator("input[data-testid^='workflows-preview-simulate-']");
            foreach (var simulation in await simulations.AllAsync()) {
                await Assertions.Expect(simulation).Not.ToBeCheckedAsync();
            }
            await page.GetByTestId("workflows-preview-input-run").ClickAsync();
            await Assertions.Expect(page.GetByTestId("workflows-test-result")).ToContainTextAsync(
                new System.Text.RegularExpressions.Regex("Succeeded|Failed"), new() { Timeout = ModelTurnTimeoutMilliseconds });
            var terminalResult = await page.GetByTestId("workflows-test-result").InnerTextAsync();
            evidence.Observations["terminalResult"] = Sanitize(terminalResult);
            if (!terminalResult.Contains("Succeeded", StringComparison.Ordinal)) {
                await page.ScreenshotAsync(new() { Path = host.Artifact(live ? "workflow-live-failed.png" : "workflow-scripted-failed.png") });
            }
            if (incomplete) {
                Assert.Contains("Failed", terminalResult, StringComparison.Ordinal);
                await host.SeedAsync(async services => {
                    var store = services.GetRequiredService<IWorkflowRunStore>();
                    var failedRun = Assert.Single(await store.ListRunsAsync(definition.Id));
                    Assert.Equal(WorkflowRunState.Failed, failedRun.State);
                    Assert.Equal(definition.VersionId, failedRun.VersionId);
                    var tree = await services.GetRequiredService<ProjectStructureAgentService>().GetStructureAsync(projectId, new(IncludeAssets: true));
                    Assert.DoesNotContain(tree.Nodes, item => item.Title == "Workflow generated proof");
                    Assert.DoesNotContain(await store.ListEventsAsync(failedRun.RunId), item => item.Kind == WorkflowEventKind.ExecutorCompleted && item.NodeId?.Value == "asset");
                    evidence.Observations["incompleteResponse"] = new { failedRun.RunId, failedRun.State, httpStatus = 200,
                        reason = LiveProviderReason.OutputTokenLimit, assetsCreated = 0, maxOutputTokens = 150 };
                    return true;
                });
                Assert.Equal(1, scripted!.Requests);
                await oracle.AssertCleanAsync();
                evidence.Passed = true;
                return;
            }
            Assert.Contains("Succeeded", terminalResult, StringComparison.Ordinal);
            var (run, events, artifacts, node, content) = await host.SeedAsync(async services => {
                var store = services.GetRequiredService<IWorkflowRunStore>();
                var run = Assert.Single(await store.ListRunsAsync(definition.Id));
                var structure = services.GetRequiredService<ProjectStructureAgentService>();
                var tree = await structure.GetStructureAsync(projectId, new(IncludeAssets: true));
                var node = Assert.Single(tree.Nodes, item => item.Title == "Workflow generated proof");
                return (run, await store.ListEventsAsync(run.RunId), await store.ListArtifactsAsync(run.RunId), node,
                    await structure.GetAssetContentAsync(projectId, node.Id));
            });
            Assert.Equal(definition.VersionId, run.VersionId);
            Assert.Equal(WorkflowRunState.Completed, run.State);
            var origin = Assert.IsType<WorkflowLaunchOrigin.Preview>(run.Origin);
            Assert.NotNull(origin.StructureAuthority);
            Assert.Equal($"project:{projectId:D}", node.ParentId);
            var bytes = Convert.FromBase64String(content.Base64Data);
            Assert.Equal(output, Encoding.UTF8.GetString(bytes));
            Assert.Contains(events, item => item.Kind == WorkflowEventKind.ExecutorCompleted && item.NodeId?.Value == "model");
            Assert.Contains(events, item => item.Kind == WorkflowEventKind.ExecutorCompleted && item.NodeId?.Value == "asset");
            Assert.NotEmpty(artifacts);
            evidence.Observations["workflow"] = new { run.RunId, run.WorkflowId, run.VersionId, run.State,
                originKind = origin.GetType().Name, submittedInput = input, node.Id, node.ParentId, node.ArtifactId,
                byteLength = bytes.Length, sha256 = Convert.ToHexString(SHA256.HashData(bytes)),
                events = events.Select(item => new { item.Id, item.Kind, item.NodeId }), artifacts = artifacts.Select(item => item.Id),
                externalBoundaryScripted = !live, scriptedRequests = scripted?.Requests ?? 0 };
            if (!await page.GetByTestId("workflows-run-detail-dialog").IsVisibleAsync()) {
                await page.GetByTestId("workflows-run-detail").First.ClickAsync();
            }
            await Assertions.Expect(page.GetByTestId("workflows-run-detail-event")).Not.ToHaveCountAsync(0);
            await page.ScreenshotAsync(new() { Path = host.Artifact(live ? "workflow-live-run.png" : "workflow-scripted-run.png") });
            await oracle.NavigateAsync($"{host.BaseUrl}/projects/{projectId:D}/structure");
            await ReadyFileCanvasAsync(page);
            await SelectFileNodeAsync(page, node.Id, node.Title);
            await page.GetByTestId("project-structure-selection-window").GetByRole(AriaRole.Button,
                new() { Name = "Expand preview", Exact = true }).ClickAsync();
            var preview = page.GetByRole(AriaRole.Dialog, new() { NameRegex = new System.Text.RegularExpressions.Regex("file interaction$") });
            await Assertions.Expect(preview).ToContainTextAsync(marker);
            await page.ScreenshotAsync(new() { Path = host.Artifact(live ? "workflow-live-preview.png" : "workflow-scripted-preview.png") });
            await ProjectsFilesBrowserProof.ReopenProducedAssetAsync(host, page, oracle, projectId,
                node.MediaRelativePath!, output, "workflow-produced-file");
            if (!live) {
                Assert.Equal(1, scripted!.Requests);
                await oracle.NavigateAsync($"{host.BaseUrl}/test-lab?projectId={projectId:D}");
                await Assertions.Expect(page.GetByTestId("testlab-workspace")).ToHaveAttributeAsync("data-interactive", "true");
                await page.GetByTestId("testlab-title-input").FillAsync("Recorded actual Workflow proof");
                await page.GetByRole(AriaRole.Button, new() { NameRegex = new System.Text.RegularExpressions.Regex("^Runs") }).ClickAsync();
                await page.GetByRole(AriaRole.Button, new() { Name = "Add run", Exact = true }).ClickAsync();
                await page.GetByTestId("testlab-run-timestamp").FillAsync(run.UpdatedAtUtc.ToString("O"));
                await page.GetByTestId("testlab-run-runner").FillAsync("Actual Workflow runtime with scripted external model");
                await page.GetByTestId("testlab-run-result").SelectOptionAsync(nameof(TestCaseStatus.Passed));
                var summary = $"Workflow {run.RunId.Value:D}; version {run.VersionId.Value:D}; asset {node.Id}; SHA256 {Convert.ToHexString(SHA256.HashData(bytes))}";
                await page.GetByTestId("testlab-run-summary").FillAsync(summary);
                await page.GetByTestId("testlab-save-button").ClickAsync();
                await Assertions.Expect(page.GetByTestId("testlab-save-state")).ToHaveAttributeAsync("data-state", "Saved");
                var planId = Guid.Parse((await page.GetByTestId("testlab-save-state").GetAttributeAsync("data-plan-id"))!);
                var plan = await host.SeedAsync(services => services.GetRequiredService<TestLabService>().GetAsync(planId));
                Assert.Equal(projectId, plan.ProjectId);
                Assert.Equal(summary, Assert.Single(plan.Runs).Summary);
                Assert.Equal(TestCaseStatus.Passed, plan.Runs[0].Result);
                await oracle.NavigateAsync($"{host.BaseUrl}/test-lab?planId={planId:D}");
                await Assertions.Expect(page.GetByTestId("testlab-workspace")).ToHaveAttributeAsync("data-interactive", "true");
                await Assertions.Expect(page.GetByTestId("testlab-save-state")).ToHaveAttributeAsync("data-plan-id", planId.ToString("D"));
                await page.GetByRole(AriaRole.Button, new() { NameRegex = new System.Text.RegularExpressions.Regex("^Runs") }).ClickAsync();
                await Assertions.Expect(page.GetByTestId("testlab-run-summary")).ToHaveValueAsync(summary);
                await page.ScreenshotAsync(new() { Path = host.Artifact("testlab-actual-workflow-record.png") });
                evidence.Observations["testLab"] = new { planId, plan.ProjectId, recordedRun = plan.Runs[0].Id, workflowRun = run.RunId, summary };
            }
            await oracle.AssertCleanAsync();
            evidence.Passed = true;
        } finally {
            if (evidence.Targets.TryGetValue("workflowId", out var target) && target is Guid workflowId) {
                evidence.Observations["workflowAttempts"] = await host.SeedAsync(async services =>
                    (await services.GetRequiredService<IWorkflowRunStore>().ListRunsAsync(new(workflowId)))
                    .Select(run => new { run.RunId, run.WorkflowId, run.VersionId, run.State, run.CreatedAtUtc, run.UpdatedAtUtc }).ToArray());
            }
            await evidence.WriteAsync(host);
        }
    }
}
