using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workbench.CanvasAdapters;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Tests.Components;

public sealed class ProjectStructureActionCatalogAdapterTests
{
    [Fact]
    public void Prompt_flow_context_actions_include_wizard_and_create_tools()
    {
        var adapter = new ProjectStructureActionCatalogAdapter();
        var node = CreateNode("flow", ProjectObjectType.PromptFlow, "Flow", 0, 0);

        var actions = adapter.BuildNodeContextActions(node);

        Assert.Contains(actions, action => action.ActionId == "wizard");
        Assert.Contains(actions, action => action.ActionId == "copy-info");
        Assert.Contains(actions, action => action.ActionId == "progress");
        Assert.Contains(actions, action => action.ActionId == "marker");
        Assert.Contains(actions, action => action.ActionId == "priority");
        Assert.Contains(actions, action => action.ActionId.StartsWith("add-", StringComparison.Ordinal));
    }

    [Fact]
    public void Group_context_actions_expose_border_and_shared_status_tools()
    {
        var adapter = new ProjectStructureActionCatalogAdapter();

        var actions = adapter.BuildGroupContextActions();

        Assert.Contains(actions, action => action.ActionId == "group-frame");
        Assert.Contains(actions, action => action.ActionId == "group-clear-frame");
        Assert.Contains(actions, action => action.ActionId == "progress");
        Assert.Contains(actions, action => action.ActionId == "marker");
        Assert.Contains(actions, action => action.ActionId == "priority");
    }

    [Fact]
    public void Quick_create_actions_preserve_requested_root_shortcuts_and_keep_the_layer_unique()
    {
        var adapter = new ProjectStructureActionCatalogAdapter();

        var actions = adapter.BuildQuickCreateActions(null);

        AssertShortcut(actions, "add-note", "n");
        AssertShortcut(actions, "group-blocks", "b");
        AssertShortcut(actions, "group-assets", "a");
        AssertShortcut(actions, "group-people", "p");
        AssertShortcut(actions, "group-infrastructure", "i");
        AssertShortcut(actions, "group-work", "w");
        AssertShortcut(actions, "group-meetings", "q");
        AssertDistinctShortcuts(actions);
    }

    [Fact]
    public void Quick_create_nested_groups_preserve_requested_shortcuts()
    {
        var adapter = new ProjectStructureActionCatalogAdapter();
        var actions = adapter.BuildQuickCreateActions(null);

        var blocks = FindAction(actions, "group-blocks");
        AssertShortcut(blocks.Children, "add-block-delivery", "d");
        AssertShortcut(blocks.Children, "add-block-backlog", "b");
        AssertShortcut(blocks.Children, "add-block-support", "s");
        AssertShortcut(blocks.Children, "add-block-feature", "f");
        AssertDistinctShortcuts(blocks.Children);

        var assets = FindAction(actions, "group-assets");
        AssertShortcut(assets.Children, "add-file-pdf", "p");
        AssertShortcut(assets.Children, "add-file-excel", "e");
        AssertShortcut(assets.Children, "add-file-docx", "w");
        AssertShortcut(assets.Children, "add-file-json", "j");
        AssertShortcut(assets.Children, "add-file-text", "t");
        AssertDistinctShortcuts(assets.Children);

        var meetings = FindAction(actions, "group-meetings");
        AssertShortcut(meetings.Children, "add-meeting-onsite", "s");
        AssertShortcut(meetings.Children, "add-meeting-online", "o");
        AssertDistinctShortcuts(meetings.Children);

        var work = FindAction(actions, "group-work");
        AssertShortcut(work.Children, "add-work-task", "t");
        AssertDistinctShortcuts(work.Children);
    }

    [Fact]
    public void Project_nodes_keep_project_actions_and_expose_create_tools()
    {
        var adapter = new ProjectStructureActionCatalogAdapter();
        var node = CreateNode(
            "project-child:11111111-1111-1111-1111-111111111111",
            ProjectObjectType.ProjectRoot,
            "Project child",
            0,
            0,
            projectRole: ProjectStructureProjectRole.Subproject,
            relatedProjectId: Guid.Parse("11111111-1111-1111-1111-111111111111"),
            parentProjectCount: 2);

        var actions = adapter.BuildNodeContextActions(node);

        Assert.Contains(actions, action => action.ActionId == "open");
        Assert.Contains(actions, action => action.ActionId == "copy-info");
        Assert.Contains(actions, action => action.ActionId == "project:open-structure");
        Assert.Contains(actions, action => action.ActionId == "project:add-subproject");
        Assert.Contains(actions, action => action.ActionId == "project:reconnect-subproject");
        Assert.Single(actions, action => action.ActionId == "browse-files");
        Assert.Contains(actions, action => action.ActionId == "add-note");
        Assert.Contains(actions, action => action.ActionId == "group-blocks");
        Assert.DoesNotContain(actions, action => action.ActionId == "reconnect");
        Assert.DoesNotContain(actions, action => action.ActionId == "disconnect");
        Assert.DoesNotContain(actions, action => action.ActionId == "delete");
    }

