using System.Diagnostics;
using System.Text.Json;
using CanDoItAll.Modules.Processes;
using Microsoft.Playwright;

namespace CanDoItAll.Tests.Playwright;

[Collection(WebGlSandboxCollection.Name)]
public sealed class WebGlSandboxSmokeTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly WebGlSandboxPlaywrightFixture fixture;

    public WebGlSandboxSmokeTests(WebGlSandboxPlaywrightFixture fixture)
    {
        this.fixture = fixture;
    }

    [Fact]
    [Trait("Surface", "WebGlSandbox")]
    public async Task Sandbox_renders_default_template_and_switches_to_dense_scene()
    {
        var artifactsDir = EnsureArtifactsDirectory();

        await using var context = await fixture.Browser.NewContextAsync(new BrowserNewContextOptions
        {
            IgnoreHTTPSErrors = true,
            ViewportSize = new ViewportSize
            {
                Width = 1900,
                Height = 1200
            }
        });
        var page = await context.NewPageAsync();

        var response = await page.GotoAsync($"{fixture.BaseUrl}/webgl/process-workbench");
        Assert.NotNull(response);
        Assert.True(response!.Ok, $"Expected sandbox route to return 2xx, got {(int)response.Status}.");

        var initialScene = await WaitForSnapshotAsync(
            page,
            snapshot => snapshot.Nodes.Count > 0 && snapshot.Edges.Count > 0,
            "default representative template");

        Assert.Equal("customer-onboarding", initialScene.SceneKey);
        Assert.True(initialScene.DeterministicMode);
        Assert.Equal("perspective", initialScene.ProjectionMode);
        await AssertStageBoundsAsync(page, 900, 600);

        await page.Locator("[data-testid='webgl-sandbox-stage']").ScreenshotAsync(new LocatorScreenshotOptions
        {
            Path = Path.Combine(artifactsDir, "01-webgl-default-template.png")
        });

        await page.GetByLabel("Template").SelectOptionAsync(
        [
            new SelectOptionValue
            {
                Label = "Multi-team software delivery and release governance"
            }
        ]);

        var softwareDeliveryScene = await WaitForSnapshotAsync(
            page,
            snapshot =>
                string.Equals(snapshot.SceneKey, "software-delivery", StringComparison.Ordinal) &&
                snapshot.Nodes.Count > initialScene.Nodes.Count &&
                snapshot.Edges.Count > initialScene.Edges.Count,
            "software delivery representative template");

        Assert.True(softwareDeliveryScene.Nodes.Count >= 12);

        await page.GetByLabel("Template").SelectOptionAsync(
        [
            new SelectOptionValue
            {
                Label = "Branching code review and merge governance"
            }
        ]);

        var denseScene = await WaitForSnapshotAsync(
            page,
            snapshot =>
                string.Equals(snapshot.SceneKey, "branching-code-review", StringComparison.Ordinal) &&
                snapshot.Nodes.Count >= softwareDeliveryScene.Nodes.Count &&
                snapshot.Edges.Count >= softwareDeliveryScene.Edges.Count,
            "dense representative template");

        Assert.Contains(denseScene.Nodes, node => node.Kind.Contains("role", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(denseScene.Nodes, node => node.Kind.Contains("branch", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(denseScene.Edges, edge => edge.IsPrimaryPath && edge.Emphasis > 1.4d);
        Assert.Contains(denseScene.Edges, edge => !edge.IsPrimaryPath && edge.Emphasis < 1d);
        await AssertStageBoundsAsync(page, 900, 600);

        await page.Locator("[data-testid='webgl-sandbox-stage']").ScreenshotAsync(new LocatorScreenshotOptions
        {
            Path = Path.Combine(artifactsDir, "02-webgl-dense-template.png")
        });

        Assert.False(await page.Locator("#blazor-error-ui").IsVisibleAsync());
    }

    [Fact]
    [Trait("Surface", "WebGlSandbox")]
    public async Task Sandbox_supports_drag_connection_and_export_without_camera_reset()
    {
        var artifactsDir = EnsureArtifactsDirectory();

        await using var context = await fixture.Browser.NewContextAsync(new BrowserNewContextOptions
        {
            IgnoreHTTPSErrors = true,
            ViewportSize = new ViewportSize
            {
                Width = 1900,
                Height = 1200
            }
        });
        var page = await context.NewPageAsync();

        var response = await page.GotoAsync($"{fixture.BaseUrl}/webgl/process-workbench?template=branching-code-review");
        Assert.NotNull(response);
        Assert.True(response!.Ok, $"Expected sandbox route to return 2xx, got {(int)response.Status}.");

        var initialScene = await WaitForSnapshotAsync(
            page,
            snapshot => string.Equals(snapshot.SceneKey, "branching-code-review", StringComparison.Ordinal) && snapshot.Nodes.Count > 0,
            "branching template");
        await AssertStageBoundsAsync(page, 900, 600);
        var targetNode = initialScene.Nodes
            .Where(node => !node.Kind.Contains("branch", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(node => ResolveSceneClearance(initialScene, node))
            .First();
        var focused = await page.EvaluateAsync<bool>(
            @"nodeId => {
                const host = document.querySelector('.wgl-workbench-host');
                const runtime = window.CanDoItAll?.webglWorkbench;
                if (!host || !runtime?.focusNode) {
                    return false;
                }

                runtime.focusNode(host, nodeId);
                return true;
            }",
            targetNode.Id);
        Assert.True(focused, $"Expected focusNode to be available for '{targetNode.Id}'.");

        var focusedState = await WaitForUiStateAsync(
            page,
            state =>
                Math.Abs(state.Camera.TargetX - targetNode.X) < 1 &&
                Math.Abs(state.Camera.TargetY + targetNode.Y) < 1 &&
                Math.Abs(state.Camera.TargetZ - targetNode.Z) < 1,
            "focusNode to move the camera target");
        var dragPlan = await ResolveSupportedDragAsync(page, targetNode);
        Assert.True(dragPlan.Accepted, $"Expected simulateDrag to accept a collision-safe drag vector for '{targetNode.Id}'.");

        var afterDrag = await WaitForSnapshotAsync(
            page,
            snapshot =>
            {
                var movedNode = snapshot.Nodes.FirstOrDefault(node => string.Equals(node.Id, targetNode.Id, StringComparison.Ordinal));
                return movedNode is not null &&
                    (Math.Abs(movedNode.X - targetNode.X) > 1 || Math.Abs(movedNode.Y - targetNode.Y) > 1);
            },
            "dragged node to persist through rerender");
        var afterDragState = await ReadUiStateAsync(page);
        var movedNode = afterDrag.Nodes.First(node => string.Equals(node.Id, targetNode.Id, StringComparison.Ordinal));

        Assert.True(Math.Abs(afterDragState.Camera.TargetX - focusedState.Camera.TargetX) < 0.1);
        Assert.True(Math.Abs(afterDragState.Camera.TargetY - focusedState.Camera.TargetY) < 0.1);
        Assert.True(Math.Abs(afterDragState.Camera.TargetZ - focusedState.Camera.TargetZ) < 0.1);
        Assert.True(Math.Abs(afterDragState.Camera.Zoom - focusedState.Camera.Zoom) < 0.01);
        Assert.True(Math.Abs(afterDragState.Camera.Distance - focusedState.Camera.Distance) < 0.5);
        Assert.True(Math.Abs(afterDragState.Camera.Azimuth - focusedState.Camera.Azimuth) < 0.01);
        Assert.True(Math.Abs(afterDragState.Camera.Polar - focusedState.Camera.Polar) < 0.01);
        Assert.True(
            Math.Abs(movedNode.X - targetNode.X) > 1 || Math.Abs(movedNode.Y - targetNode.Y) > 1,
            $"Expected node '{targetNode.Id}' to move after the accepted collision-safe drag.");

        var edge = afterDrag.Edges.First(edgeCandidate =>
            (ProcessCanvasCatalog.DefinitionPorts.IsStepStructuralOutputPortId(edgeCandidate.SourcePortId) &&
                ProcessCanvasCatalog.DefinitionPorts.IsStepStructuralInputPortId(edgeCandidate.TargetPortId)) ||
            string.Equals(edgeCandidate.CategoryKey, ProcessCanvasCatalog.ConnectionCategories.BranchRoute, StringComparison.Ordinal));

        var disconnected = await page.EvaluateAsync<bool>(
            @"request => {
                const host = document.querySelector('.wgl-workbench-host');
                const runtime = window.CanDoItAll?.webglWorkbench;
                return !!runtime?.simulateConnection(host, request);
            }",
            new
            {
                actionId = "disconnect",
                edgeId = edge.Id,
                sourceNodeId = edge.SourceNodeId,
                sourceAnchorId = edge.SourceAnchorId,
                sourcePortId = edge.SourcePortId,
                targetNodeId = edge.TargetNodeId,
                targetAnchorId = edge.TargetAnchorId,
                targetPortId = edge.TargetPortId,
                kind = edge.Kind,
                categoryKey = edge.CategoryKey
            });
        Assert.True(disconnected, $"Expected disconnect proof request for edge '{edge.Id}' to succeed.");

        await WaitForSnapshotAsync(
            page,
            snapshot => snapshot.Edges.All(candidate => !string.Equals(candidate.Id, edge.Id, StringComparison.Ordinal)),
            "semantic disconnection");

        var reconnected = await page.EvaluateAsync<bool>(
            @"request => {
                const host = document.querySelector('.wgl-workbench-host');
                const runtime = window.CanDoItAll?.webglWorkbench;
                return !!runtime?.simulateConnection(host, request);
            }",
            new
            {
                actionId = "connect",
                edgeId = (string?)null,
                sourceNodeId = edge.SourceNodeId,
                sourceAnchorId = edge.SourceAnchorId,
                sourcePortId = edge.SourcePortId,
                targetNodeId = edge.TargetNodeId,
                targetAnchorId = edge.TargetAnchorId,
                targetPortId = edge.TargetPortId,
                kind = edge.Kind,
                categoryKey = edge.CategoryKey
            });
        Assert.True(reconnected, $"Expected reconnect proof request for edge '{edge.Id}' to succeed.");

        await WaitForSnapshotAsync(
            page,
            snapshot => snapshot.Edges.Any(candidate => string.Equals(candidate.Id, edge.Id, StringComparison.Ordinal)),
            "semantic reconnection");

        var exportLength = await page.EvaluateAsync<int>(
            @"() => {
                const host = document.querySelector('.wgl-workbench-host');
                return window.CanDoItAll?.webglWorkbench?.exportImageLength(host) ?? 0;
            }");
        Assert.True(exportLength > 1000, $"Expected browser-side export length to be meaningful, got {exportLength}.");

        await page.GetByTestId("webgl-sandbox-export").ClickAsync();
        await page.WaitForFunctionAsync(
            @"() => {
                const text = document.body?.innerText || '';
                return text.includes('Exported image') && text.includes('export chars');
            }",
            null,
            new PageWaitForFunctionOptions
            {
                Timeout = 30_000
            });

        await page.Locator("[data-testid='webgl-sandbox-stage']").ScreenshotAsync(new LocatorScreenshotOptions
        {
            Path = Path.Combine(artifactsDir, "03-webgl-semantic-proof.png")
        });

        Assert.False(await page.Locator("#blazor-error-ui").IsVisibleAsync());
    }

    [Fact]
    [Trait("Surface", "WebGlSandbox")]
    public async Task Sandbox_in_scene_chrome_controls_camera_settings_and_context_actions()
    {
        var artifactsDir = EnsureArtifactsDirectory();

        await using var context = await fixture.Browser.NewContextAsync(new BrowserNewContextOptions
        {
            IgnoreHTTPSErrors = true,
            ViewportSize = new ViewportSize
            {
                Width = 1900,
                Height = 1200
            }
        });
        var page = await context.NewPageAsync();

        var response = await page.GotoAsync($"{fixture.BaseUrl}/webgl/process-workbench?template=branching-code-review");
        Assert.NotNull(response);
        Assert.True(response!.Ok, $"Expected sandbox route to return 2xx, got {(int)response.Status}.");

        await WaitForSnapshotAsync(
            page,
            snapshot => string.Equals(snapshot.SceneKey, "branching-code-review", StringComparison.Ordinal) && snapshot.Nodes.Count > 0,
            "branching template for in-scene chrome");

        var initialState = await ReadUiStateAsync(page);
        Assert.Equal("perspective", initialState.Camera.ProjectionMode);

        var initialChrome = await ReadChromeStateAsync(page);
        Assert.Equal("select", initialChrome.ToolMode);
        Assert.False(initialChrome.SettingsOpen);
        Assert.Contains(initialChrome.Actions, action => string.Equals(action.Id, "tool:reconnect", StringComparison.Ordinal));
        Assert.Contains(initialChrome.Actions, action => string.Equals(action.Id, "chrome:settings", StringComparison.Ordinal));

        Assert.True(await InvokeChromeActionAsync(page, "chrome:settings"));
        var settingsChrome = await WaitForChromeStateAsync(
            page,
            chrome =>
                chrome.SettingsOpen &&
                chrome.Actions.Any(action => string.Equals(action.Id, "info:miniature", StringComparison.Ordinal)) &&
                chrome.Actions.Any(action => string.Equals(action.Id, "toggle:roles", StringComparison.Ordinal)),
            "settings panel to expose in-scene display controls");
        Assert.Contains(settingsChrome.Actions, action => string.Equals(action.Id, "toggle:branches", StringComparison.Ordinal));

        Assert.True(await InvokeChromeActionAsync(page, "info:miniature"));
        var miniatureScene = await WaitForSnapshotAsync(
            page,
            snapshot => string.Equals(snapshot.NodeInfoMode, "miniature", StringComparison.Ordinal),
            "miniature node label mode");
        await page.WaitForFunctionAsync(
            @"() => !!document.querySelector('.wgl-node-label.is-miniature')");

        Assert.True(await InvokeChromeActionAsync(page, "toggle:roles"));
        var filteredScene = await WaitForSnapshotAsync(
            page,
            snapshot =>
                !snapshot.ShowRoleNodes &&
                snapshot.Nodes.All(node => !node.Kind.Contains("role", StringComparison.OrdinalIgnoreCase)),
            "role helper nodes to hide");
        Assert.True(filteredScene.Nodes.Count < miniatureScene.Nodes.Count);

        await page.Locator("[data-testid='webgl-sandbox-stage']").ScreenshotAsync(new LocatorScreenshotOptions
        {
            Path = Path.Combine(artifactsDir, "06-webgl-in-scene-settings.png")
        });

        Assert.True(await InvokeChromeActionAsync(page, "chrome:settings"));
        await WaitForChromeStateAsync(
            page,
            chrome => !chrome.SettingsOpen,
            "settings panel to close");

        var connectCandidate = await ResolveExplicitConnectionCandidateAsync(page);
        Assert.False(
            string.IsNullOrWhiteSpace(connectCandidate.SourceAnchorId),
            "Expected the dense WebGL sandbox to expose a multi-anchor connect candidate.");

        var sourcePoint = await ResolveNodeViewportPointAsync(page, connectCandidate.SourceNodeId);
        await page.Mouse.ClickAsync((float)sourcePoint.X, (float)sourcePoint.Y, new MouseClickOptions
        {
            Button = MouseButton.Right
        });

        var sourceMenu = await WaitForChromeStateAsync(
            page,
            chrome =>
                chrome.ContextMenu is not null &&
                string.Equals(chrome.ContextMenu.Title, connectCandidate.SourceNodeTitle, StringComparison.Ordinal) &&
                chrome.ContextMenu.Items.Any(item => string.Equals(item.Id, "menu:connect-from-node", StringComparison.Ordinal)),
            "node context menu to expose the explicit-source connect draft");
        Assert.Contains(sourceMenu.ContextMenu!.Items, item => string.Equals(item.Id, "menu:focus-node", StringComparison.Ordinal));

        await page.Locator("[data-testid='webgl-sandbox-stage']").ScreenshotAsync(new LocatorScreenshotOptions
        {
            Path = Path.Combine(artifactsDir, "07-webgl-node-context-menu.png")
        });

        Assert.True(await InvokeChromeActionAsync(page, "menu:connect-from-node"));
        await WaitForChromeStateAsync(
            page,
            chrome => string.Equals(chrome.ToolMode, "connect", StringComparison.Ordinal),
            "connect draft to activate");

        var sourceAnchorPoint = await ResolveAnchorViewportPointAsync(page, connectCandidate.SourceAnchorId);
        await page.Mouse.ClickAsync((float)sourceAnchorPoint.X, (float)sourceAnchorPoint.Y);
        await page.WaitForFunctionAsync(
            @"anchorId => {
                const element = document.querySelector(`[data-webgl-port-label-for='${anchorId}']`);
                return element instanceof HTMLElement && getComputedStyle(element).display !== 'none';
            }",
            connectCandidate.SourceAnchorId,
            new PageWaitForFunctionOptions
            {
                Timeout = 30_000
            });

        var targetPoint = await ResolveAnchorViewportPointAsync(page, connectCandidate.TargetAnchorId);
        await page.Mouse.ClickAsync((float)targetPoint.X, (float)targetPoint.Y);

        var connectedScene = await WaitForSnapshotAsync(
            page,
                snapshot =>
                snapshot.Edges.Any(edge =>
                    string.Equals(edge.SourceAnchorId, connectCandidate.SourceAnchorId, StringComparison.Ordinal) &&
                    string.Equals(edge.TargetAnchorId, connectCandidate.TargetAnchorId, StringComparison.Ordinal)),
            "connect tool to create an explicit source-point to target-input connection");

        Assert.True(await InvokeChromeActionAsync(page, "tool:select"));
        await WaitForChromeStateAsync(
            page,
            chrome => string.Equals(chrome.ToolMode, "select", StringComparison.Ordinal),
            "select tool to restore after connection draft");

        Assert.True(await page.EvaluateAsync<bool>(
            @"targetNodeId => {
                const host = document.querySelector('.wgl-workbench-host');
                const runtime = window.CanDoItAll?.webglWorkbench;
                if (!host || !runtime?.focusNode || !runtime?.zoomView) {
                    return false;
                }

                runtime.focusNode(host, targetNodeId);
                runtime.zoomView(host, 2);
                return true;
            }",
            connectCandidate.TargetNodeId));
        await page.WaitForFunctionAsync(
            @"anchorId => {
                const element = document.querySelector(`[data-webgl-port-label-for='${anchorId}']`);
                return element instanceof HTMLElement && getComputedStyle(element).display !== 'none';
            }",
            connectCandidate.TargetAnchorId,
            new PageWaitForFunctionOptions
            {
                Timeout = 30_000
            });

        var baselineCameraState = await ReadUiStateAsync(page);
        Assert.True(await page.EvaluateAsync<bool>(
            @"() => {
                const host = document.querySelector('.wgl-workbench-host');
                const runtime = window.CanDoItAll?.webglWorkbench;
                if (!host || !runtime?.orbitView) {
                    return false;
                }

                runtime.orbitView(host, 0.22, 0.04);
                return true;
            }"));
        var orbitState = await WaitForUiStateAsync(
            page,
            state => Math.Abs(state.Camera.Azimuth - baselineCameraState.Camera.Azimuth) > 0.08,
            "runtime orbit action to change azimuth");

        var zoomedCamera = await page.EvaluateAsync<WebGlCameraStateProof>(
            @"() => {
                const host = document.querySelector('.wgl-workbench-host');
                const runtime = window.CanDoItAll?.webglWorkbench;
                if (!host || !runtime?.zoomView || !runtime?.getState) {
                    return { projectionMode: '', targetX: 0, targetY: 0, targetZ: 0, zoom: 0, distance: 0, azimuth: 0, polar: 0 };
                }

                runtime.zoomView(host, 2);
                return JSON.parse(runtime.getState(host)).camera;
            }");
        Assert.True(
            zoomedCamera.Distance < orbitState.Camera.Distance - 50,
            $"Expected runtime zoom action to reduce camera distance below {orbitState.Camera.Distance - 50:0.##}, got {zoomedCamera.Distance:0.##}.");

        Assert.True(await page.EvaluateAsync<bool>(
            @"() => {
                const host = document.querySelector('.wgl-workbench-host');
                const runtime = window.CanDoItAll?.webglWorkbench;
                if (!host || !runtime?.panView) {
                    return false;
                }

                runtime.panView(host, 140, -60);
                return true;
            }"));
        var pannedState = await WaitForUiStateAsync(
            page,
            state =>
                Math.Abs(state.Camera.TargetX - zoomedCamera.TargetX) > 1 ||
                Math.Abs(state.Camera.TargetY - zoomedCamera.TargetY) > 1 ||
                Math.Abs(state.Camera.TargetZ - zoomedCamera.TargetZ) > 1,
            "runtime pan action to move the camera target");

        Assert.True(await InvokeChromeActionAsync(page, "view:reset"));
        await page.WaitForTimeoutAsync(200);
        Assert.True(pannedState.Camera.Distance > 0);
        Assert.False(await page.Locator("#blazor-error-ui").IsVisibleAsync());
    }

    [Fact]
    [Trait("Surface", "WebGlSandbox")]
    public async Task Sandbox_recomposes_scene_adjusts_spacing_scales_labels_and_blocks_collisions()
    {
        await using var context = await fixture.Browser.NewContextAsync(new BrowserNewContextOptions
        {
            IgnoreHTTPSErrors = true,
            ViewportSize = new ViewportSize
            {
                Width = 1900,
                Height = 1200
            }
        });
        var page = await context.NewPageAsync();

        var response = await page.GotoAsync($"{fixture.BaseUrl}/webgl/process-workbench?template=branching-code-review");
        Assert.NotNull(response);
        Assert.True(response!.Ok, $"Expected sandbox route to return 2xx, got {(int)response.Status}.");

        var baselineScene = await WaitForSnapshotAsync(
            page,
            snapshot => string.Equals(snapshot.SceneKey, "branching-code-review", StringComparison.Ordinal) && snapshot.Nodes.Count > 0,
            "branching template for recomposition");
        var trackedNode = baselineScene.Nodes.First(node =>
            !node.Kind.Contains("role", StringComparison.OrdinalIgnoreCase) &&
            !node.Kind.Contains("branch", StringComparison.OrdinalIgnoreCase));

        await page.GetByLabel("Layout algorithm").SelectOptionAsync(
        [
            new SelectOptionValue
            {
                Label = "Alternating arc"
            }
        ]);

        var recomposedScene = await WaitForSnapshotAsync(
            page,
            snapshot =>
                string.Equals(snapshot.LayoutMode, "alternating-arc", StringComparison.Ordinal) &&
                snapshot.Nodes.Any(node =>
                    string.Equals(node.Id, trackedNode.Id, StringComparison.Ordinal) &&
                    Math.Abs(node.X - trackedNode.X) > 20),
            "alternating arc recomposition");

        await page.GetByTestId("webgl-sandbox-spacing-increase").ClickAsync();
        await page.GetByTestId("webgl-sandbox-spacing-increase").ClickAsync();

        var spacedScene = await WaitForSnapshotAsync(
            page,
            snapshot =>
                string.Equals(snapshot.LayoutMode, "alternating-arc", StringComparison.Ordinal) &&
                snapshot.NodeSpacingFactor > recomposedScene.NodeSpacingFactor &&
                snapshot.Nodes.Where(node => node.Kind.Contains("role", StringComparison.OrdinalIgnoreCase)).Average(node => Math.Abs(node.X)) >
                recomposedScene.Nodes.Where(node => node.Kind.Contains("role", StringComparison.OrdinalIgnoreCase)).Average(node => Math.Abs(node.X)),
            "spacing increase to spread nodes");

        var labelScaleBeforeZoom = await ReadLabelScaleAsync(page, trackedNode.Id);
        var initialCameraState = await ReadUiStateAsync(page);

        var zoomedOut = await page.EvaluateAsync<WebGlCameraStateProof>(
            @"() => {
                const host = document.querySelector('.wgl-workbench-host');
                const runtime = window.CanDoItAll?.webglWorkbench;
                if (!host || !runtime?.zoomView || !runtime?.getState) {
                    return { projectionMode: '', targetX: 0, targetY: 0, targetZ: 0, zoom: 0, distance: 0, azimuth: 0, polar: 0 };
                }

                for (let index = 0; index < 5; index += 1) {
                    runtime.zoomView(host, 0.84);
                }

                return JSON.parse(runtime.getState(host)).camera;
            }");
        Assert.True(
            zoomedOut.Distance > initialCameraState.Camera.Distance + 80,
            $"Expected runtime zoom out to increase camera distance beyond {initialCameraState.Camera.Distance + 80:0.##}, got {zoomedOut.Distance:0.##}.");

        var labelScaleAfterZoom = await ReadLabelScaleAsync(page, trackedNode.Id);
        Assert.True(
            labelScaleAfterZoom <= labelScaleBeforeZoom + 0.01d,
            "Expected labels to hold steady or shrink before reaching the unzoom clamp.");
        Assert.InRange(labelScaleAfterZoom, 0.44d, 0.7d);

        var preCollisionScene = await ReadSceneSnapshotAsync(page);
        var collisionPlan = await ResolveCollisionProbeAsync(page);
        Assert.True(collisionPlan.Accepted, "Expected at least one collision probe drag to advance until contact without overlapping the target node.");
        var sourceBeforeCollision = preCollisionScene.Nodes.First(node => string.Equals(node.Id, collisionPlan.NodeId, StringComparison.Ordinal));
        var targetBeforeCollision = preCollisionScene.Nodes.First(node => string.Equals(node.Id, collisionPlan.TargetNodeId, StringComparison.Ordinal));
        Assert.True(
            Math.Abs(collisionPlan.X - sourceBeforeCollision.X) > 1 || Math.Abs(collisionPlan.Y - sourceBeforeCollision.Y) > 1,
            "Expected the accepted collision probe to commit a meaningful move.");

        var movedNode = new WebGlSceneNode
        {
            Id = collisionPlan.NodeId,
            Kind = sourceBeforeCollision.Kind,
            Title = sourceBeforeCollision.Title,
            Subtitle = sourceBeforeCollision.Subtitle,
            X = collisionPlan.X,
            Y = collisionPlan.Y,
            Z = sourceBeforeCollision.Z,
            SceneWidth = sourceBeforeCollision.SceneWidth,
            SceneHeight = sourceBeforeCollision.SceneHeight,
            SceneDepth = sourceBeforeCollision.SceneDepth
        };

        Assert.False(NodesOverlap(movedNode, targetBeforeCollision), "Collision protection should prevent nodes from overlapping after drag.");
        Assert.False(await page.Locator("#blazor-error-ui").IsVisibleAsync());
    }

    [Theory]
    [InlineData(1366, 768, 1200, 600, "04-webgl-route-1366x768.png")]
    [InlineData(430, 932, 360, 540, "05-webgl-route-430x932.png")]
    [Trait("Surface", "WebGlSandbox")]
    public async Task Sandbox_route_stays_visible_across_review_viewports(
        int viewportWidth,
        int viewportHeight,
        double minimumStageWidth,
        double minimumStageHeight,
        string screenshotFileName)
    {
        var artifactsDir = EnsureArtifactsDirectory();

        await using var context = await fixture.Browser.NewContextAsync(new BrowserNewContextOptions
        {
            IgnoreHTTPSErrors = true,
            ViewportSize = new ViewportSize
            {
                Width = viewportWidth,
                Height = viewportHeight
            }
        });
        var page = await context.NewPageAsync();

        var response = await page.GotoAsync($"{fixture.BaseUrl}/webgl/process-workbench");
        Assert.NotNull(response);
        Assert.True(response!.Ok, $"Expected sandbox route to return 2xx, got {(int)response.Status}.");

        await WaitForSnapshotAsync(
            page,
            snapshot => snapshot.Nodes.Count > 0 && snapshot.Edges.Count > 0,
            $"sandbox snapshot at {viewportWidth}x{viewportHeight}");

        await AssertStageBoundsAsync(page, minimumStageWidth, minimumStageHeight);

        await page.ScreenshotAsync(new PageScreenshotOptions
        {
            FullPage = true,
            Path = Path.Combine(artifactsDir, screenshotFileName)
        });

        Assert.False(await page.Locator("#blazor-error-ui").IsVisibleAsync());
    }

    private static string EnsureArtifactsDirectory()
    {
        var directory = Path.Combine(GetRepoRoot(), "output", "playwright", "webgl-sandbox");
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static string GetRepoRoot()
    {
        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    }

    private static async Task<WebGlSceneSnapshot> ReadSceneSnapshotAsync(IPage page)
    {
        var json = await page.EvaluateAsync<string>(
            @"() => {
                const host = document.querySelector('.wgl-workbench-host');
                return JSON.stringify(window.CanDoItAll?.webglWorkbench?.getSceneSnapshot(host) ?? {});
            }");
        return DeserializeSceneSnapshot(json);
    }

    private static async Task<WebGlUiStateProof> ReadUiStateAsync(IPage page)
    {
        var json = await page.EvaluateAsync<string>(
            @"() => {
                const host = document.querySelector('.wgl-workbench-host');
                return window.CanDoItAll?.webglWorkbench?.getState(host) ?? '{}';
            }");
        return DeserializeUiState(json);
    }

    private static async Task<WebGlChromeStateProof> ReadChromeStateAsync(IPage page)
    {
        var json = await page.EvaluateAsync<string>(
            @"() => {
                const host = document.querySelector('.wgl-workbench-host');
                return JSON.stringify(window.CanDoItAll?.webglWorkbench?.getChromeState(host) ?? {});
            }");
        return DeserializeChromeState(json);
    }

    private static async Task<bool> InvokeChromeActionAsync(IPage page, string actionId)
    {
        return await page.EvaluateAsync<bool>(
            @"requestedActionId => {
                const host = document.querySelector('.wgl-workbench-host');
                const runtime = window.CanDoItAll?.webglWorkbench;
                return !!host && !!runtime?.invokeChromeAction?.(host, requestedActionId);
            }",
            actionId);
    }

    private static WebGlSceneSnapshot DeserializeSceneSnapshot(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new WebGlSceneSnapshot();
        }

        var snapshot = JsonSerializer.Deserialize<WebGlSceneSnapshot>(json, JsonOptions) ?? new WebGlSceneSnapshot();
        snapshot.Nodes ??= [];
        snapshot.Edges ??= [];
        return snapshot;
    }

    private static WebGlUiStateProof DeserializeUiState(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new WebGlUiStateProof();
        }

        var state = JsonSerializer.Deserialize<WebGlUiStateProof>(json, JsonOptions) ?? new WebGlUiStateProof();
        state.Camera ??= new WebGlCameraStateProof();
        return state;
    }

    private static WebGlChromeStateProof DeserializeChromeState(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new WebGlChromeStateProof();
        }

        var state = JsonSerializer.Deserialize<WebGlChromeStateProof>(json, JsonOptions) ?? new WebGlChromeStateProof();
        state.Actions ??= [];
        state.ContextMenu ??= new WebGlContextMenuProof();
        state.ContextMenu.Items ??= [];
        return state;
    }

    private async Task<WebGlSceneSnapshot> WaitForSnapshotAsync(
        IPage page,
        Func<WebGlSceneSnapshot, bool> predicate,
        string description,
        int timeoutMs = 30_000)
    {
        var stopwatch = Stopwatch.StartNew();
        WebGlSceneSnapshot? latestSnapshot = null;

        while (stopwatch.ElapsedMilliseconds < timeoutMs)
        {
            latestSnapshot = await ReadSceneSnapshotAsync(page);
            if (predicate(latestSnapshot))
            {
                return latestSnapshot;
            }

            await page.WaitForTimeoutAsync(200);
        }

        var snapshotSummary = latestSnapshot is null
            ? "No snapshot returned."
            : $"SceneKey={latestSnapshot.SceneKey}, Nodes={latestSnapshot.Nodes.Count}, Edges={latestSnapshot.Edges.Count}.";
        throw new TimeoutException(
            $"Timed out waiting for {description}. {snapshotSummary}{Environment.NewLine}{fixture.GetLogSnapshot()}");
    }

    private async Task<WebGlUiStateProof> WaitForUiStateAsync(
        IPage page,
        Func<WebGlUiStateProof, bool> predicate,
        string description,
        int timeoutMs = 30_000)
    {
        var stopwatch = Stopwatch.StartNew();
        WebGlUiStateProof? latestState = null;

        while (stopwatch.ElapsedMilliseconds < timeoutMs)
        {
            latestState = await ReadUiStateAsync(page);
            if (predicate(latestState))
            {
                return latestState;
            }

            await page.WaitForTimeoutAsync(150);
        }

        var stateSummary = latestState is null
            ? "No UI state returned."
            : $"Projection={latestState.Camera.ProjectionMode}, Distance={latestState.Camera.Distance}, Azimuth={latestState.Camera.Azimuth}, Polar={latestState.Camera.Polar}.";
        throw new TimeoutException(
            $"Timed out waiting for {description}. {stateSummary}{Environment.NewLine}{fixture.GetLogSnapshot()}");
    }

    private async Task<WebGlChromeStateProof> WaitForChromeStateAsync(
        IPage page,
        Func<WebGlChromeStateProof, bool> predicate,
        string description,
        int timeoutMs = 30_000)
    {
        var stopwatch = Stopwatch.StartNew();
        WebGlChromeStateProof? latestState = null;

        while (stopwatch.ElapsedMilliseconds < timeoutMs)
        {
            latestState = await ReadChromeStateAsync(page);
            if (predicate(latestState))
            {
                return latestState;
            }

            await page.WaitForTimeoutAsync(150);
        }

        var stateSummary = latestState is null
            ? "No chrome state returned."
            : $"ToolMode={latestState.ToolMode}, SettingsOpen={latestState.SettingsOpen}, ContextMenuTitle={latestState.ContextMenu?.Title ?? "<none>"}";
        throw new TimeoutException(
            $"Timed out waiting for {description}. {stateSummary}{Environment.NewLine}{fixture.GetLogSnapshot()}");
    }

    private static async Task AssertStageBoundsAsync(IPage page, double minimumWidth, double minimumHeight)
    {
        var bounds = await page.Locator("[data-testid='webgl-sandbox-stage']").BoundingBoxAsync();
        Assert.NotNull(bounds);
        Assert.True(
            bounds!.Width >= minimumWidth,
            $"Expected stage width >= {minimumWidth}, got {bounds.Width}.");
        Assert.True(
            bounds.Height >= minimumHeight,
            $"Expected stage height >= {minimumHeight}, got {bounds.Height}.");
    }

    private static async Task<double> ReadLabelScaleAsync(IPage page, string nodeId)
    {
        return await page.EvaluateAsync<double>(
            @"targetNodeId => {
                const element = document.querySelector(`[data-webgl-node-id='${targetNodeId}']`);
                if (!(element instanceof HTMLElement)) {
                    return 0;
                }

                const rawValue = getComputedStyle(element).getPropertyValue('--wgl-label-scale').trim();
                const parsed = Number.parseFloat(rawValue);
                return Number.isFinite(parsed) ? parsed : 0;
            }",
            nodeId);
    }

    private static async Task<WebGlViewportPointProof> ResolveNodeViewportPointAsync(IPage page, string nodeId)
    {
        return await page.EvaluateAsync<WebGlViewportPointProof>(
            @"targetNodeId => {
                const host = document.querySelector('.wgl-workbench-host');
                const runtime = window.CanDoItAll?.webglWorkbench;
                const rect = host?.getBoundingClientRect();
                const snapshot = runtime?.getSceneSnapshot?.(host);
                const node = snapshot?.nodes?.find(candidate => candidate.id === targetNodeId);
                if (!host || !rect || !node) {
                    return { x: 0, y: 0 };
                }

                return {
                    x: rect.left + node.left + (node.width / 2),
                    y: rect.top + node.top + (node.height / 2)
                };
            }",
            nodeId);
    }

    private static async Task<WebGlViewportPointProof> ResolveAnchorViewportPointAsync(IPage page, string anchorId)
    {
        return await page.EvaluateAsync<WebGlViewportPointProof>(
            @"targetAnchorId => {
                const host = document.querySelector('.wgl-workbench-host');
                const state = host?.__webglWorkbenchState;
                const rect = host?.getBoundingClientRect();
                const projectedAnchor = state?.projectedAnchors?.get?.(targetAnchorId);
                if (!host || !rect || !projectedAnchor) {
                    return { x: 0, y: 0 };
                }

                return {
                    x: rect.left + projectedAnchor.x,
                    y: rect.top + projectedAnchor.y
                };
            }",
            anchorId);
    }

    private static async Task<WebGlExplicitConnectionCandidateProof> ResolveExplicitConnectionCandidateAsync(IPage page)
    {
        return await page.EvaluateAsync<WebGlExplicitConnectionCandidateProof>(
            @"() => {
                const host = document.querySelector('.wgl-workbench-host');
                const state = host?.__webglWorkbenchState;
                const nodes = state?.surface?.nodes || [];
                const edges = state?.surface?.edges || [];

                const normalizeCategory = value => {
                    const category = String(value || '').toLowerCase();
                    if (!category) {
                        return 'default';
                    }

                    if (category.includes('branch')) {
                        return 'branch';
                    }

                    if (category.includes('struct')) {
                        return 'structural';
                    }

                    if (category.includes('artifact')) {
                        return 'artifact';
                    }

                    if (category.includes('message')) {
                        return 'messaging';
                    }

                    if (category.includes('decision')) {
                        return 'decision';
                    }

                    if (category.includes('responsibility')) {
                        return 'responsibility';
                    }

                    return category;
                };

                const areCompatible = (sourceAnchor, targetAnchor) => {
                    if (!sourceAnchor || !targetAnchor || sourceAnchor.nodeId === targetAnchor.nodeId) {
                        return false;
                    }

                    const sourceCategory = normalizeCategory(sourceAnchor.categoryKey);
                    const targetCategory = normalizeCategory(targetAnchor.categoryKey);
                    if (sourceCategory === targetCategory) {
                        return true;
                    }

                    if (sourceCategory === 'default' || targetCategory === 'default') {
                        return true;
                    }

                    return sourceCategory === 'branch' && targetCategory === 'structural';
                };

                for (const sourceNode of nodes) {
                    const outputs = (sourceNode.anchors || []).filter(anchor => anchor?.role === 'output');
                    if (outputs.length < 2) {
                        continue;
                    }

                    for (const sourceAnchor of outputs.slice(1)) {
                        for (const targetNode of nodes) {
                            if (targetNode.id === sourceNode.id) {
                                continue;
                            }

                            const inputs = (targetNode.anchors || []).filter(anchor => anchor?.role === 'input');
                            if (inputs.length < 2) {
                                continue;
                            }

                            for (const targetAnchor of inputs) {
                                if (!areCompatible(sourceAnchor, targetAnchor)) {
                                    continue;
                                }

                                const edgeExists = edges.some(edge =>
                                    edge?.sourceAnchorId === sourceAnchor.id &&
                                    edge?.targetAnchorId === targetAnchor.id);
                                if (edgeExists) {
                                    continue;
                                }

                                return {
                                    sourceNodeId: sourceNode.id || '',
                                    sourceNodeTitle: sourceNode.title || sourceNode.id || '',
                                    sourceAnchorId: sourceAnchor.id || '',
                                    sourceAnchorLabel: sourceAnchor.label || sourceAnchor.portId || sourceAnchor.id || '',
                                    targetNodeId: targetNode.id || '',
                                    targetNodeTitle: targetNode.title || targetNode.id || '',
                                    targetAnchorId: targetAnchor.id || '',
                                    targetAnchorLabel: targetAnchor.label || targetAnchor.portId || targetAnchor.id || ''
                                };
                            }
                        }
                    }
                }

                return {
                    sourceNodeId: '',
                    sourceNodeTitle: '',
                    sourceAnchorId: '',
                    sourceAnchorLabel: '',
                    targetNodeId: '',
                    targetNodeTitle: '',
                    targetAnchorId: '',
                    targetAnchorLabel: ''
                };
            }");
    }

    private static async Task<WebGlViewportPointProof> ResolveEdgeViewportPointAsync(IPage page, string edgeId)
    {
        return await page.EvaluateAsync<WebGlViewportPointProof>(
            @"targetEdgeId => {
                const host = document.querySelector('.wgl-workbench-host');
                const state = host?.__webglWorkbenchState;
                const rect = host?.getBoundingClientRect();
                const projectedEdge = state?.projectedEdges?.get?.(targetEdgeId);
                if (!host || !rect || !state?.raycaster || !state?.camera || !projectedEdge) {
                    return { x: 0, y: 0 };
                }

                const resolveNodeId = intersection => intersection?.object?.userData?.nodeId || intersection?.object?.parent?.userData?.nodeId || '';
                const resolveEdgeId = intersection => intersection?.object?.userData?.edgeId || intersection?.object?.parent?.userData?.edgeId || '';
                const tryHit = (offsetX, offsetY) => {
                    const clientX = rect.left + projectedEdge.x + offsetX;
                    const clientY = rect.top + projectedEdge.y + offsetY;
                    const ndc = {
                        x: ((clientX - rect.left) / rect.width) * 2 - 1,
                        y: -(((clientY - rect.top) / rect.height) * 2 - 1)
                    };

                    state.raycaster.setFromCamera(ndc, state.camera);
                    const nodeHits = state.raycaster.intersectObjects(state.nodeMeshes || [], false);
                    const edgeHits = state.raycaster.intersectObjects(state.edgeHitMeshes || [], false);
                    if (resolveEdgeId(edgeHits[0]) !== targetEdgeId || resolveNodeId(nodeHits[0])) {
                        return null;
                    }

                    return {
                        x: clientX,
                        y: clientY
                    };
                };

                const offsets = [
                    [0, 0],
                    [0, -18],
                    [18, 0],
                    [-18, 0],
                    [0, 18],
                    [24, -16],
                    [-24, -16],
                    [24, 16],
                    [-24, 16],
                    [32, 0],
                    [-32, 0],
                    [0, -32],
                    [0, 32]
                ];

                for (const [offsetX, offsetY] of offsets) {
                    const point = tryHit(offsetX, offsetY);
                    if (point) {
                        return point;
                    }
                }

                return {
                    x: rect.left + projectedEdge.x,
                    y: rect.top + projectedEdge.y
                };
            }",
            edgeId);
    }

    private static async Task<WebGlEdgeContextMenuProbeProof> ResolveEdgeContextMenuCandidateAsync(IPage page)
    {
        return await page.EvaluateAsync<WebGlEdgeContextMenuProbeProof>(
            @"() => {
                const host = document.querySelector('.wgl-workbench-host');
                const state = host?.__webglWorkbenchState;
                const rect = host?.getBoundingClientRect();
                if (!host || !rect || !state?.raycaster || !state?.camera || !state?.projectedEdges?.size) {
                    return { edgeId: '', x: 0, y: 0 };
                }

                const resolveNodeId = intersection => intersection?.object?.userData?.nodeId || intersection?.object?.parent?.userData?.nodeId || '';
                const resolveEdgeId = intersection => intersection?.object?.userData?.edgeId || intersection?.object?.parent?.userData?.edgeId || '';
                const offsets = [
                    [0, 0],
                    [0, -18],
                    [18, 0],
                    [-18, 0],
                    [0, 18],
                    [24, -16],
                    [-24, -16],
                    [24, 16],
                    [-24, 16],
                    [32, 0],
                    [-32, 0],
                    [0, -32],
                    [0, 32]
                ];

                const tryHit = (edgeId, projectedEdge, offsetX, offsetY) => {
                    const clientX = rect.left + projectedEdge.x + offsetX;
                    const clientY = rect.top + projectedEdge.y + offsetY;
                    const ndc = {
                        x: ((clientX - rect.left) / rect.width) * 2 - 1,
                        y: -(((clientY - rect.top) / rect.height) * 2 - 1)
                    };

                    state.raycaster.setFromCamera(ndc, state.camera);
                    const nodeHits = state.raycaster.intersectObjects(state.nodeMeshes || [], false);
                    const edgeHits = state.raycaster.intersectObjects(state.edgeHitMeshes || [], false);
                    if (resolveEdgeId(edgeHits[0]) !== edgeId || resolveNodeId(nodeHits[0])) {
                        return null;
                    }

                    return {
                        edgeId,
                        x: clientX,
                        y: clientY
                    };
                };

                for (const [edgeId, projectedEdge] of state.projectedEdges.entries()) {
                    for (const [offsetX, offsetY] of offsets) {
                        const point = tryHit(edgeId, projectedEdge, offsetX, offsetY);
                        if (point) {
                            return point;
                        }
                    }
                }

                return { edgeId: '', x: 0, y: 0 };
            }");
    }

    private static async Task<string> ResolveReconnectTargetNodeIdAsync(IPage page, string edgeId)
    {
        return await page.EvaluateAsync<string>(
            @"targetEdgeId => {
                const host = document.querySelector('.wgl-workbench-host');
                const state = host?.__webglWorkbenchState;
                const edge = (state?.surface?.edges || []).find(candidate => candidate.id === targetEdgeId);
                if (!edge) {
                    return '';
                }

                const candidates = (state.surface?.nodes || []).filter(node => {
                    const kind = String(node?.kind || '').toLowerCase();
                    const hasProjectedNode = state.projectedNodes?.has?.(node.id) ?? false;
                    const hasInputAnchor = Array.isArray(node?.anchors) && node.anchors.some(anchor => anchor?.role === 'input');
                    return hasProjectedNode &&
                        hasInputAnchor &&
                        !kind.includes('role') &&
                        !kind.includes('branch') &&
                        node.id !== edge.sourceNodeId &&
                        node.id !== edge.targetNodeId;
                });

                return candidates[0]?.id || '';
            }",
            edgeId);
    }

    private static async Task<WebGlDragProbeResult> ResolveSupportedDragAsync(IPage page, WebGlSceneNode node)
    {
        return await page.EvaluateAsync<WebGlDragProbeResult>(
            @"request => {
                const host = document.querySelector('.wgl-workbench-host');
                const runtime = window.CanDoItAll?.webglWorkbench;
                if (!host || !runtime?.simulateDrag || !runtime?.getSceneSnapshot) {
                    return { accepted: false, nodeId: request.nodeId, targetNodeId: '', deltaX: 0, deltaY: 0, x: request.nodeX, y: request.nodeY };
                }

                const horizontalDirection = (request.nodeX || 0) <= 0 ? -1 : 1;
                const verticalDirection = (request.nodeY || 0) <= 0 ? -1 : 1;
                const candidates = [
                    { deltaX: horizontalDirection * 48, deltaY: 0 },
                    { deltaX: horizontalDirection * 72, deltaY: verticalDirection * 24 },
                    { deltaX: horizontalDirection * 36, deltaY: verticalDirection * 18 },
                    { deltaX: 0, deltaY: verticalDirection * 36 },
                    { deltaX: horizontalDirection * 24, deltaY: 0 },
                    { deltaX: horizontalDirection * 24, deltaY: verticalDirection * 12 }
                ];

                for (const candidate of candidates) {
                    const accepted = !!runtime.simulateDrag(host, {
                        nodeId: request.nodeId,
                        deltaX: candidate.deltaX,
                        deltaY: candidate.deltaY
                    });
                    if (!accepted) {
                        continue;
                    }

                    const movedNode = runtime.getSceneSnapshot(host)?.nodes?.find(node => node.id === request.nodeId);
                    return {
                        accepted: true,
                        nodeId: request.nodeId,
                        targetNodeId: '',
                        deltaX: candidate.deltaX,
                        deltaY: candidate.deltaY,
                        x: movedNode?.x ?? request.nodeX,
                        y: movedNode?.y ?? request.nodeY
                    };
                }

                return { accepted: false, nodeId: request.nodeId, targetNodeId: '', deltaX: 0, deltaY: 0, x: request.nodeX, y: request.nodeY };
            }",
            new
            {
                nodeId = node.Id,
                nodeX = node.X,
                nodeY = node.Y
            });
    }

    private static async Task<WebGlDragProbeResult> ResolveCollisionProbeAsync(IPage page)
    {
        return await page.EvaluateAsync<WebGlDragProbeResult>(
            @"() => {
                const host = document.querySelector('.wgl-workbench-host');
                const runtime = window.CanDoItAll?.webglWorkbench;
                if (!host || !runtime?.simulateDrag || !runtime?.getSceneSnapshot) {
                    return { accepted: false, nodeId: '', targetNodeId: '', deltaX: 0, deltaY: 0, x: 0, y: 0 };
                }

                const overlaps = (left, right) => {
                    const overlapsX = Math.abs((left?.x || 0) - (right?.x || 0)) < (((left?.sceneWidth || 0) + (right?.sceneWidth || 0)) / 2);
                    const overlapsY = Math.abs((left?.y || 0) - (right?.y || 0)) < (((left?.sceneHeight || 0) + (right?.sceneHeight || 0)) / 2);
                    const overlapsZ = Math.abs((left?.z || 0) - (right?.z || 0)) < (((left?.sceneDepth || 0) + (right?.sceneDepth || 0)) / 2);
                    return overlapsX && overlapsY && overlapsZ;
                };

                const snapshot = runtime.getSceneSnapshot(host);
                const processNodes = (snapshot?.nodes || []).filter(node => !node.kind.includes('role') && !node.kind.includes('branch'));
                const pairCandidates = [];

                for (const source of processNodes) {
                    for (const target of processNodes) {
                        if (source.id === target.id) {
                            continue;
                        }

                        const deltaX = target.x - source.x;
                        const deltaY = target.y - source.y;
                        if (Math.abs(deltaX) < 40 && Math.abs(deltaY) < 40) {
                            continue;
                        }

                        pairCandidates.push({
                            sourceId: source.id,
                            targetId: target.id,
                            deltaX,
                            deltaY,
                            distance: Math.abs(deltaX) + Math.abs(deltaY) + Math.abs(target.z - source.z)
                        });
                    }
                }

                pairCandidates.sort((left, right) => left.distance - right.distance);

                for (const candidate of pairCandidates) {
                    const beforeSnapshot = runtime.getSceneSnapshot(host);
                    const sourceNode = beforeSnapshot?.nodes?.find(node => node.id === candidate.sourceId);
                    const targetNode = beforeSnapshot?.nodes?.find(node => node.id === candidate.targetId);
                    if (!sourceNode || !targetNode) {
                        continue;
                    }

                    const accepted = !!runtime.simulateDrag(host, {
                        nodeId: candidate.sourceId,
                        deltaX: candidate.deltaX,
                        deltaY: candidate.deltaY
                    });
                    const movedNode = runtime.getSceneSnapshot(host)?.nodes?.find(node => node.id === candidate.sourceId);
                    const moved = movedNode &&
                        (Math.abs((movedNode.x || 0) - (sourceNode.x || 0)) > 1 || Math.abs((movedNode.y || 0) - (sourceNode.y || 0)) > 1);

                    if (accepted && moved && movedNode && !overlaps(movedNode, targetNode)) {
                        return {
                            accepted: true,
                            nodeId: candidate.sourceId,
                            targetNodeId: candidate.targetId,
                            deltaX: candidate.deltaX,
                            deltaY: candidate.deltaY,
                            x: movedNode.x || 0,
                            y: movedNode.y || 0
                        };
                    }

                    if (accepted && movedNode) {
                        runtime.simulateDrag(host, {
                            nodeId: candidate.sourceId,
                            deltaX: (sourceNode.x || 0) - (movedNode.x || 0),
                            deltaY: (sourceNode.y || 0) - (movedNode.y || 0)
                        });
                    }
                }

                return { accepted: false, nodeId: '', targetNodeId: '', deltaX: 0, deltaY: 0, x: 0, y: 0 };
            }");
    }

    private static bool NodesOverlap(WebGlSceneNode left, WebGlSceneNode right)
    {
        var overlapsX = Math.Abs(left.X - right.X) < ((left.SceneWidth + right.SceneWidth) / 2d);
        var overlapsY = Math.Abs(left.Y - right.Y) < ((left.SceneHeight + right.SceneHeight) / 2d);
        var overlapsZ = Math.Abs(left.Z - right.Z) < ((left.SceneDepth + right.SceneDepth) / 2d);
        return overlapsX && overlapsY && overlapsZ;
    }

    private static double ResolveSceneClearance(WebGlSceneSnapshot snapshot, WebGlSceneNode node)
    {
        return snapshot.Nodes
            .Where(other => !string.Equals(other.Id, node.Id, StringComparison.Ordinal))
            .Select(other => Math.Abs(other.X - node.X) + Math.Abs(other.Y - node.Y) + Math.Abs(other.Z - node.Z))
            .DefaultIfEmpty(0)
            .Min();
    }

    private sealed class WebGlSceneSnapshot
    {
        public string SceneKey { get; set; } = string.Empty;

        public string ProjectionMode { get; set; } = string.Empty;

        public string LayoutMode { get; set; } = string.Empty;

        public string ToolMode { get; set; } = string.Empty;

        public string NodeInfoMode { get; set; } = string.Empty;

        public double NodeSpacingFactor { get; set; }

        public bool DeterministicMode { get; set; }

        public bool ShowRoleNodes { get; set; }

        public bool ShowBranchNodes { get; set; }

        public List<WebGlSceneNode> Nodes { get; set; } = [];

        public List<WebGlSceneEdge> Edges { get; set; } = [];
    }

    private sealed class WebGlSceneNode
    {
        public string Id { get; set; } = string.Empty;

        public string Kind { get; set; } = string.Empty;

        public string Title { get; set; } = string.Empty;

        public string Subtitle { get; set; } = string.Empty;

        public double X { get; set; }

        public double Y { get; set; }

        public double Z { get; set; }

        public double SceneWidth { get; set; }

        public double SceneHeight { get; set; }

        public double SceneDepth { get; set; }

        public bool Selected { get; set; }
    }

    private sealed class WebGlSceneEdge
    {
        public string Id { get; set; } = string.Empty;

        public string SourceNodeId { get; set; } = string.Empty;

        public string SourceAnchorId { get; set; } = string.Empty;

        public string SourcePortId { get; set; } = string.Empty;

        public string TargetNodeId { get; set; } = string.Empty;

        public string TargetAnchorId { get; set; } = string.Empty;

        public string TargetPortId { get; set; } = string.Empty;

        public string Kind { get; set; } = string.Empty;

        public string CategoryKey { get; set; } = string.Empty;

        public bool IsPrimaryPath { get; set; }

        public double Emphasis { get; set; }

        public double Opacity { get; set; }
    }

    private sealed class WebGlChromeStateProof
    {
        public string ToolMode { get; set; } = string.Empty;

        public string NodeInfoMode { get; set; } = string.Empty;

        public bool SettingsOpen { get; set; }

        public bool ShowRoleNodes { get; set; }

        public bool ShowBranchNodes { get; set; }

        public List<WebGlChromeActionProof> Actions { get; set; } = [];

        public WebGlContextMenuProof ContextMenu { get; set; } = new();
    }

    private sealed class WebGlChromeActionProof
    {
        public string Id { get; set; } = string.Empty;

        public string Label { get; set; } = string.Empty;

        public string Section { get; set; } = string.Empty;

        public double X { get; set; }

        public double Y { get; set; }

        public double Width { get; set; }

        public double Height { get; set; }
    }

    private sealed class WebGlContextMenuProof
    {
        public string Title { get; set; } = string.Empty;

        public double X { get; set; }

        public double Y { get; set; }

        public double Width { get; set; }

        public double Height { get; set; }

        public List<WebGlChromeActionProof> Items { get; set; } = [];
    }

    private sealed class WebGlDragProbeResult
    {
        public bool Accepted { get; set; }

        public string NodeId { get; set; } = string.Empty;

        public string TargetNodeId { get; set; } = string.Empty;

        public double DeltaX { get; set; }

        public double DeltaY { get; set; }

        public double X { get; set; }

        public double Y { get; set; }
    }

    private sealed class WebGlViewportPointProof
    {
        public double X { get; set; }

        public double Y { get; set; }
    }

    private sealed class WebGlExplicitConnectionCandidateProof
    {
        public string SourceNodeId { get; set; } = string.Empty;

        public string SourceNodeTitle { get; set; } = string.Empty;

        public string SourceAnchorId { get; set; } = string.Empty;

        public string SourceAnchorLabel { get; set; } = string.Empty;

        public string TargetNodeId { get; set; } = string.Empty;

        public string TargetNodeTitle { get; set; } = string.Empty;

        public string TargetAnchorId { get; set; } = string.Empty;

        public string TargetAnchorLabel { get; set; } = string.Empty;
    }

    private sealed class WebGlEdgeContextMenuProbeProof
    {
        public string EdgeId { get; set; } = string.Empty;

        public double X { get; set; }

        public double Y { get; set; }
    }

    private sealed class WebGlUiStateProof
    {
        public WebGlCameraStateProof Camera { get; set; } = new();
    }

    private sealed class WebGlCameraStateProof
    {
        public string ProjectionMode { get; set; } = string.Empty;

        public double TargetX { get; set; }

        public double TargetY { get; set; }

        public double TargetZ { get; set; }

        public double Zoom { get; set; }

        public double Distance { get; set; }

        public double Azimuth { get; set; }

        public double Polar { get; set; }
    }
}
