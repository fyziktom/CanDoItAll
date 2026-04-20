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

        await page.GetByLabel("Template").SelectOptionAsync("2");

        var denseScene = await WaitForSnapshotAsync(
            page,
            snapshot =>
                string.Equals(snapshot.SceneKey, "branching-code-review", StringComparison.Ordinal) &&
                snapshot.Nodes.Count > initialScene.Nodes.Count &&
                snapshot.Edges.Count >= initialScene.Edges.Count,
            "dense representative template");

        Assert.Contains(denseScene.Nodes, node => node.Kind.Contains("role", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(denseScene.Nodes, node => node.Kind.Contains("branch", StringComparison.OrdinalIgnoreCase));
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
        var targetNode = initialScene.Nodes.First(node =>
            !node.Kind.Contains("role", StringComparison.OrdinalIgnoreCase) &&
            !node.Kind.Contains("branch", StringComparison.OrdinalIgnoreCase));

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

        await page.WaitForTimeoutAsync(200);
        var focusedState = await ReadUiStateAsync(page);

        var dragAccepted = await page.EvaluateAsync<bool>(
            @"request => {
                const host = document.querySelector('.wgl-workbench-host');
                const runtime = window.CanDoItAll?.webglWorkbench;
                return !!runtime?.simulateDrag(host, request);
            }",
            new
            {
                nodeId = targetNode.Id,
                deltaX = 132,
                deltaY = 58
            });
        Assert.True(dragAccepted, $"Expected simulateDrag to accept node '{targetNode.Id}'.");

        var afterDrag = await WaitForSnapshotAsync(
            page,
            snapshot =>
            {
                var movedNode = snapshot.Nodes.FirstOrDefault(node => string.Equals(node.Id, targetNode.Id, StringComparison.Ordinal));
                return movedNode is not null &&
                    Math.Abs(movedNode.X - (targetNode.X + 132)) < 0.5 &&
                    Math.Abs(movedNode.Y - (targetNode.Y + 58)) < 0.5;
            },
            "dragged node to persist through rerender");
        var afterDragState = await ReadUiStateAsync(page);

        Assert.True(Math.Abs(afterDragState.Camera.TargetX - focusedState.Camera.TargetX) < 0.1);
        Assert.True(Math.Abs(afterDragState.Camera.TargetY - focusedState.Camera.TargetY) < 0.1);
        Assert.True(Math.Abs(afterDragState.Camera.TargetZ - focusedState.Camera.TargetZ) < 0.1);
        Assert.True(Math.Abs(afterDragState.Camera.Zoom - focusedState.Camera.Zoom) < 0.01);
        Assert.True(Math.Abs(afterDragState.Camera.Distance - focusedState.Camera.Distance) < 0.5);
        Assert.True(Math.Abs(afterDragState.Camera.Azimuth - focusedState.Camera.Azimuth) < 0.01);
        Assert.True(Math.Abs(afterDragState.Camera.Polar - focusedState.Camera.Polar) < 0.01);

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
    public async Task Sandbox_overlay_navigation_controls_drive_perspective_camera()
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
            "branching template for navigation controls");

        var initialState = await ReadUiStateAsync(page);
        Assert.Equal("perspective", initialState.Camera.ProjectionMode);

        await page.GetByTestId("webgl-stage-orbit-right").ClickAsync();
        var orbitState = await WaitForUiStateAsync(
            page,
            state => Math.Abs(state.Camera.Azimuth - initialState.Camera.Azimuth) > 0.08,
            "orbit control to change azimuth");

        await page.GetByTestId("webgl-stage-zoom-in").ClickAsync();
        var zoomedState = await WaitForUiStateAsync(
            page,
            state => state.Camera.Distance < orbitState.Camera.Distance - 10,
            "zoom control to reduce camera distance");

        await page.GetByTestId("webgl-stage-pan-right").ClickAsync();
        var pannedState = await WaitForUiStateAsync(
            page,
            state =>
                Math.Abs(state.Camera.TargetX - zoomedState.Camera.TargetX) > 1 ||
                Math.Abs(state.Camera.TargetY - zoomedState.Camera.TargetY) > 1 ||
                Math.Abs(state.Camera.TargetZ - zoomedState.Camera.TargetZ) > 1,
            "pan control to move the camera target");

        await page.Locator("[data-testid='webgl-sandbox-stage']").ScreenshotAsync(new LocatorScreenshotOptions
        {
            Path = Path.Combine(artifactsDir, "06-webgl-3d-navigation-overlay.png")
        });

        await page.GetByTestId("webgl-stage-reset-view").ClickAsync();
        var resetState = await WaitForUiStateAsync(
            page,
            state =>
                Math.Abs(state.Camera.TargetX - initialState.Camera.TargetX) < 1 &&
                Math.Abs(state.Camera.TargetY - initialState.Camera.TargetY) < 1 &&
                Math.Abs(state.Camera.TargetZ - initialState.Camera.TargetZ) < 1 &&
                Math.Abs(state.Camera.Distance - initialState.Camera.Distance) < 5 &&
                Math.Abs(state.Camera.Azimuth - initialState.Camera.Azimuth) < 0.05 &&
                Math.Abs(state.Camera.Polar - initialState.Camera.Polar) < 0.05,
            "reset control to restore the fitted camera");

        Assert.True(Math.Abs(resetState.Camera.Azimuth - pannedState.Camera.Azimuth) > 0.08);
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

    private sealed class WebGlSceneSnapshot
    {
        public string SceneKey { get; set; } = string.Empty;

        public string ProjectionMode { get; set; } = string.Empty;

        public bool DeterministicMode { get; set; }

        public List<WebGlSceneNode> Nodes { get; set; } = [];

        public List<WebGlSceneEdge> Edges { get; set; } = [];
    }

    private sealed class WebGlSceneNode
    {
        public string Id { get; set; } = string.Empty;

        public string Kind { get; set; } = string.Empty;

        public double X { get; set; }

        public double Y { get; set; }

        public double Z { get; set; }
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
