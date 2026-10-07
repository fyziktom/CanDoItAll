using System.Net.Http.Json;
using System.Text.Json;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Prompts;
using CanDoItAll.Modules.Workspace.ApiAccess;
using Microsoft.Playwright;

namespace CanDoItAll.Tests.Playwright;

public sealed partial class SharedProviderNativeConsumerTests {
    [Fact]
    [Trait("Category", "ExternalSharedProviderUi")]
    public async Task Final_image_canvas_gestures_save_native_graph_and_execute_the_original_Gallery_version() {
        await using var fixture = await SharedProviderConsumerFixture.StartAsync(ApiAccessScopeNames.ReadPrompts, ApiAccessScopeNames.WritePrompts);
        var profile = await ImportedResponsesAsync(fixture);
        var marker = "WF1_PINNED_" + Guid.NewGuid().ToString("N");
        var prompt = new PromptGalleryDraft(null, null, null, marker, "WF1 immutable canvas binding",
            PromptGalleryItemKind.FullPrompt, "workflow", marker + ": return the original pinned result.",
            SupportedConsumers: [PromptGalleryConsumer.Workflow]);
        var draft = await fixture.PostAsync<PromptDraftSaveReceipt>("api/prompt-gallery/items", prompt);
        var version = await fixture.PostAsync<PromptVersionSnapshot>($"api/prompt-gallery/items/{draft.PromptArtifactId:D}/versions",
            new PromptVersionCreateRequest("WF1 original", draft.UpdatedAtUtc));
        var page = fixture.Page;
        await fixture.NavigateAsync("/agents/workflows");
        await page.GetByTestId("workflows-tab-editor").ClickAsync();
        await page.WaitForFunctionAsync("""
            () => {
                const h = document.querySelector('.cw-canvas-host');
                return h && window.CanDoItAll?.canvasWorkbench?.getSceneSnapshot(h)?.dependencySourceId;
            }
            """);
        var originalCanvas = await page.Locator(".cw-canvas-host").EvaluateAsync<string>(
            "h => window.CanDoItAll.canvasWorkbench.getSceneSnapshot(h).dependencySourceId");
        await page.GetByTestId("workflow-canvas-new-draft").ClickAsync();
        await page.WaitForFunctionAsync("""
            original => {
                const h = document.querySelector('.cw-canvas-host');
                const scene = h && window.CanDoItAll.canvasWorkbench.getSceneSnapshot(h);
                return scene && scene.dependencySourceId !== original && scene.minimap.nodeCount === 2;
            }
            """, originalCanvas);
        await page.GetByTestId("workflow-canvas-name").FillAsync(marker);
        if (!await page.GetByTestId("workflow-toolbox-node-StrictLogic").IsVisibleAsync()) {
            await page.GetByTestId("workflow-toolbox-group-workflow-nodes").ClickAsync();
        }
        await AddCanvasNodeAsync(page, WorkflowNodeKind.StrictLogic, "WF1 deterministic");
        await page.GetByTestId("workflow-canvas-toggle-components").ClickAsync();
        var components = page.GetByTestId("workflow-canvas-components-window");
        await components.GetByTestId("workflow-canvas-component-provider").SelectOptionAsync(profile.Id.ToString("D"));
        await components.GetByTestId("workflow-canvas-component-model").SelectOptionAsync(new SelectOptionValue {
            Label = $"Provider default ({profile.GetModelDisplayName(profile.DefaultModel)})"
        });
        await components.GetByTestId("prompt-gallery-picker-button").ClickAsync();
        var picker = page.GetByTestId("prompt-gallery-picker-dialog");
        await picker.GetByTestId("prompt-gallery-search").FillAsync(marker);
        await Assertions.Expect(picker.GetByTestId("prompt-gallery-select")).ToHaveCountAsync(1);
        await picker.GetByTestId("prompt-gallery-select").ClickAsync();
        await picker.WaitForAsync(new() { State = WaitForSelectorState.Detached });
        await Assertions.Expect(page.GetByTestId("workflow-canvas-select-node").Filter(new() { HasTextString = marker })).ToHaveCountAsync(1);
        var component = Assert.Single((await fixture.Api.GetFromJsonAsync<LlmCallComponent[]>("api/workflows/components", SharedProviderConsumerFixture.Json))!,
            item => item.PromptArtifactId == draft.PromptArtifactId);
        Assert.Equal(version.PromptVersionId, component.PromptVersionId);
        Assert.Equal(profile.DefaultModel, component.Model);
        Assert.Equal(version.Content, component.Instructions);
        await page.GetByTestId("workflow-canvas-toggle-components").ClickAsync();
        await AddCanvasNodeAsync(page, WorkflowNodeKind.Artifact, "WF1 temporary");
        await page.GetByTestId("workflow-canvas-tab-node").ClickAsync();
        await page.GetByTestId("workflow-canvas-remove-node").ClickAsync();
        await Assertions.Expect(page.GetByTestId("workflow-canvas-select-node")).ToHaveCountAsync(4);
        await page.GetByTestId("workflow-canvas-select-node").Filter(new() { HasTextString = "Start" }).ClickAsync();
        await Assertions.Expect(page.GetByTestId("workflow-canvas-remove-node")).ToBeDisabledAsync();
        await page.GetByTestId("workflow-canvas-tab-routes").ClickAsync();
        var entry = page.GetByTestId("workflow-canvas-edge-row").Filter(new() { HasTextString = "Start -> WF1 deterministic" });
        await entry.GetByTestId("workflow-canvas-edit-edge").ClickAsync();
        await page.GetByTestId("workflow-canvas-edge-route-label").FillAsync("WF1 authored entry");
        await page.GetByTestId("workflow-canvas-add-edge").ClickAsync();
        await Assertions.Expect(entry).ToContainTextAsync("WF1 authored entry");
        await entry.GetByTestId("workflow-canvas-remove-edge").ClickAsync();
        await Assertions.Expect(page.GetByTestId("workflow-canvas-edge-row")).ToHaveCountAsync(2);
        await page.GetByTestId("workflow-canvas-toggle-toolbox").ClickAsync();
        await page.GetByTestId("workflow-canvas-toggle-selection").ClickAsync();
        var host = page.Locator(".cw-canvas-host");
        await page.GetByRole(AriaRole.Button, new() { Name = "Fit canvas", Exact = true }).ClickAsync();
        await page.WaitForFunctionAsync("""
            () => {
                const h = document.querySelector('.cw-canvas-host');
                const n = window.CanDoItAll.canvasWorkbench.getSceneSnapshot(h)?.nodes.find(n => n.id === 'logic');
                return n && n.left > 0 && n.top > 0 && n.right < h.clientWidth && n.bottom < h.clientHeight;
            }
            """);
        await WaitForCanvasSettledAsync(host);
        var ports = (await host.EvaluateAsync<JsonElement>("""
            h => {
                const r = h.getBoundingClientRect();
                const scene = window.CanDoItAll.canvasWorkbench.getSceneSnapshot(h);
                return ['start', 'logic'].map(id => {
                    const ports = scene.hotZones.filter(p => p.nodeId === id && p.type === 'node-port').sort((a, b) => a.bounds.x - b.bounds.x);
                    const p = (id === 'start' ? ports.at(-1) : ports[0]).bounds;
                    const x = p.x + p.width / 2, y = p.y + p.height / 2;
                    return { x: r.x + x, y: r.y + y, left: x, top: y };
                });
            }
            """)).Deserialize<CanvasNodePosition[]>(SharedProviderConsumerFixture.Json)!;
        await page.Mouse.ClickAsync(ports[0].X, ports[0].Y);
        await page.Mouse.MoveAsync(ports[1].X, ports[1].Y, new() { Steps = 12 });
        await page.Mouse.ClickAsync(ports[1].X, ports[1].Y);
        await Assertions.Expect(page.GetByTestId("workflow-canvas-edge-row")).ToHaveCountAsync(3);
        await entry.GetByTestId("workflow-canvas-edit-edge").ClickAsync();
        await page.GetByTestId("workflow-canvas-edge-route-label").FillAsync("WF1 authored entry");
        await page.GetByTestId("workflow-canvas-add-edge").ClickAsync();
        await Assertions.Expect(entry).ToContainTextAsync("WF1 authored entry");
        await page.GetByTestId("workflow-canvas-save").ClickAsync();
        await page.GetByText("Workflow saved", new() { Exact = true }).WaitForAsync();
        var beforeCatalog = (await fixture.Api.GetFromJsonAsync<WorkflowCatalogItem[]>("api/workflows/definitions", SharedProviderConsumerFixture.Json))!;
        var beforeDrag = await ReadNativeDefinitionAsync(fixture, Assert.Single(beforeCatalog, item => item.Name == marker).Id);
        var initialLogic = Assert.Single(beforeDrag.Graph.Nodes, node => node.Id.Value == "logic");
        await page.GetByText("Workflow saved", new() { Exact = true }).WaitForAsync(new() { State = WaitForSelectorState.Detached });
        await page.GetByRole(AriaRole.Button, new() { Name = "Fit canvas", Exact = true }).ClickAsync();
        await WaitForCanvasSettledAsync(host);
        var before = await CanvasNodePositionAsync(host, "logic");
        var previousPosition = await host.EvaluateAsync<string>("h => JSON.stringify(JSON.parse(window.CanDoItAll.canvasWorkbench.getState(h)).manualPositions.logic ?? null)");
        await page.WaitForFunctionAsync("p => document.elementFromPoint(p.x,p.y)?.closest('.cw-canvas-host')",
            new { x = (double)before.X, y = (double)before.Y });
        await fixture.EvidenceAsync("wf1-drag-before", new { before, previousPosition });
        await fixture.EvidenceAsync("wf1-drag-target", await page.EvaluateAsync<JsonElement>(
            "p => ({ x:p.x, y:p.y, targets:document.elementsFromPoint(p.x,p.y).slice(0,5).map(e=>({tag:e.tagName,class:e.className})), width:innerWidth, height:innerHeight })", new { x = (double)before.X, y = (double)before.Y }));
        await page.Mouse.MoveAsync(before.X, before.Y);
        await page.Mouse.DownAsync();
        await page.Mouse.MoveAsync(before.X + 70, before.Y + 35, new() { Steps = 12 });
        await page.Mouse.UpAsync();
        await fixture.EvidenceAsync("wf1-drag-after", await host.EvaluateAsync<JsonElement>(
            "h => ({state:JSON.parse(window.CanDoItAll.canvasWorkbench.getState(h)),scene:window.CanDoItAll.canvasWorkbench.getSceneSnapshot(h)})"));
        await page.WaitForFunctionAsync("""
            previous => {
                const h = document.querySelector('.cw-canvas-host');
                const node = window.CanDoItAll.canvasWorkbench.getSceneSnapshot(h).nodes.find(n => n.id === 'logic');
                return node && node.left > previous + 45;
            }
            """, before.Left);
        var after = await CanvasNodePositionAsync(host, "logic");
        await page.GetByTestId("workflow-canvas-save").ClickAsync();
        await page.GetByText("Workflow saved", new() { Exact = true }).WaitForAsync();
        var catalog = (await fixture.Api.GetFromJsonAsync<WorkflowCatalogItem[]>("api/workflows/definitions", SharedProviderConsumerFixture.Json))!;
        var accepted = Assert.Single(catalog, item => item.Name == marker);
        var authored = await ReadNativeDefinitionAsync(fixture, accepted.Id);
        Assert.Equal(beforeDrag.Id, authored.Id);
        Assert.NotEqual(beforeDrag.VersionId, authored.VersionId);
        Assert.Equal(4, authored.Graph.Nodes.Count);
        Assert.Equal(3, authored.Graph.Edges.Count);
        var logic = Assert.Single(authored.Graph.Nodes, node => node.Id.Value == "logic");
        var llm = Assert.Single(authored.Graph.Nodes, node => node.Kind == WorkflowNodeKind.LlmCall);
        Assert.Equal(component.Id, llm.Settings.ComponentId);
        Assert.NotEqual(initialLogic.CanvasX, logic.CanvasX);
        Assert.NotEqual(initialLogic.CanvasY, logic.CanvasY);
        Assert.Contains(authored.Graph.Edges, edge => edge.SourceNodeId == llm.Id && edge.TargetNodeId.Value == "end");
        Assert.Contains(authored.Graph.Edges, edge => edge.Routing.Label == "WF1 authored entry");
        await fixture.NavigateAsync($"/agents/workflows?workflowId={authored.Id.Value:D}");
        await page.GetByTestId("workflows-tab-editor").ClickAsync();
        await Assertions.Expect(page.GetByTestId("workflow-canvas-name")).ToHaveValueAsync(marker);
        var fresh = await ReadNativeDefinitionAsync(fixture, authored.Id);
        Assert.Equal(JsonSerializer.Serialize(authored.Graph), JsonSerializer.Serialize(fresh.Graph));
        await fixture.ScreenshotAsync("wf1-native-authored-graph-1920");
        await fixture.EvidenceAsync("wf1-native-graph-gestures", new { authored.Id, authored.VersionId, authored.Graph, Before = before, After = after });

        var currentPrompt = (await fixture.Api.GetFromJsonAsync<PromptGalleryItemDetails>(
            $"api/prompt-gallery/items/{draft.PromptArtifactId:D}", SharedProviderConsumerFixture.Json))!;
        var changed = await fixture.PostAsync<PromptDraftSaveReceipt>("api/prompt-gallery/items", prompt with {
            Id = draft.PromptArtifactId, Content = "WF1 successor head must never execute for the pinned component.", ExpectedUpdatedAtUtc = currentPrompt.UpdatedAtUtc
        });
        var successor = await fixture.PostAsync<PromptVersionSnapshot>($"api/prompt-gallery/items/{draft.PromptArtifactId:D}/versions",
            new PromptVersionCreateRequest("WF1 successor", changed.UpdatedAtUtc));
        Assert.NotEqual(version.PromptVersionId, successor.PromptVersionId);
        await page.GetByTestId("workflow-canvas-select-node").Filter(new() { HasTextString = "WF1 deterministic" }).ClickAsync();
        await page.GetByTestId("workflow-canvas-tab-node").ClickAsync();
        await page.GetByTestId("workflow-canvas-remove-node").ClickAsync();
        await Assertions.Expect(page.GetByTestId("workflow-canvas-select-node")).ToHaveCountAsync(3);
        await page.GetByTestId("workflow-canvas-save").ClickAsync();
        await page.GetByText("Workflow saved", new() { Exact = true }).WaitForAsync();
        var executable = await ReadNativeDefinitionAsync(fixture, authored.Id);
        Assert.NotEqual(authored.VersionId, executable.VersionId);
        var output = "WF1 original pinned result " + marker;
        await fixture.ScriptAsync(profile.GetModelDisplayName(profile.DefaultModel), marker, new { kind = "text", text = output });
        await page.GetByTestId("workflows-tab-history").ClickAsync();
        await page.GetByTestId("workflows-test-input").FillAsync("{\"message\":\"WF1 pinned execution\"}");
        await page.GetByTestId("workflows-test-input").PressAsync("Tab");
        await page.GetByTestId("workflows-run-test").ClickAsync();
        var completed = await AwaitWorkflowStateAsync(fixture, executable, WorkflowRunState.Completed);
        var runId = completed.GetProperty("runId").GetGuid();
        var detail = await fixture.GetAsync($"api/workflows/runs/{runId:D}/detail");
        Assert.Equal(runId, detail.GetProperty("run").GetProperty("runId").GetGuid());
        await Assertions.Expect(page.GetByTestId("workflows-run-detail-dialog")).ToContainTextAsync(output);
        await fixture.AssertScriptCompleteAsync(1);
        var captures = await fixture.ReadCapturesAsync();
        var call = Assert.Single(captures.GetProperty("requests").EnumerateArray(), item => item.GetProperty("body").GetString()!.Contains(marker, StringComparison.Ordinal));
        Assert.Contains(version.Content, call.GetProperty("body").GetString(), StringComparison.Ordinal);
        Assert.DoesNotContain(successor.Content, call.GetProperty("body").GetString(), StringComparison.Ordinal);
        var retained = (await fixture.Api.GetFromJsonAsync<LlmCallComponent>($"api/workflows/components/{component.Id.Value:D}", SharedProviderConsumerFixture.Json))!;
        Assert.Equal(version.PromptVersionId, retained.PromptVersionId);
        await fixture.EvidenceAsync("wf1-native-pinned-prompt", new {
            executable.Id, executable.VersionId, RunId = runId, ComponentId = component.Id, PromptId = draft.PromptArtifactId,
            OriginalVersion = version.PromptVersionId, SuccessorVersion = successor.PromptVersionId,
            SourceModel = profile.GetModelDisplayName(profile.DefaultModel), Route = profile.DefaultModel,
            OutputSha256 = SharedProviderConsumerFixture.Hash(output), OriginalInstructions = version.Content
        });
    }

