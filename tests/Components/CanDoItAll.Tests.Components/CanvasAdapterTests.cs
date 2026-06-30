using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Modules.Factory;
using CanDoItAll.Modules.Factory.CanvasAdapters;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workbench.CanvasAdapters;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Tests.Components;

public sealed class CanvasAdapterTests
{
    [Fact]
    public void Project_structure_graph_adapter_projects_canvas_nodes_and_links()
    {
        var rootNode = new ProjectStructureNode(
            "root",
            null,
            ProjectObjectType.ProjectRoot,
            string.Empty,
            "Project",
            "Root",
            "Ready",
            "Project root",
            "/projects/1/structure",
            "Project",
            null,
            string.Empty,
            string.Empty,
            string.Empty,
            0,
            0,
            new ProjectObjectVisualProfile("hex", "#0f172a", "PR", "Project"),
            ["Scheduled"],
            "complete",
            100,
            string.Empty,
            string.Empty,
            string.Empty,
            [],
            0);
        var noteNode = new ProjectStructureNode(
            "note-1",
            "root",
            ProjectObjectType.Note,
            string.Empty,
            "Architecture note",
            string.Empty,
            "Blocked",
            "Shared canvas adapter",
            "/projects/1/structure",
            "Note",
            null,
            string.Empty,
            string.Empty,
            string.Empty,
            120,
            160,
            new ProjectObjectVisualProfile("pill", "#059669", "NT", "Note"),
            [],
            "progress",
            25,
            string.Empty,
            string.Empty,
            string.Empty,
            [],
            0);

        var surface = new ProjectStructureSurface(
            Guid.NewGuid(),
            "Structure Project",
            [rootNode, noteNode],
            [new ProjectStructureLink("root", "note-1", ProjectObjectLinkKind.Contains, false)],
            null);
        var actionCatalog = new ProjectStructureActionCatalogAdapter();
        var adapter = new ProjectStructureGraphAdapter();
        var uiState = new CanvasWorkbenchUiState
        {
            SelectedNodeIds = ["note-1"]
        };

        var canvasSurface = adapter.BuildSurface(
            surface,
            uiState,
            new CanvasWorkbenchChrome
            {
                QuickCreateActions = actionCatalog.BuildQuickCreateActions(ProjectObjectType.Note).ToList(),
                GroupContextActions = actionCatalog.BuildGroupContextActions().ToList()
            },
            actionCatalog);

        var note = Assert.Single(canvasSurface.Nodes, node => node.Id == "note-1");
        Assert.True(note.IsInlineTextNode);
        Assert.Contains(note.Annotations, annotation => string.Equals(annotation.Kind, "health", StringComparison.Ordinal) && string.Equals(annotation.ActionId, "summary", StringComparison.Ordinal));
        Assert.Contains(note.ContextActions, action => string.Equals(action.ActionId, "open", StringComparison.Ordinal));
        Assert.True(canvasSurface.Chrome.Diagnostics.IsEnabled);
        Assert.True(canvasSurface.Chrome.Minimap.IsEnabled);
        Assert.True(canvasSurface.Chrome.Clipboard.IsEnabled);
        Assert.Single(canvasSurface.Links);
    }

    [Fact]
    public void Project_structure_placement_policy_places_sibling_below_source()
    {
        var sourceNode = new ProjectStructureNode(
            "source",
            "parent",
            ProjectObjectType.Note,
            string.Empty,
            "Source",
            string.Empty,
            "Draft",
            string.Empty,
            "/projects/1/structure",
            "Note",
            null,
            string.Empty,
            string.Empty,
            string.Empty,
            300,
            200,
            new ProjectObjectVisualProfile("pill", "#059669", "NT", "Note"),
            [],
            "progress",
            0,
            string.Empty,
            string.Empty,
            string.Empty,
            [],
            0);
        var siblingNode = sourceNode with { Id = "sibling" };
        var request = new CanvasWorkbenchCreateActionRequest(
            "add-note",
            "source",
            0,
            0,
            null,
            string.Empty,
            string.Empty,
            string.Empty,
            "sibling",
            "command",
            string.Empty,
            null);

        var placement = new ProjectStructurePlacementPolicy().ResolveCreatePlacement([sourceNode, siblingNode], sourceNode, null, request);

        Assert.Equal(300, placement.X);
        Assert.Equal(312, placement.Y);
    }

