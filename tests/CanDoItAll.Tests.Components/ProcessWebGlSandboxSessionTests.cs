using CanDoItAll.Components.WebGlLib;
using CanDoItAll.Components.WebGlSandbox;
using CanDoItAll.Modules.Processes;

namespace CanDoItAll.Tests.Components;

public sealed class ProcessWebGlSandboxSessionTests
{
    [Fact]
    public void Session_applies_node_moves_to_in_memory_scene()
    {
        var session = new ProcessWebGlSandboxSession(CreateAdapter());
        session.LoadTemplate("branching-code-review");

        var beforeMove = session.BuildSurface();
        var node = beforeMove.Nodes.First(candidate =>
            !candidate.Kind.Contains("role", StringComparison.OrdinalIgnoreCase) &&
            !candidate.Kind.Contains("branch", StringComparison.OrdinalIgnoreCase));

        session.ApplyNodesMoved(
        [
            new WebGlNodePositionChange(node.Id, node.X + 96, node.Y + 44, node.Z)
        ]);

        var afterMove = session.BuildSurface();
        var movedNode = Assert.Single(afterMove.Nodes, candidate => candidate.Id == node.Id);

        Assert.Equal(node.X + 96, movedNode.X);
        Assert.Equal(node.Y + 44, movedNode.Y);
        Assert.Equal(node.Id, session.SelectedNodeId);
        Assert.Equal("Moved node", session.CommandLog[0].Title);
    }

    [Fact]
    public void Session_disconnects_and_reconnects_existing_semantic_edge()
    {
        var session = new ProcessWebGlSandboxSession(CreateAdapter());
        session.LoadTemplate("branching-code-review");

        var beforeChange = session.BuildSurface();
        var edge = beforeChange.Edges.First(edgeCandidate =>
            ProcessCanvasCatalog.DefinitionPorts.IsStepStructuralOutputPortId(edgeCandidate.SourcePortId) &&
            ProcessCanvasCatalog.DefinitionPorts.IsStepStructuralInputPortId(edgeCandidate.TargetPortId) ||
            string.Equals(edgeCandidate.CategoryKey, ProcessCanvasCatalog.ConnectionCategories.BranchRoute, StringComparison.Ordinal));

        var disconnectRequest = new WebGlConnectionChangeRequest(
            WebGlWorkbenchConnectionActions.Disconnect,
            edge.Id,
            edge.SourceNodeId,
            edge.SourceAnchorId,
            edge.SourcePortId,
            edge.TargetNodeId,
            edge.TargetAnchorId,
            edge.TargetPortId,
            edge.Kind,
            edge.CategoryKey);

        Assert.True(session.ApplyConnectionChange(disconnectRequest));

        var withoutEdge = session.BuildSurface();
        Assert.DoesNotContain(withoutEdge.Edges, candidate => candidate.Id == edge.Id);
        Assert.Equal("Removed connection", session.CommandLog[0].Title);

        var reconnectRequest = disconnectRequest with
        {
            ActionId = WebGlWorkbenchConnectionActions.Connect,
            EdgeId = null
        };

        Assert.True(session.ApplyConnectionChange(reconnectRequest));

        var restored = session.BuildSurface();
        Assert.Contains(restored.Edges, candidate => candidate.Id == edge.Id);
        Assert.Equal("Created connection", session.CommandLog[0].Title);
    }

