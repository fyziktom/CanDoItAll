using CanDoItAll.Components.CanvasLib;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Workbench.Pages;

public partial class ProjectStructurePage
{
    private sealed record ProjectStructureInspectorAction(
        string ActionId,
        string Label,
        string Icon,
        string Tone);

    private sealed record ProjectStructureNodeEditModel(
        CanvasWorkbenchAction Action,
        CanvasWorkbenchCreateActionRequest Request);

    private IReadOnlyList<ProjectStructureInspectorAction> ResolveInspectorActions(ProjectStructureNode node)
    {
        if (node.ProjectRole != ProjectStructureProjectRole.None)
        {
            return ResolveProjectInspectorActions(node);
        }

        var actions = new List<ProjectStructureInspectorAction>();
        if (CanEditNode(node))
        {
            actions.Add(new ProjectStructureInspectorAction("edit", "Edit", "draw", "accent"));
        }

        if (HasMermaidViewer(node))
        {
            actions.Add(new ProjectStructureInspectorAction("mermaid:view", "View Mermaid", "schema", "accent"));
        }

        actions.Add(new ProjectStructureInspectorAction("copy-id", "Copy id", "copy", "ghost"));
        actions.Add(new ProjectStructureInspectorAction("copy-info", "Copy info", "copy", "primary"));
        actions.Add(new ProjectStructureInspectorAction("copy-subtree-ids", "Copy tree ids", "copy", "sky"));

        var runtimeLaunch = RuntimeLauncher.Resolve(node);
        if (RuntimeLauncher.IsAvailable && runtimeLaunch.IsSuccess)
        {
            actions.Add(new ProjectStructureInspectorAction("runtime:open", "Open PowerShell", "powershell", "accent"));
            actions.Add(new ProjectStructureInspectorAction("runtime:admin", "Open PowerShell (Admin)", "admin_panel_settings", "warn"));
        }

        if (CanShowLocalOpen(node))
        {
            actions.Add(new ProjectStructureInspectorAction("open-local", "Open in File Explorer", "folder_open", "primary"));
        }

        if (CanOpenIpfsNodeInNewTab(node))
        {
            actions.Add(new ProjectStructureInspectorAction("open-new-tab", "Open in New Tab", "open", "accent"));
        }

        actions.AddRange(ProjectStructureNodeHelpers.ResolveInspectorCommands(node).Select(MapCommandAction));
        actions.Add(new ProjectStructureInspectorAction("summary", "Summary", "summary", "sky"));

        if (node.ObjectType == ProjectObjectType.ProcessDefinition)
        {
            actions.Add(new ProjectStructureInspectorAction("start-process", "Start", "play_arrow", "mint"));
        }
        else if (CanLinkExistingProcess(node))
        {
            actions.Add(new ProjectStructureInspectorAction("add-process", "Add process", "account_tree", "mint"));
        }

        if (node.ObjectType == ProjectObjectType.WorkflowDefinition)
        {
            actions.Add(new ProjectStructureInspectorAction("start-workflow", "Start workflow", "play_arrow", "mint"));
        }
        else if (CanLinkExistingWorkflow(node))
        {
            actions.Add(new ProjectStructureInspectorAction("add-workflow", "Add workflow", "flow", "accent"));
        }

        actions.Add(new ProjectStructureInspectorAction("connect", "Connect selected", "link", "ghost"));
        actions.Add(new ProjectStructureInspectorAction("reconnect", "Reconnect", "relink", "primary"));
        actions.Add(new ProjectStructureInspectorAction("disconnect", "Disconnect", "link_off", "ghost"));
        actions.Add(new ProjectStructureInspectorAction("move-descendants-to-subproject", "To subproject", "fork", "primary"));
        actions.Add(new ProjectStructureInspectorAction("export-image", "Export image", "image", "accent"));

        if (CanCreateTranscript(node))
        {
            actions.Add(new ProjectStructureInspectorAction("transcript:create", "Create transcript", "transcript", "mint"));
        }

        if (HasTranscriptActions(node))
        {
            actions.Add(new ProjectStructureInspectorAction("transcript:summarize", "Summarize", "summary", "accent"));
            actions.Add(new ProjectStructureInspectorAction("transcript:find-my-tasks", "Find my tasks", "task", "warn"));
            actions.Add(new ProjectStructureInspectorAction("transcript:find-others-deliveries", "Find others delivery to me", "people", "sky"));
        }

        if (node.ObjectType == ProjectObjectType.ProjectBlock)
        {
            actions.Add(new ProjectStructureInspectorAction("block:change-type", "Change block", "swap_horiz", "accent"));
        }

        if (node.ObjectType == ProjectObjectType.Note)
        {
            actions.Add(new ProjectStructureInspectorAction("note:convert-to-block", "Convert", "swap_horiz", "accent"));
        }

        actions.Add(new ProjectStructureInspectorAction("delete", "Delete", "delete", "danger"));
        return actions;
    }