    [Fact]
    public void Prompt_factory_session_graph_adapter_builds_selection_and_run_nodes()
    {
        var blockId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var runNodeId = Guid.NewGuid();
        var block = new PromptBlockSummary(
            blockId,
            "mission-block",
            "mission-scope",
            "Mission scope",
            PromptBlockKind.Instruction,
            "Scope the mission",
            true,
            true,
            ["review"],
            [],
            [],
            ["scope"],
            [],
            ["goal"],
            "Mission: {{goal}}",
            "Mission preview",
            1,
            "catalog");
        var group = new PromptLibraryGroupSummary(
            "mission-scope",
            "Mission",
            "Mission building blocks",
            "Mission framing",
            "default",
            1,
            1,
            [block]);
        var adapter = new PromptFactorySessionGraphAdapter();
        var uiState = adapter.BuildUiState(new CanvasWorkbenchUiState().ToJson(), ["node:" + runNodeId.ToString("N")], "review");
        var request = new PromptFactorySessionGraphRequest(
            new PromptFactoryEditorModel
            {
                SessionId = sessionId,
                SessionName = "Review session",
                Phase = "Review",
                BlueprintId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                FlowTemplateId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                SelectedBlockIds = [blockId],
                Warnings = ["Provider profile is still missing."],
                WarningSummary = "Provider profile is still missing.",
                ComponentCustomizations =
                [
                    new PromptSessionComponentCustomization
                    {
                        BlockId = blockId,
                        BlockKey = block.Key,
                        Name = block.Name,
                        TemplateValues = [new PromptTemplateValue { Key = "goal", Value = "Ship safely" }],
                        RenderedContent = "Mission: Ship safely"
                    }
                ],
                SessionAttachments =
                [
                    new PromptSessionAttachmentSummary
                    {
                        Id = "attach-1",
                        Kind = "note",
                        Title = "Checklist",
                        Subtitle = "Review",
                        Notes = "Use during review."
                    }
                ],
                Nodes =
                [
                    new PromptRunNodeSummary(runNodeId, "Review step", "main", "Main", 1, PromptRunNodeState.Prepared, null, null, null, "Inspect the result.")
                ]
            },
            new PromptLibraryCatalogSummary(
                [group],
                [new PromptFlowTemplateSummary(Guid.Parse("22222222-2222-2222-2222-222222222222"), "review-flow", "Review Flow", "Review the output", [blockId], [block.Key], ["review"], [], 1, "catalog")],
                [new PromptBlueprintSummary(Guid.Parse("11111111-1111-1111-1111-111111111111"), "review-blueprint", "Review Blueprint", "review", "Blueprint summary", "Blueprint guidance", Guid.Parse("22222222-2222-2222-2222-222222222222"), "review-flow", [block.Key], 1, "catalog")],
                1,
                1,
                1),
            [new PromptBlueprintSummary(Guid.Parse("11111111-1111-1111-1111-111111111111"), "review-blueprint", "Review Blueprint", "review", "Blueprint summary", "Blueprint guidance", Guid.Parse("22222222-2222-2222-2222-222222222222"), "review-flow", [block.Key], 1, "catalog")],
            [new PromptFlowTemplateSummary(Guid.Parse("22222222-2222-2222-2222-222222222222"), "review-flow", "Review Flow", "Review the output", [blockId], [block.Key], ["review"], [], 1, "catalog")],
            [block],
            [new PromptSessionAttachmentSummary { Id = "attach-1", Kind = "note", Title = "Checklist", Subtitle = "Review", Notes = "Use during review." }],
            new PromptSessionSetupProfile { WorkRepository = "CanDoItAll" },
            uiState,
            "Repository ready",
            "Review the current session setup.",
            "Setup incomplete",
            false,
            2);

        var surface = adapter.BuildSurface(request);

        var sessionRoot = Assert.Single(surface.Nodes, node => string.Equals(node.Id, "session-root", StringComparison.Ordinal));
        var setup = Assert.Single(surface.Nodes, node => string.Equals(node.Id, "selection:setup", StringComparison.Ordinal));
        var blueprint = Assert.Single(surface.Nodes, node => string.Equals(node.Id, "selection:blueprint", StringComparison.Ordinal));
        var component = Assert.Single(surface.Nodes, node => string.Equals(node.Id, "selection:component:mission-block", StringComparison.Ordinal));
        Assert.Contains(surface.Nodes, node => string.Equals(node.Id, "selection:setup", StringComparison.Ordinal));
        Assert.Contains(surface.Nodes, node => string.Equals(node.Id, "selection:components", StringComparison.Ordinal));
        Assert.Contains(surface.Nodes, node => string.Equals(node.Id, "selection:input:attach-1", StringComparison.Ordinal));
        Assert.Contains(surface.Nodes, node => string.Equals(node.Id, $"node:{runNodeId:N}", StringComparison.Ordinal));
        Assert.Contains(surface.Links, link => string.Equals(link.SourceId, "branch:main", StringComparison.Ordinal) && string.Equals(link.TargetId, $"node:{runNodeId:N}", StringComparison.Ordinal));
        Assert.Contains(sessionRoot.Annotations, annotation => string.Equals(annotation.Kind, "validation", StringComparison.Ordinal));
        Assert.Contains(setup.Annotations, annotation => string.Equals(annotation.Label, "2 missing", StringComparison.Ordinal));
        Assert.Contains(blueprint.Annotations, annotation => string.Equals(annotation.Kind, "recommendation", StringComparison.Ordinal) && string.Equals(annotation.ActionId, "apply-recommendations", StringComparison.Ordinal));
        Assert.Contains(component.Annotations, annotation => string.Equals(annotation.Label, "Recommended", StringComparison.Ordinal));
        Assert.True(surface.Chrome.Diagnostics.IsEnabled);
        Assert.True(surface.Chrome.Minimap.IsEnabled);
        Assert.True(surface.Chrome.Clipboard.IsEnabled);
    }

    [Fact]
    public void Prompt_factory_undo_redo_adapter_restores_canvas_state_and_selected_node()
    {
        var adapter = new PromptFactoryUndoRedoAdapter();
        var nodeId = Guid.NewGuid();
        var original = new PromptFactoryEditorModel
        {
            SessionName = "Original",
            CanvasUiStateJson = new CanvasWorkbenchUiState().ToJson()
        };
        var updated = new PromptFactoryEditorModel
        {
            SessionName = "Updated",
            CanvasUiStateJson = new CanvasWorkbenchUiState().ToJson()
        };

        adapter.Remember(original, ["session-root"], "context", "session-root");

        Assert.True(adapter.TryUndo(updated, [$"node:{nodeId:N}"], "review", $"node:{nodeId:N}", out var snapshot));

        var snapshotUiState = CanvasWorkbenchUiState.Parse(snapshot.CanvasUiStateJson);
        Assert.Equal("Original", snapshot.SessionName);
        Assert.Equal("context", snapshotUiState.ActiveInspectorTab);
        Assert.Null(snapshot.SelectedNodeId);
    }
}