    [Fact]
    public void Session_preserves_camera_state_across_surface_rebuilds()
    {
        var session = new ProcessWebGlSandboxSession(CreateAdapter());
        session.LoadTemplate("branching-code-review");
        session.ApplyUiState(new WebGlWorkbenchUiState
        {
            Camera = new WebGlWorkbenchCameraState
            {
                ViewMode = WebGlWorkbenchCameraViewModes.Perspective,
                ProjectionMode = WebGlWorkbenchProjectionModes.Perspective,
                Zoom = 1.12,
                TargetX = 120,
                TargetY = -220,
                TargetZ = -160,
                Distance = 860,
                Azimuth = -0.44d,
                Polar = 1.22d
            }
        });

        var beforeMove = session.BuildSurface();
        var node = beforeMove.Nodes.First(candidate =>
            !candidate.Kind.Contains("role", StringComparison.OrdinalIgnoreCase) &&
            !candidate.Kind.Contains("branch", StringComparison.OrdinalIgnoreCase));

        session.ApplyNodesMoved(
        [
            new WebGlNodePositionChange(node.Id, node.X + 32, node.Y + 18, node.Z)
        ]);

        var rebuiltSurface = session.BuildSurface();

        Assert.Equal(WebGlWorkbenchProjectionModes.Perspective, rebuiltSurface.UiState.Camera.ProjectionMode);
        Assert.Equal(WebGlWorkbenchCameraViewModes.Perspective, rebuiltSurface.UiState.Camera.ViewMode);
        Assert.Equal(1.12, rebuiltSurface.UiState.Camera.Zoom);
        Assert.Equal(120, rebuiltSurface.UiState.Camera.TargetX);
        Assert.Equal(-220, rebuiltSurface.UiState.Camera.TargetY);
        Assert.Equal(-160, rebuiltSurface.UiState.Camera.TargetZ);
        Assert.Equal(860, rebuiltSurface.UiState.Camera.Distance);
        Assert.Equal(-0.44d, rebuiltSurface.UiState.Camera.Azimuth);
        Assert.Equal(1.22d, rebuiltSurface.UiState.Camera.Polar);
    }

    [Fact]
    [Theory]
    [InlineData(WebGlWorkbenchCameraViewModes.Perspective, WebGlWorkbenchProjectionModes.Perspective)]
    [InlineData(WebGlWorkbenchCameraViewModes.XY, WebGlWorkbenchProjectionModes.Orthographic)]
    [InlineData(WebGlWorkbenchCameraViewModes.XZ, WebGlWorkbenchProjectionModes.Orthographic)]
    [InlineData(WebGlWorkbenchCameraViewModes.YZ, WebGlWorkbenchProjectionModes.Orthographic)]
    public void Session_applies_camera_view_mode_from_route_state(string cameraViewMode, string expectedProjectionMode)
    {
        var session = new ProcessWebGlSandboxSession(CreateAdapter());

        session.ApplyRouteState("customer-onboarding", cameraViewMode, null);

        var surface = session.BuildSurface();

        Assert.Equal(cameraViewMode, session.CameraViewMode);
        Assert.Equal(expectedProjectionMode, session.ProjectionMode);
        Assert.Equal(expectedProjectionMode, surface.UiState.Camera.ProjectionMode);
        Assert.Equal(cameraViewMode, surface.UiState.Camera.ViewMode);
    }

    [Fact]
    public void Session_defaults_missing_route_camera_to_perspective()
    {
        var session = new ProcessWebGlSandboxSession(CreateAdapter());

        session.ApplyRouteState("customer-onboarding", WebGlWorkbenchCameraViewModes.XY, null);
        session.ApplyRouteState("customer-onboarding", null, null);

        var surface = session.BuildSurface();

        Assert.Equal(WebGlWorkbenchCameraViewModes.Perspective, session.CameraViewMode);
        Assert.Equal(WebGlWorkbenchProjectionModes.Perspective, session.ProjectionMode);
        Assert.Equal(WebGlWorkbenchCameraViewModes.Perspective, surface.UiState.Camera.ViewMode);
        Assert.Equal(WebGlWorkbenchProjectionModes.Perspective, surface.UiState.Camera.ProjectionMode);
    }

    [Fact]
    public void Session_recompose_clears_node_overrides_and_tracks_layout_mode()
    {
        var session = new ProcessWebGlSandboxSession(CreateAdapter());
        session.LoadTemplate("branching-code-review");

        var beforeMove = session.BuildSurface();
        var node = beforeMove.Nodes.First(candidate =>
            !candidate.Kind.Contains("role", StringComparison.OrdinalIgnoreCase) &&
            !candidate.Kind.Contains("branch", StringComparison.OrdinalIgnoreCase));

        session.ApplyNodesMoved(
        [
            new WebGlNodePositionChange(node.Id, node.X + 120, node.Y + 40, node.Z)
        ]);

        session.Recompose(WebGlWorkbenchLayoutModes.AlternatingArc);

        var recomposedSurface = session.BuildSurface();
        var recomposedNode = Assert.Single(recomposedSurface.Nodes, candidate => candidate.Id == node.Id);

        Assert.Equal(WebGlWorkbenchLayoutModes.AlternatingArc, session.LayoutMode);
        Assert.Equal(WebGlWorkbenchLayoutModes.AlternatingArc, recomposedSurface.UiState.LayoutMode);
        Assert.NotEqual(node.X + 120, recomposedNode.X);
        Assert.NotEqual(node.Y + 40, recomposedNode.Y);
        Assert.Equal("Recomposed scene", session.CommandLog[0].Title);
    }