    [Fact]
    public void Storage_backed_infrastructure_node_exposes_one_browse_action_separate_from_open_local()
    {
        var adapter = new ProjectStructureActionCatalogAdapter();
        var node = CreateNode(
            "storage-node",
            ProjectObjectType.Infrastructure,
            "Delivery storage",
            0,
            0,
            nodeReferences: new ProjectNodeReferenceCollection
            {
                InfrastructureStorageCatalogId = Guid.NewGuid()
            });

        var actions = adapter.BuildNodeContextActions(
            node,
            canLaunchRuntime: false,
            canOpenInFileExplorer: true,
            canOpenInNewTab: false);

        Assert.Single(actions, action => action.ActionId == "browse-files");
        Assert.Single(actions, action => action.ActionId == "open-local");
    }

    [Fact]
    public void Unsupported_node_does_not_expose_collection_browsing()
    {
        var adapter = new ProjectStructureActionCatalogAdapter();
        var node = CreateNode("note", ProjectObjectType.Note, "Note", 0, 0);

        var actions = adapter.BuildNodeContextActions(node);

        Assert.DoesNotContain(actions, action => action.ActionId == "browse-files");
    }

    [Fact]
    public void Node_context_actions_assign_marker_progress_and_priority_shortcuts_without_collisions()
    {
        var adapter = new ProjectStructureActionCatalogAdapter();
        var node = CreateNode("note", ProjectObjectType.Note, "Note", 0, 0);

        var actions = adapter.BuildNodeContextActions(node);

        AssertShortcut(actions, "marker", "m");
        AssertShortcut(actions, "add-note", "n");
        AssertShortcut(actions, "group-blocks", "b");
        AssertDistinctShortcuts(actions);

        var marker = FindAction(actions, "marker");
        AssertShortcut(marker.Children, "marker:question", "q");
        AssertShortcut(marker.Children, "marker:alert", "e");
        AssertDistinctShortcuts(marker.Children);

        var progress = FindAction(actions, "progress");
        AssertShortcut(progress.Children, "progress:0", "0");
        AssertShortcut(progress.Children, "progress:10", "1");
        AssertShortcut(progress.Children, "progress:90", "9");
        AssertShortcut(progress.Children, "progress:100", "c");
        AssertShortcut(progress.Children, "progress:started", "s");
        AssertShortcut(progress.Children, "progress:na", "n");
        AssertDistinctShortcuts(progress.Children);

        var priority = FindAction(actions, "priority");
        AssertShortcut(priority.Children, "priority:0", "0");
        AssertShortcut(priority.Children, "priority:6", "6");
        AssertDistinctShortcuts(priority.Children);
    }

    [Fact]
    public void Note_context_actions_start_with_the_standard_hive_first_ring()
    {
        var adapter = new ProjectStructureActionCatalogAdapter();
        var node = CreateNode("note", ProjectObjectType.Note, "Note", 0, 0);

        var actions = adapter.BuildNodeContextActions(node);

        Assert.Equal(
            new[]
            {
                "group-blocks",
                "group-assets",
                "group-work",
                "progress",
                "marker",
                "note:convert-to-block"
            },
            actions.Take(6).Select(action => action.ActionId).ToArray());
    }

    [Fact]
    public void Prompt_flow_context_actions_use_wizard_as_the_primary_first_ring_slot()
    {
        var adapter = new ProjectStructureActionCatalogAdapter();
        var node = CreateNode("flow", ProjectObjectType.PromptFlow, "Flow", 0, 0);

        var actions = adapter.BuildNodeContextActions(node);

        Assert.Equal(
            new[]
            {
                "group-blocks",
                "group-assets",
                "group-work",
                "progress",
                "marker",
                "wizard"
            },
            actions.Take(6).Select(action => action.ActionId).ToArray());
    }

    [Fact]
    public void Project_nodes_use_structure_as_the_primary_first_ring_slot()
    {
        var adapter = new ProjectStructureActionCatalogAdapter();
        var node = CreateNode(
            "project-child:11111111-1111-1111-1111-111111111111",
            ProjectObjectType.ProjectRoot,
            "Project child",
            0,
            0,
            projectRole: ProjectStructureProjectRole.Subproject,
            relatedProjectId: Guid.Parse("11111111-1111-1111-1111-111111111111"),
            parentProjectCount: 2);

        var actions = adapter.BuildNodeContextActions(node);

        Assert.Equal(
            new[]
            {
                "group-blocks",
                "group-assets",
                "group-work",
                "progress",
                "marker",
                "project:open-structure"
            },
            actions.Take(6).Select(action => action.ActionId).ToArray());
    }