    private static IReadOnlyList<ProjectStructureInspectorAction> ResolveProjectInspectorActions(ProjectStructureNode node)
    {
        var actions = new List<ProjectStructureInspectorAction>
        {
            new("command:open", "Open", "open", "primary"),
            new("copy-id", "Copy id", "copy", "ghost"),
            new("copy-info", "Copy info", "copy", "primary"),
            new("copy-subtree-ids", "Copy tree ids", "copy", "sky"),
            new("summary", "Summary", "summary", "sky"),
            new("connect", "Connect selected", "link", "ghost"),
            new("export-image", "Export image", "image", "accent")
        };

        if (node.ProjectRole is ProjectStructureProjectRole.Subproject or
            ProjectStructureProjectRole.ParentProject or
            ProjectStructureProjectRole.AdditionalParentProject)
        {
            actions.Insert(1, new ProjectStructureInspectorAction("project:open-structure", "Open structure tab", "open", "accent"));
        }

        if (node.ProjectRole is ProjectStructureProjectRole.ActiveProject or ProjectStructureProjectRole.Subproject)
        {
            actions.Add(new ProjectStructureInspectorAction("project:add-subproject", "Add subproject", "fork", "accent"));
        }

        if (node.ProjectRole == ProjectStructureProjectRole.Subproject)
        {
            actions.Add(new ProjectStructureInspectorAction("project:reconnect-subproject", "Reconnect parent", "relink", "primary"));
        }

        return actions;
    }

    private static ProjectStructureInspectorAction MapCommandAction(ProjectStructureCommandKind command)
        => command switch
        {
            ProjectStructureCommandKind.Open => new ProjectStructureInspectorAction("command:open", ProjectStructureNodeHelpers.ResolveCommandLabel(command), "open", "primary"),
            ProjectStructureCommandKind.Wizard => new ProjectStructureInspectorAction("command:wizard", ProjectStructureNodeHelpers.ResolveCommandLabel(command), "prompt", "accent"),
            ProjectStructureCommandKind.Branch => new ProjectStructureInspectorAction("command:branch", ProjectStructureNodeHelpers.ResolveCommandLabel(command), "fork", "accent"),
            ProjectStructureCommandKind.MarkUsed => new ProjectStructureInspectorAction("command:mark-used", ProjectStructureNodeHelpers.ResolveCommandLabel(command), "use", "mint"),
            ProjectStructureCommandKind.Skip => new ProjectStructureInspectorAction("command:skip", ProjectStructureNodeHelpers.ResolveCommandLabel(command), "skip", "ghost"),
            ProjectStructureCommandKind.Validate => new ProjectStructureInspectorAction("command:validate", ProjectStructureNodeHelpers.ResolveCommandLabel(command), "qa", "primary"),
            _ => new ProjectStructureInspectorAction("command:test", ProjectStructureNodeHelpers.ResolveCommandLabel(command), "test", "warn")
        };

    private IReadOnlyList<ProjectStructureSupportPanelContextAction> ResolveSupportPanelContextActions(ProjectStructureNode node)
        => ResolveInspectorActions(node)
            .Select(action => new ProjectStructureSupportPanelContextAction(
                action.ActionId,
                action.Label,
                action.Icon,
                action.Tone))
            .ToList();

    private async Task ExecuteOutlineContextActionAsync(ProjectStructureSupportPanelContextActionRequest request)
    {
        var node = ResolveNode(request.NodeId);
        if (node is null)
        {
            return;
        }

        await SelectNodeAsync(node.Id);
        await ExecuteInspectorActionAsync(node, request.ActionId);
    }