    private static async Task AddCanvasNodeAsync(IPage page, WorkflowNodeKind kind, string name) {
        await page.GetByTestId("workflow-toolbox-node-" + kind).ClickAsync();
        var composer = page.Locator(".cw-canvas-composer__scroll");
        await composer.Locator("input").First.FillAsync(name);
        await composer.Locator("select").Nth(1).SelectOptionAsync(nameof(WorkflowValueShapeKind.Text));
        await page.GetByRole(AriaRole.Button, new() { Name = "Add node", Exact = true }).ClickAsync();
        await page.GetByTestId("workflow-canvas-select-node").Filter(new() { HasTextString = name }).WaitForAsync();
    }

    private static async Task<WorkflowDefinition> ReadNativeDefinitionAsync(SharedProviderConsumerFixture fixture, WorkflowId id) =>
        (await fixture.Api.GetFromJsonAsync<WorkflowDefinitionDetail>($"api/workflows/definitions/{id.Value:D}", SharedProviderConsumerFixture.Json))!.Definition;

    private static Task WaitForCanvasSettledAsync(ILocator host) => host.EvaluateAsync("""
            h => new Promise((resolve, reject) => {
                let previous = '', stable = 0;
                const deadline = performance.now() + 5000;
                function observe() {
                    const current = JSON.stringify({ state: window.CanDoItAll.canvasWorkbench.getState(h), scene: window.CanDoItAll.canvasWorkbench.getSceneSnapshot(h).nodes });
                    stable = current === previous ? stable + 1 : 0;
                    previous = current;
                    if (stable >= 3) {
                        resolve();
                    } else if (performance.now() >= deadline) {
                        reject(new Error('Canvas viewport did not settle.'));
                    } else {
                        requestAnimationFrame(observe);
                    }
                }
                requestAnimationFrame(observe);
            })
            """);

    private sealed record CanvasNodePosition(float X, float Y, double Left, double Top);

    private static async Task<CanvasNodePosition> CanvasNodePositionAsync(ILocator host, string nodeId) => (await host.EvaluateAsync<JsonElement>("""
        (h, id) => {
            const n = window.CanDoItAll.canvasWorkbench.getSceneSnapshot(h).nodes.find(n => n.id === id);
            const r = h.getBoundingClientRect();
            return { x: r.x + n.left + n.width / 2, y: r.y + n.top + 35, left: n.left, top: n.top };
        }
        """, nodeId)).Deserialize<CanvasNodePosition>(SharedProviderConsumerFixture.Json)!;
}