    [Fact]
    public void Additional_parent_project_nodes_remain_read_only()
    {
        var adapter = new ProjectStructureActionCatalogAdapter();
        var node = CreateNode(
            "project-related-parent:11111111-1111-1111-1111-111111111111",
            ProjectObjectType.ProjectRoot,
            "Shared parent",
            0,
            0,
            projectRole: ProjectStructureProjectRole.AdditionalParentProject,
            relatedProjectId: Guid.Parse("11111111-1111-1111-1111-111111111111"));

        var actions = adapter.BuildNodeContextActions(node);

        Assert.Contains(actions, action => action.ActionId == "open");
        Assert.DoesNotContain(actions, action => action.ActionId.StartsWith("add-", StringComparison.Ordinal));
        Assert.DoesNotContain(actions, action => action.ActionId.StartsWith("group-", StringComparison.Ordinal));
    }

    [Fact]
    public void Process_definition_nodes_expose_start_process_without_add_process()
    {
        var adapter = new ProjectStructureActionCatalogAdapter();
        var node = CreateNode("process-definition:11111111-1111-1111-1111-111111111111", ProjectObjectType.ProcessDefinition, "Delivery process", 0, 0);

        var actions = adapter.BuildNodeContextActions(node);

        Assert.Contains(actions, action => action.ActionId == "start-process");
        Assert.Contains(actions, action => action.ActionId == "estimate-process");
        Assert.DoesNotContain(actions, action => action.ActionId == "add-process");
    }

    [Fact]
    public void Workflow_definition_nodes_expose_start_workflow_without_add_workflow()
    {
        var adapter = new ProjectStructureActionCatalogAdapter();
        var node = CreateNode("workflow-definition:11111111-1111-1111-1111-111111111111", ProjectObjectType.WorkflowDefinition, "Delivery workflow", 0, 0);

        var actions = adapter.BuildNodeContextActions(node);

        Assert.Contains(actions, action => action.ActionId == "start-workflow");
        Assert.DoesNotContain(actions, action => action.ActionId == "add-workflow");
    }

    [Fact]
    public void Runtime_capable_nodes_expose_normal_and_admin_run_actions_when_requested()
    {
        var adapter = new ProjectStructureActionCatalogAdapter();
        var node = CreateNode("script", ProjectObjectType.Script, "Run setup", 0, 0);

        var actions = adapter.BuildNodeContextActions(node, canLaunchRuntime: true);

        Assert.Contains(actions, action => action.ActionId == "runtime:open" && action.Label == "Run normally");
        Assert.Contains(actions, action => action.ActionId == "runtime:admin" && action.Label == "Run as administrator");
        Assert.Equal("runtime:open", actions.Skip(5).First().ActionId);
        AssertDistinctShortcuts(actions);
    }

    [Fact]
    public void Runtime_actions_stay_hidden_when_runtime_launch_does_not_resolve()
    {
        var adapter = new ProjectStructureActionCatalogAdapter();
        var node = CreateNode("script", ProjectObjectType.Script, "Run setup", 0, 0);

        var actions = adapter.BuildNodeContextActions(node, canLaunchRuntime: false);

        Assert.DoesNotContain(actions, action => action.ActionId == "runtime:open");
        Assert.DoesNotContain(actions, action => action.ActionId == "runtime:admin");
    }

    [Fact]
    public void Local_file_nodes_expose_file_explorer_context_action_when_requested()
    {
        var adapter = new ProjectStructureActionCatalogAdapter();
        var node = CreateNode("file", ProjectObjectType.File, "Specs.pdf", 0, 0);

        var actions = adapter.BuildNodeContextActions(
            node,
            canLaunchRuntime: false,
            canOpenInFileExplorer: true,
            canOpenInNewTab: false);

        Assert.Contains(actions, action => action.ActionId == "open-local" && action.Label == "Show in folder");
        Assert.DoesNotContain(actions, action => action.ActionId == "open-new-tab");
        AssertOrderedBefore(actions, "open-local", "open");
        AssertDistinctShortcuts(actions);
    }

