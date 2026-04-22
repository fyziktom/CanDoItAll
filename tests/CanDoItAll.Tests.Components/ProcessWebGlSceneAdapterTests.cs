using CanDoItAll.Components.WebGlLib;
using CanDoItAll.Modules.Processes;

namespace CanDoItAll.Tests.Components;

public sealed class ProcessWebGlSceneAdapterTests
{
    [Fact]
    public void Representative_templates_match_bundle_lock_and_reuse_stable_projected_ids()
    {
        var adapter = CreateAdapter();

        var templates = adapter.ListRepresentativeTemplates();

        Assert.Collection(
            templates,
            template => Assert.Equal("customer-onboarding", template.Key),
            template => Assert.Equal("architecture-decision-governance", template.Key),
            template => Assert.Equal("branching-code-review", template.Key),
            template => Assert.Equal("software-delivery", template.Key),
            template => Assert.Equal("ai-assisted-change-delivery", template.Key),
            template => Assert.Equal("release-readiness-and-deployment", template.Key));

        var first = adapter.LoadProjectedDefinition("architecture-decision-governance");
        var second = adapter.LoadProjectedDefinition("architecture-decision-governance");

        Assert.Equal(first.Id, second.Id);
        Assert.Equal(first.WorkingVersionId, second.WorkingVersionId);
        Assert.Equal(
            first.Roles.Select(role => (role.Key, role.Id)).OrderBy(item => item.Key, StringComparer.Ordinal).ToArray(),
            second.Roles.Select(role => (role.Key, role.Id)).OrderBy(item => item.Key, StringComparer.Ordinal).ToArray());
        Assert.Equal(
            first.Steps.Select(step => (step.Key, step.Id)).OrderBy(item => item.Key, StringComparer.Ordinal).ToArray(),
            second.Steps.Select(step => (step.Key, step.Id)).OrderBy(item => item.Key, StringComparer.Ordinal).ToArray());
    }