    private async Task ExecuteInspectorActionAsync(ProjectStructureNode node, string actionId)
    {
        switch (actionId)
        {
            case "edit":
                await OpenEditDialogAsync(node);
                break;
            case "mermaid:view":
                OpenMermaidViewer(node);
                await InvokeAsync(StateHasChanged);
                break;
            case "runtime:open":
                await LaunchRuntimeAsync(node, false);
                break;
            case "runtime:admin":
                await LaunchRuntimeAsync(node, true);
                break;
            case "open-local":
                await OpenAttachmentLocallyAsync(node);
                break;
            case "open-new-tab":
                await OpenArtifactInNewTabAsync(node.Route);
                break;
            case "command:open":
                await ExecuteCommandAsync(ProjectStructureCommandKind.Open, node.Id);
                break;
            case "command:wizard":
                await ExecuteCommandAsync(ProjectStructureCommandKind.Wizard, node.Id);
                break;
            case "command:branch":
                await ExecuteCommandAsync(ProjectStructureCommandKind.Branch, node.Id);
                break;
            case "command:mark-used":
                await ExecuteCommandAsync(ProjectStructureCommandKind.MarkUsed, node.Id);
                break;
            case "command:skip":
                await ExecuteCommandAsync(ProjectStructureCommandKind.Skip, node.Id);
                break;
            case "command:validate":
                await ExecuteCommandAsync(ProjectStructureCommandKind.Validate, node.Id);
                break;
            case "command:test":
                await ExecuteCommandAsync(ProjectStructureCommandKind.Test, node.Id);
                break;
            case "copy-id":
            case "copy-info":
            case "copy-subtree-ids":
                await TryHandleCopyActionAsync(actionId, node.Id);
                break;
            case "summary":
                await OpenSummaryAsync(node.Id);
                break;
            case "add-process":
                await OpenAddProcessDialogAsync(node);
                break;
            case "start-process":
            case "execute-process":
                await OpenStartProcessDialogAsync(node);
                break;
            case "add-workflow":
                await OpenAddWorkflowDialogAsync(node);
                break;
            case "start-workflow":
            case "execute-workflow":
                await OpenStartWorkflowDialogAsync(node);
                break;
            case "project:open-structure":
                await OpenProjectStructureInNewTabAsync(node);
                break;
            case "project:add-subproject":
                await OpenAddSubprojectDialogAsync(node);
                break;
            case "project:reconnect-subproject":
                await OpenReconnectSubprojectDialogAsync(node);
                break;
            case "connect":
                await EnterDependencyModeAsync(node.Id);
                break;
            case "reconnect":
                await BeginReconnectAsync(node.Id);
                break;
            case "disconnect":
                await DisconnectNodeAsync(node.Id);
                break;
            case "move-descendants-to-subproject":
                await OpenMoveDescendantsToSubprojectDialogAsync(node);
                break;
            case "export-image":
                await ExportMindmapImageAsync();
                break;
            case "block:change-type":
                await OpenChangeBlockTypeDialogAsync(node);
                break;
            case "note:convert-to-block":
                await OpenNoteConversionDialogAsync(node);
                break;
            case "transcript:create":
                await CreateTranscriptFromRecordingAsync(node);
                break;
            case "transcript:summarize":
                await OpenTranscriptActionAsync(ProjectLlmActionKind.Summarize, node.Id);
                break;
            case "transcript:find-my-tasks":
                await OpenTranscriptActionAsync(ProjectLlmActionKind.FindMyTasks, node.Id);
                break;
            case "transcript:find-others-deliveries":
                await OpenTranscriptActionAsync(ProjectLlmActionKind.FindOthersDeliveries, node.Id);
                break;
            case "delete":
                await DeleteNodeAsync(node.Id);
                break;
        }
    }

    private bool CanShowAdvancedDetails(ProjectStructureNode node)
        => SelectedNodeFacts.Count > 0 ||
            !string.IsNullOrWhiteSpace(node.ArtifactKind) ||
            !string.IsNullOrWhiteSpace(node.VisualProfile.AccentBadge);

    private static bool CanLinkExistingProcess(ProjectStructureNode node)
    {
        return node.ProjectRole == ProjectStructureProjectRole.None &&
               node.ObjectType is not (ProjectObjectType.ProcessDefinition or ProjectObjectType.ProcessRun);
    }

    private static bool CanLinkExistingWorkflow(ProjectStructureNode node)
    {
        return node.ProjectRole == ProjectStructureProjectRole.None &&
               node.ObjectType is not (
                   ProjectObjectType.ProcessDefinition or
                   ProjectObjectType.ProcessRun or
                   ProjectObjectType.WorkflowDefinition or
                   ProjectObjectType.WorkflowRun);
    }