    [Fact]
    public void Ipfs_file_nodes_expose_new_tab_context_action_when_requested()
    {
        var adapter = new ProjectStructureActionCatalogAdapter();
        var node = CreateNode("ipfs-file", ProjectObjectType.File, "IPFS asset", 0, 0);

        var actions = adapter.BuildNodeContextActions(
            node,
            canLaunchRuntime: false,
            canOpenInFileExplorer: false,
            canOpenInNewTab: true);

        Assert.Contains(actions, action => action.ActionId == "open-new-tab" && action.Label == "Open in New Tab");
        Assert.DoesNotContain(actions, action => action.ActionId == "open-local");
        AssertOrderedBefore(actions, "open-new-tab", "open");
        AssertDistinctShortcuts(actions);
    }

    [Fact]
    public void Non_process_nodes_expose_add_process_action()
    {
        var adapter = new ProjectStructureActionCatalogAdapter();
        var node = CreateNode("work-item", ProjectObjectType.WorkItem, "Task", 0, 0);

        var actions = adapter.BuildNodeContextActions(node);

        Assert.Contains(actions, action => action.ActionId == "add-process");
    }

    [Fact]
    public void Non_workflow_nodes_expose_add_workflow_action()
    {
        var adapter = new ProjectStructureActionCatalogAdapter();
        var node = CreateNode("work-item", ProjectObjectType.WorkItem, "Task", 0, 0);

        var actions = adapter.BuildNodeContextActions(node);

        Assert.Contains(actions, action => action.ActionId == "add-workflow");
    }

    [Fact]
    public void Image_asset_context_actions_expose_generated_image_create_action()
    {
        var adapter = new ProjectStructureActionCatalogAdapter();
        var node = CreateNode("image-asset", ProjectObjectType.ImageAsset, "Image asset", 0, 0);

        var actions = adapter.BuildNodeContextActions(node);
        var assetsGroup = FindAction(actions, "group-assets");

        var generateAction = Assert.Single(
            assetsGroup.Children,
            action => action.ActionId == "generate-image-asset");
        Assert.Equal("Generate image", generateAction.Label);
        Assert.Equal("Generate", generateAction.MenuLabel);
        Assert.Equal("auto_awesome", generateAction.Icon);
    }

    private static ProjectStructureNode CreateNode(
        string id,
        ProjectObjectType objectType,
        string title,
        double x,
        double y,
        ProjectStructureProjectRole projectRole = ProjectStructureProjectRole.None,
        Guid? relatedProjectId = null,
        int parentProjectCount = 0,
        ProjectNodeReferenceCollection? nodeReferences = null)
        => new(
            id,
            null,
            objectType,
            string.Empty,
            title,
            string.Empty,
            "Draft",
            string.Empty,
            $"/projects/1/{id}",
            title,
            null,
            string.Empty,
            string.Empty,
            string.Empty,
            x,
            y,
            new ProjectObjectVisualProfile("rect", "#2563eb", "ID", title),
            [],
            string.Empty,
            0,
            string.Empty,
            string.Empty,
            string.Empty,
            [],
            0,
            ProjectRole: projectRole,
            RelatedProjectId: relatedProjectId,
            ParentProjectCount: parentProjectCount,
            NodeReferences: nodeReferences);

    private static void AssertShortcut(IEnumerable<CanvasWorkbenchAction> actions, string actionId, string expectedShortcut)
        => Assert.Equal(expectedShortcut, FindAction(actions, actionId).ShortcutKey);

    private static CanvasWorkbenchAction FindAction(IEnumerable<CanvasWorkbenchAction> actions, string actionId)
        => actions.Single(action => string.Equals(action.ActionId, actionId, StringComparison.Ordinal));

    private static void AssertOrderedBefore(IReadOnlyList<CanvasWorkbenchAction> actions, string actionId, string laterActionId)
    {
        var actionIndex = IndexOf(actions, actionId);
        var laterActionIndex = IndexOf(actions, laterActionId);

        Assert.True(
            actionIndex >= 0 && laterActionIndex >= 0 && actionIndex < laterActionIndex,
            $"Expected '{actionId}' to be ordered before '{laterActionId}', but the action order was: {string.Join(", ", actions.Select(action => action.ActionId))}");
    }

    private static int IndexOf(IReadOnlyList<CanvasWorkbenchAction> actions, string actionId)
    {
        for (var index = 0; index < actions.Count; index++)
        {
            if (string.Equals(actions[index].ActionId, actionId, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return -1;
    }

    private static void AssertDistinctShortcuts(IEnumerable<CanvasWorkbenchAction> actions)
    {
        var shortcuts = actions
            .Select(action => action.ShortcutKey)
            .ToList();

        Assert.DoesNotContain(shortcuts, string.IsNullOrWhiteSpace);
        Assert.Equal(shortcuts.Count, shortcuts.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }
}


