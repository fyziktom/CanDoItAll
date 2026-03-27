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
        var actions = new List<ProjectStructureInspectorAction>();
        if (CanEditNode(node))
        {
            actions.Add(new ProjectStructureInspectorAction("edit", "Edit", "draw", "accent"));
        }

        var runtimeLaunch = RuntimeLauncher.Resolve(node);
        if (RuntimeLauncher.IsAvailable && runtimeLaunch.IsSuccess)
        {
            actions.Add(new ProjectStructureInspectorAction("runtime:open", "Open PowerShell", "powershell", "accent"));
            actions.Add(new ProjectStructureInspectorAction("runtime:admin", "Open PowerShell (Admin)", "admin_panel_settings", "warn"));
        }

        actions.AddRange(ResolveInspectorCommands(node).Select(MapCommandAction));
        actions.Add(new ProjectStructureInspectorAction("summary", "Summary", "summary", "sky"));
        actions.Add(new ProjectStructureInspectorAction("connect", "Connect selected", "link", "ghost"));
        actions.Add(new ProjectStructureInspectorAction("reconnect", "Reconnect", "relink", "primary"));
        actions.Add(new ProjectStructureInspectorAction("disconnect", "Disconnect", "link_off", "ghost"));
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

        actions.Add(new ProjectStructureInspectorAction("delete", "Delete", "delete", "danger"));
        return actions;
    }

    private static ProjectStructureInspectorAction MapCommandAction(ProjectStructureCommandKind command)
        => command switch
        {
            ProjectStructureCommandKind.Open => new ProjectStructureInspectorAction("command:open", ResolveCommandLabel(command), "open", "primary"),
            ProjectStructureCommandKind.Wizard => new ProjectStructureInspectorAction("command:wizard", ResolveCommandLabel(command), "prompt", "accent"),
            ProjectStructureCommandKind.Branch => new ProjectStructureInspectorAction("command:branch", ResolveCommandLabel(command), "fork", "accent"),
            ProjectStructureCommandKind.MarkUsed => new ProjectStructureInspectorAction("command:mark-used", ResolveCommandLabel(command), "use", "mint"),
            ProjectStructureCommandKind.Skip => new ProjectStructureInspectorAction("command:skip", ResolveCommandLabel(command), "skip", "ghost"),
            ProjectStructureCommandKind.Validate => new ProjectStructureInspectorAction("command:validate", ResolveCommandLabel(command), "qa", "primary"),
            _ => new ProjectStructureInspectorAction("command:test", ResolveCommandLabel(command), "test", "warn")
        };

    private async Task ExecuteInspectorActionAsync(ProjectStructureNode node, string actionId)
    {
        switch (actionId)
        {
            case "edit":
                await OpenEditDialogAsync(node);
                break;
            case "runtime:open":
                await LaunchRuntimeAsync(node, false);
                break;
            case "runtime:admin":
                await LaunchRuntimeAsync(node, true);
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
            case "summary":
                await OpenSummaryAsync(node.Id);
                break;
            case "connect":
                EnableLinkMode();
                break;
            case "reconnect":
                await BeginReconnectAsync(node.Id);
                break;
            case "disconnect":
                await DisconnectNodeAsync(node.Id);
                break;
            case "export-image":
                await ExportMindmapImageAsync();
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

        await ReloadSurfaceAsync(updated.Id);
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