    private bool CanEditNode(ProjectStructureNode? node)
    {
        if (node is null || node.Badges.Contains("Synced", StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        return TryBuildNodeEditModel(node, out _);
    }

    private async Task OpenEditDialogAsync(ProjectStructureNode node)
    {
        if (node.ObjectType == ProjectObjectType.SecretReference)
        {
            await OpenSecretReferenceEditDialogAsync(node);
            return;
        }

        if (!TryBuildNodeEditModel(node, out var model) || workbenchRef is null)
        {
            return;
        }

        await workbenchRef.OpenCreateDialogAsync(model.Action, model.Request);
    }

    private async Task<bool> TryApplyNodeEditAsync(CanvasWorkbenchCreateActionRequest request)
    {
        if (!TryResolveEditAction(request.ActionId, out var createActionId) ||
            !ProjectStructureCanvasCatalog.TryResolveCreateDefinition(createActionId, out var definition) ||
            surface?.Nodes.FirstOrDefault(node => string.Equals(node.Id, request.SourceNodeId, StringComparison.Ordinal)) is not { } targetNode)
        {
            return false;
        }

        var update = ProjectStructureNodeEditor.ComposeUpdate(definition, targetNode, request);
        var updated = await ProjectWorkbenchService.UpdateObjectAsync(ProjectId, targetNode.Id, update);
        if (updated is null)
        {
            workflowFeedback = "The selected node could not be updated.";
            workflowFeedbackTone = "warn";
            return true;
        }

        await ApplySurfaceNodeUpdatesAsync([updated]);
        workflowFeedback = $"{updated.Title} was updated.";
        workflowFeedbackTone = "mint";
        return true;
    }

    private bool TryBuildNodeEditModel(ProjectStructureNode node, out ProjectStructureNodeEditModel model)
    {
        model = default!;
        if (!ProjectStructureCanvasCatalog.TryResolveCreateDefinition(node.ObjectType, node.ObjectSubtype, out var definition))
        {
            return false;
        }

        var baseAction = HydrateCreateAction(ProjectStructureCanvasCatalog.BuildComposerAction(definition));
        var action = new CanvasWorkbenchAction
        {
            ActionId = $"edit:{definition.ActionId}",
            Label = "Edit",
            Description = $"Edit {ProjectStructureCanvasCatalog.ResolveNodeLabel(node).ToLowerInvariant()} settings.",
            Icon = "draw",
            MenuLabel = "Edit",
            MenuSize = baseAction.MenuSize,
            SubmenuLayout = baseAction.SubmenuLayout,
            Tone = "accent",
            RequiresInput = true,
            CreateMode = "dialog",
            ObjectSubtype = baseAction.ObjectSubtype,
            TitleLabel = baseAction.TitleLabel,
            TitlePlaceholder = baseAction.TitlePlaceholder,
            SubtitleLabel = baseAction.SubtitleLabel,
            SubtitlePlaceholder = baseAction.SubtitlePlaceholder,
            NotesLabel = baseAction.NotesLabel,
            NotesPlaceholder = baseAction.NotesPlaceholder,
            ShowDefaultTextFields = baseAction.ShowDefaultTextFields,
            SubmitLabel = "Save changes",
            RequiresFile = false,
            AcceptedFileTypes = string.Empty,
            FilePrompt = "File replacement is not supported from the edit flow.",
            SupportsDragDrop = false,
            InputFields = baseAction.InputFields
                .Where(field => ProjectStructureNodeEditor.SupportsEditingField(field.Key))
                .ToList(),
            DefaultInputValues = [],
            Children = []
        };

        model = new ProjectStructureNodeEditModel(
            action,
            new CanvasWorkbenchCreateActionRequest(
                action.ActionId,
                node.Id,
                node.X,
                node.Y,
                node.ParentId,
                node.Title,
                node.Subtitle,
                node.Notes,
                "edit",
                "dialog",
                node.ObjectSubtype,
                null,
                ProjectStructureNodeEditor.BuildInputValues(definition, node)));
        return true;
    }

    private static bool TryResolveEditAction(string actionId, out string createActionId)
    {
        createActionId = string.Empty;
        if (!actionId.StartsWith("edit:", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        createActionId = actionId["edit:".Length..];
        return !string.IsNullOrWhiteSpace(createActionId);
    }
}