    [Fact]
    public void Scene_mapping_projects_generic_nodes_edges_and_anchor_contracts()
    {
        var adapter = CreateAdapter();
        var editor = adapter.LoadProjectedDefinition("branching-code-review");

        var surface = adapter.BuildDefinitionScene(
            editor,
            new ProcessWebGlSceneOptions(
                TemplateKey: "branching-code-review",
                ProjectionMode: WebGlWorkbenchProjectionModes.Perspective,
                CameraViewMode: WebGlWorkbenchCameraViewModes.Perspective,
                ViewPreset: WebGlWorkbenchViewPresets.Dependencies,
                SelectedNodeId: null,
                DeterministicMode: true,
                ShowDiagnostics: true));

        Assert.Equal("branching-code-review", surface.SceneKey);
        Assert.NotEmpty(surface.Nodes);
        Assert.NotEmpty(surface.Edges);
        Assert.Contains(surface.Nodes, node => node.Kind.Contains("role", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(surface.Nodes, node => node.Kind.Contains("branch", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(WebGlWorkbenchProjectionModes.Perspective, surface.UiState.Camera.ProjectionMode);
        Assert.Equal(WebGlWorkbenchCameraViewModes.Perspective, surface.UiState.Camera.ViewMode);
        Assert.Equal(WebGlWorkbenchViewPresets.Dependencies, surface.UiState.ActiveViewPreset);
        Assert.Equal(WebGlWorkbenchLayoutModes.CenterLane, surface.UiState.LayoutMode);
        Assert.Equal(1, surface.UiState.NodeSpacingFactor);
        Assert.True(surface.UiState.DeterministicMode);
        Assert.True(surface.UiState.ShowDiagnostics);
        Assert.Contains(surface.Edges, edge => edge.IsPrimaryPath && edge.Emphasis > 1.4d && edge.Opacity > 0.9d);
        Assert.Contains(surface.Edges, edge => !edge.IsPrimaryPath && edge.Emphasis < 1d && edge.Opacity < 0.8d);

        Assert.All(surface.Nodes, node =>
        {
            Assert.False(string.IsNullOrWhiteSpace(node.Id));
            Assert.NotEmpty(node.Anchors);
        });
        Assert.All(surface.Nodes.SelectMany(node => node.Anchors), anchor =>
        {
            Assert.StartsWith(anchor.NodeId + "::", anchor.Id, StringComparison.Ordinal);
            Assert.False(string.IsNullOrWhiteSpace(anchor.PortId));
            Assert.True(anchor.TotalOnSide >= 1);
        });
        Assert.All(surface.Edges, edge =>
        {
            Assert.False(string.IsNullOrWhiteSpace(edge.SourceAnchorId));
            Assert.False(string.IsNullOrWhiteSpace(edge.TargetAnchorId));
            Assert.False(string.IsNullOrWhiteSpace(edge.SourcePortId));
            Assert.False(string.IsNullOrWhiteSpace(edge.TargetPortId));
        });
    }

    [Fact]
    public void Scene_mapping_keeps_main_lane_centered_and_roles_spread_around_it()
    {
        var adapter = CreateAdapter();
        var editor = adapter.LoadProjectedDefinition("branching-code-review");

        var surface = adapter.BuildDefinitionScene(
            editor,
            new ProcessWebGlSceneOptions(
                TemplateKey: "branching-code-review",
                ProjectionMode: WebGlWorkbenchProjectionModes.Perspective,
                CameraViewMode: WebGlWorkbenchCameraViewModes.Perspective,
                ViewPreset: WebGlWorkbenchViewPresets.Overview,
                SelectedNodeId: null,
                DeterministicMode: true,
                ShowDiagnostics: false));

        var roleNodes = surface.Nodes
            .Where(node => node.Kind.Contains("role", StringComparison.OrdinalIgnoreCase))
            .ToList();
        var processLaneNodes = surface.Nodes
            .Where(node => !node.Kind.Contains("role", StringComparison.OrdinalIgnoreCase))
            .ToList();
        var stepNodes = processLaneNodes
            .Where(node => !node.Kind.Contains("branch", StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.NotEmpty(roleNodes);
        Assert.NotEmpty(processLaneNodes);
        Assert.NotEmpty(stepNodes);

        Assert.InRange(stepNodes.Average(node => node.X), -140, 140);
        Assert.True(
            roleNodes.Average(node => Math.Abs(node.X)) >
            processLaneNodes.Average(node => Math.Abs(node.X)) + 180);
        Assert.True(stepNodes.Max(node => node.Z) - stepNodes.Min(node => node.Z) > 900);
        Assert.True(roleNodes.Max(node => node.Y) - roleNodes.Min(node => node.Y) > 60);
    }

    [Fact]
    public void Scene_mapping_supports_multiple_recompose_algorithms_and_spacing_variants()
    {
        var adapter = CreateAdapter();
        var baselineSurface = adapter.BuildDefinitionScene(
            adapter.LoadProjectedDefinition("branching-code-review"),
            new ProcessWebGlSceneOptions(
                TemplateKey: "branching-code-review",
                ProjectionMode: WebGlWorkbenchProjectionModes.Perspective,
                CameraViewMode: WebGlWorkbenchCameraViewModes.Perspective,
                ViewPreset: WebGlWorkbenchViewPresets.Overview,
                SelectedNodeId: null,
                LayoutMode: WebGlWorkbenchLayoutModes.CenterLane,
                NodeSpacingFactor: 1));
        var arcSurface = adapter.BuildDefinitionScene(
            adapter.LoadProjectedDefinition("branching-code-review"),
            new ProcessWebGlSceneOptions(
                TemplateKey: "branching-code-review",
                ProjectionMode: WebGlWorkbenchProjectionModes.Perspective,
                CameraViewMode: WebGlWorkbenchCameraViewModes.Perspective,
                ViewPreset: WebGlWorkbenchViewPresets.Overview,
                SelectedNodeId: null,
                LayoutMode: WebGlWorkbenchLayoutModes.AlternatingArc,
                NodeSpacingFactor: 1));
        var orbitSurface = adapter.BuildDefinitionScene(
            adapter.LoadProjectedDefinition("branching-code-review"),
            new ProcessWebGlSceneOptions(
                TemplateKey: "branching-code-review",
                ProjectionMode: WebGlWorkbenchProjectionModes.Perspective,
                CameraViewMode: WebGlWorkbenchCameraViewModes.Perspective,
                ViewPreset: WebGlWorkbenchViewPresets.Overview,
                SelectedNodeId: null,
                LayoutMode: WebGlWorkbenchLayoutModes.LayeredOrbit,
                NodeSpacingFactor: 1.35d));
        var spineSurface = adapter.BuildDefinitionScene(
            adapter.LoadProjectedDefinition("branching-code-review"),
            new ProcessWebGlSceneOptions(
                TemplateKey: "branching-code-review",
                ProjectionMode: WebGlWorkbenchProjectionModes.Perspective,
                CameraViewMode: WebGlWorkbenchCameraViewModes.Perspective,
                ViewPreset: WebGlWorkbenchViewPresets.Overview,
                SelectedNodeId: null,
                LayoutMode: WebGlWorkbenchLayoutModes.CriticalPathSpine,
                NodeSpacingFactor: 1));
        var corridorSurface = adapter.BuildDefinitionScene(
            adapter.LoadProjectedDefinition("branching-code-review"),
            new ProcessWebGlSceneOptions(
                TemplateKey: "branching-code-review",
                ProjectionMode: WebGlWorkbenchProjectionModes.Perspective,
                CameraViewMode: WebGlWorkbenchCameraViewModes.Perspective,
                ViewPreset: WebGlWorkbenchViewPresets.Overview,
                SelectedNodeId: null,
                LayoutMode: WebGlWorkbenchLayoutModes.FanoutCorridor,
                NodeSpacingFactor: 1.1d));
        var radialSurface = adapter.BuildDefinitionScene(
            adapter.LoadProjectedDefinition("branching-code-review"),
            new ProcessWebGlSceneOptions(
                TemplateKey: "branching-code-review",
                ProjectionMode: WebGlWorkbenchProjectionModes.Perspective,
                CameraViewMode: WebGlWorkbenchCameraViewModes.Perspective,
                ViewPreset: WebGlWorkbenchViewPresets.Overview,
                SelectedNodeId: null,
                LayoutMode: WebGlWorkbenchLayoutModes.RadialBurst,
                NodeSpacingFactor: 1.2d));

        var trackedNodeId = baselineSurface.Nodes.First(node =>
            !node.Kind.Contains("role", StringComparison.OrdinalIgnoreCase) &&
            !node.Kind.Contains("branch", StringComparison.OrdinalIgnoreCase)).Id;
        var baselineNode = baselineSurface.Nodes.First(node => node.Id == trackedNodeId);
        var arcNode = arcSurface.Nodes.First(node => node.Id == trackedNodeId);
        var orbitNode = orbitSurface.Nodes.First(node => node.Id == trackedNodeId);
        var spineNode = spineSurface.Nodes.First(node => node.Id == trackedNodeId);
        var corridorNode = corridorSurface.Nodes.First(node => node.Id == trackedNodeId);
        var radialNode = radialSurface.Nodes.First(node => node.Id == trackedNodeId);
        var busiestNodeId = baselineSurface.Nodes
            .OrderByDescending(node => baselineSurface.Edges.Count(edge =>
                string.Equals(edge.SourceNodeId, node.Id, StringComparison.Ordinal) ||
                string.Equals(edge.TargetNodeId, node.Id, StringComparison.Ordinal)))
            .First()
            .Id;

        Assert.Equal(WebGlWorkbenchLayoutModes.CenterLane, baselineSurface.UiState.LayoutMode);
        Assert.Equal(WebGlWorkbenchLayoutModes.AlternatingArc, arcSurface.UiState.LayoutMode);
        Assert.Equal(WebGlWorkbenchLayoutModes.LayeredOrbit, orbitSurface.UiState.LayoutMode);
        Assert.Equal(WebGlWorkbenchLayoutModes.CriticalPathSpine, spineSurface.UiState.LayoutMode);
        Assert.Equal(WebGlWorkbenchLayoutModes.FanoutCorridor, corridorSurface.UiState.LayoutMode);
        Assert.Equal(WebGlWorkbenchLayoutModes.RadialBurst, radialSurface.UiState.LayoutMode);
        Assert.NotEqual((baselineNode.X, baselineNode.Y, baselineNode.Z), (arcNode.X, arcNode.Y, arcNode.Z));
        Assert.NotEqual((arcNode.X, arcNode.Y, arcNode.Z), (orbitNode.X, orbitNode.Y, orbitNode.Z));
        Assert.NotEqual((baselineNode.X, baselineNode.Y, baselineNode.Z), (spineNode.X, spineNode.Y, spineNode.Z));
        Assert.NotEqual((spineNode.X, spineNode.Y, spineNode.Z), (corridorNode.X, corridorNode.Y, corridorNode.Z));
        Assert.NotEqual((corridorNode.X, corridorNode.Y, corridorNode.Z), (radialNode.X, radialNode.Y, radialNode.Z));
        Assert.True(
            orbitSurface.Nodes.Where(node => node.Kind.Contains("role", StringComparison.OrdinalIgnoreCase)).Average(node => Math.Abs(node.X)) >
            baselineSurface.Nodes.Where(node => node.Kind.Contains("role", StringComparison.OrdinalIgnoreCase)).Average(node => Math.Abs(node.X)));
        Assert.True(ResolveNearestNodeDistance(spineSurface, busiestNodeId) > ResolveNearestNodeDistance(baselineSurface, busiestNodeId));
        Assert.True(ResolveNearestNodeDistance(corridorSurface, busiestNodeId) > ResolveNearestNodeDistance(baselineSurface, busiestNodeId));
        Assert.True(radialSurface.Nodes.Max(node => Math.Abs(node.X)) > baselineSurface.Nodes.Max(node => Math.Abs(node.X)) + 120);
        Assert.Equal(1.35d, orbitSurface.UiState.NodeSpacingFactor);
    }

    [Theory]
    [InlineData(WebGlWorkbenchCameraViewModes.Perspective, WebGlWorkbenchProjectionModes.Perspective)]
    [InlineData(WebGlWorkbenchCameraViewModes.XY, WebGlWorkbenchProjectionModes.Orthographic)]
    [InlineData(WebGlWorkbenchCameraViewModes.XZ, WebGlWorkbenchProjectionModes.Orthographic)]
    [InlineData(WebGlWorkbenchCameraViewModes.YZ, WebGlWorkbenchProjectionModes.Orthographic)]
    public void Scene_mapping_normalizes_camera_view_modes(string cameraViewMode, string expectedProjectionMode)
    {
        var adapter = CreateAdapter();

        var surface = adapter.BuildDefinitionScene(
            adapter.LoadProjectedDefinition("customer-onboarding"),
            new ProcessWebGlSceneOptions(
                TemplateKey: "customer-onboarding",
                ProjectionMode: expectedProjectionMode,
                CameraViewMode: cameraViewMode,
                ViewPreset: WebGlWorkbenchViewPresets.Overview,
                SelectedNodeId: null));

        Assert.Equal(expectedProjectionMode, surface.UiState.Camera.ProjectionMode);
        Assert.Equal(cameraViewMode, surface.UiState.Camera.ViewMode);
    }

    private static double ResolveNearestNodeDistance(WebGlWorkbenchSurface surface, string nodeId)
    {
        var source = surface.Nodes.First(node => string.Equals(node.Id, nodeId, StringComparison.Ordinal));
        return surface.Nodes
            .Where(node => !string.Equals(node.Id, nodeId, StringComparison.Ordinal))
            .Select(node => Math.Sqrt(
                Math.Pow(node.X - source.X, 2) +
                Math.Pow(node.Z - source.Z, 2)))
            .DefaultIfEmpty(0d)
            .Min();
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