    [Fact]
    public void Session_spacing_adjustments_are_clamped_and_rebuild_scene()
    {
        var session = new ProcessWebGlSandboxSession(CreateAdapter());
        session.LoadTemplate("branching-code-review");

        for (var index = 0; index < 10; index++)
        {
            session.AdjustNodeSpacing(1);
        }

        var expandedSurface = session.BuildSurface();

        Assert.Equal(1.85d, session.NodeSpacingFactor);
        Assert.Equal(1.85d, expandedSurface.UiState.NodeSpacingFactor);

        for (var index = 0; index < 20; index++)
        {
            session.AdjustNodeSpacing(-1);
        }

        var compactSurface = session.BuildSurface();

        Assert.Equal(0.75d, session.NodeSpacingFactor);
        Assert.Equal(0.75d, compactSurface.UiState.NodeSpacingFactor);
        Assert.Equal("Adjusted spacing", session.CommandLog[0].Title);
    }

    [Fact]
    public void Session_applies_tool_and_visibility_settings_from_ui_state()
    {
        var session = new ProcessWebGlSandboxSession(CreateAdapter());
        session.LoadTemplate("branching-code-review");

        session.ApplyUiState(new WebGlWorkbenchUiState
        {
            ToolMode = WebGlWorkbenchToolModes.Connect,
            NodeInfoMode = WebGlWorkbenchNodeInfoModes.Hidden,
            ShowDiagnostics = false,
            ShowGrid = false,
            TransparentGround = false,
            ShowAnchors = false,
            ShowEdgeLabels = false,
            Camera = new WebGlWorkbenchCameraState
            {
                ViewMode = WebGlWorkbenchCameraViewModes.XZ,
                ProjectionMode = WebGlWorkbenchProjectionModes.Orthographic
            }
        });

        var surface = session.BuildSurface();

        Assert.Equal(WebGlWorkbenchToolModes.Connect, surface.UiState.ToolMode);
        Assert.Equal(WebGlWorkbenchNodeInfoModes.Hidden, surface.UiState.NodeInfoMode);
        Assert.False(surface.UiState.ShowDiagnostics);
        Assert.False(surface.UiState.ShowGrid);
        Assert.False(surface.UiState.TransparentGround);
        Assert.False(surface.UiState.ShowAnchors);
        Assert.False(surface.UiState.ShowEdgeLabels);
        Assert.Equal(WebGlWorkbenchCameraViewModes.XZ, surface.UiState.Camera.ViewMode);
        Assert.Equal(WebGlWorkbenchProjectionModes.Orthographic, surface.UiState.Camera.ProjectionMode);
    }

    [Fact]
    public void Session_deletes_node_from_resettable_in_memory_surface()
    {
        var session = new ProcessWebGlSandboxSession(CreateAdapter());
        session.LoadTemplate("branching-code-review");

        var initialSurface = session.BuildSurface();
        var node = initialSurface.Nodes.First(candidate =>
            !candidate.Kind.Contains("role", StringComparison.OrdinalIgnoreCase) &&
            !candidate.Kind.Contains("branch", StringComparison.OrdinalIgnoreCase));

        Assert.True(session.ApplyDeleteRequest(new WebGlDeleteRequest(node.Id, null)));

        var afterDelete = session.BuildSurface();

        Assert.DoesNotContain(afterDelete.Nodes, candidate => candidate.Id == node.Id);
        Assert.DoesNotContain(afterDelete.Edges, candidate =>
            string.Equals(candidate.SourceNodeId, node.Id, StringComparison.Ordinal) ||
            string.Equals(candidate.TargetNodeId, node.Id, StringComparison.Ordinal));
        Assert.Equal("Deleted node", session.CommandLog[0].Title);
    }

    private static ProcessWebGlSceneAdapter CreateAdapter()
    {
        var packLoader = new ProcessTemplatePackLoader();
        return new ProcessWebGlSceneAdapter(
            new ProcessTemplateCatalogService(packLoader),
            new ProcessTemplateProjectionService(packLoader),
            new ProcessCanvasSurfaceFactory(new ProcessCanvasChromeCatalogService(packLoader)));
    }
}
