using Bunit;
using CanDoItAll.Components.WebGlLib;
using Microsoft.AspNetCore.Components;

namespace CanDoItAll.Tests.Components;

public sealed class WebGlWorkbenchInteropTests
{
    [Fact]
    public void Workbench_creates_runtime_and_updates_when_surface_changes()
    {
        using var context = new TestContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;

        var createInterop = context.JSInterop.Setup<bool>("CanDoItAll.webglWorkbench.create", _ => true);
        createInterop.SetResult(true);
        var updateInterop = context.JSInterop.Setup<bool>("CanDoItAll.webglWorkbench.update", _ => true);
        updateInterop.SetResult(true);

        var cut = context.RenderComponent<WebGlWorkbench>(parameters =>
            parameters.Add(component => component.Surface, CreateSurface("customer-onboarding", 140, 180)));

        cut.WaitForAssertion(() =>
        {
            Assert.Single(createInterop.Invocations);
            Assert.Empty(updateInterop.Invocations);
        });

        Assert.Contains("data-webgl-surface-id=\"customer-onboarding:surface\"", cut.Markup, StringComparison.Ordinal);

        cut.SetParametersAndRender(parameters =>
            parameters.Add(component => component.Surface, CreateSurface("branching-code-review", 320, 260)));

        cut.WaitForAssertion(() =>
        {
            Assert.Single(createInterop.Invocations);
            Assert.Single(updateInterop.Invocations);
        });
    }

    [Fact]
    public async Task Workbench_forwards_host_chrome_actions_to_blazor()
    {
        using var context = new TestContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;

        var actions = new List<string>();
        var cut = context.RenderComponent<WebGlWorkbench>(parameters =>
            parameters
                .Add(component => component.Surface, CreateSurface("customer-onboarding", 140, 180))
                .Add(component => component.ChromeActionRequested, EventCallback.Factory.Create<string>(this, action => actions.Add(action))));

        await cut.InvokeAsync(() => cut.Instance.OnChromeActionRequested("host:show-selection-window"));

        Assert.Equal("host:show-selection-window", Assert.Single(actions));
    }

    private static WebGlWorkbenchSurface CreateSurface(string sceneKey, double x, double y)
    {
        var node = new WebGlWorkbenchNode
        {
            Id = $"{sceneKey}:node",
            Kind = "process-definition-step",
            Family = "process-definition",
            Title = "Projected node",
            Subtitle = "Shared runtime proof",
            X = x,
            Y = y,
            Z = 6,
            Width = 220,
            Height = 124,
            Depth = 28,
            Anchors =
            [
                new WebGlWorkbenchAnchor
                {
                    Id = $"{sceneKey}:node::input",
                    NodeId = $"{sceneKey}:node",
                    PortId = "input",
                    Label = "Input",
                    Role = WebGlWorkbenchAnchorRoles.Input,
                    Side = "left",
                    Order = 0,
                    TotalOnSide = 1
                },
                new WebGlWorkbenchAnchor
                {
                    Id = $"{sceneKey}:node::output",
                    NodeId = $"{sceneKey}:node",
                    PortId = "output",
                    Label = "Output",
                    Role = WebGlWorkbenchAnchorRoles.Output,
                    Side = "right",
                    Order = 0,
                    TotalOnSide = 1
                }
            ]
        };

        return new WebGlWorkbenchSurface
        {
            SurfaceId = $"{sceneKey}:surface",
            SceneKey = sceneKey,
            Title = "WebGL proof surface",
            Nodes = [node],
            UiState = new WebGlWorkbenchUiState
            {
                SelectedNodeIds = [node.Id],
                ActiveViewPreset = WebGlWorkbenchViewPresets.Overview,
                DeterministicMode = true,
                Camera = new WebGlWorkbenchCameraState
                {
                    ProjectionMode = WebGlWorkbenchProjectionModes.Orthographic,
                    Zoom = 1,
                    TargetX = 0,
                    TargetY = 0,
                    TargetZ = 920
                }
            }
        };
    }
}
