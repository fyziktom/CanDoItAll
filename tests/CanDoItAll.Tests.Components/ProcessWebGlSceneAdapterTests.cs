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
            template => Assert.Equal("branching-code-review", template.Key));

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
        Assert.Equal(WebGlWorkbenchViewPresets.Dependencies, surface.UiState.ActiveViewPreset);
        Assert.True(surface.UiState.DeterministicMode);
        Assert.True(surface.UiState.ShowDiagnostics);

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

    private static ProcessWebGlSceneAdapter CreateAdapter()
    {
        var packLoader = new ProcessTemplatePackLoader();
        return new ProcessWebGlSceneAdapter(
            new ProcessTemplateCatalogService(packLoader),
            new ProcessTemplateProjectionService(packLoader),
            new ProcessCanvasSurfaceFactory(new ProcessCanvasChromeCatalogService(packLoader)));
    }
}
