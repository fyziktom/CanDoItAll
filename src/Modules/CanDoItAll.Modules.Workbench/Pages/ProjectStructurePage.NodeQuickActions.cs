using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.SharedKernel;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace CanDoItAll.Modules.Workbench.Pages;

public partial class ProjectStructurePage
{
    [Inject]
    private IJSRuntime JsRuntime { get; set; } = default!;

    private ProjectStructureQuickActionDialogState? quickActionDialog;
    private ProjectStructureWebPreviewDialogState? webPreviewDialog;

    private void OpenQuickActionDialog(ProjectStructureNode node)
        => quickActionDialog = BuildQuickActionDialog(node);

    private void CloseQuickActionDialog(ProjectStructureQuickActionDialogState? opening = null) {
        if (opening is null || quickActionDialog?.OpeningId == opening.OpeningId) {
            quickActionDialog = null;
        }
    }

    private ProjectStructureQuickActionDialogState BuildQuickActionDialog(ProjectStructureNode node)
    {
        var nodeLabel = ProjectStructureCanvasCatalog.ResolveNodeLabel(node);
        var copy = $"Choose the next step for this {nodeLabel.ToLowerInvariant()} without leaving the canvas.";
        if (ProjectStructureNodeActionCapabilityResolver.IsRuntimeCapable(node) &&
            !RuntimeLauncher.IsRunning(node.Id) &&
            surface?.ExpectedProjectAdmission is { } admission &&
            RuntimeLauncher.GetLastExit(node.Id, new(admission, node.RecordId)) is { ExitCode: not 0 } lastExit)
        {
            copy = $"{copy} The last run failed. {lastExit.Describe()}";
        }

        return new ProjectStructureQuickActionDialogState(
            node.Id,
            node.Title,
            nodeLabel,
            copy,
            node.Notes,
            BuildEditQuickAction(node),
            ResolvePrimaryQuickAction(node),
            ResolveSecondaryQuickActions(node)) {
                Context = CaptureActionContext(), OriginalNode = node, SelectionRevision = insightsSelectionRevision,
                RuntimeIdentity = CurrentRuntimeIdentity(node)
            };
    }

    private void OpenWebPreviewDialog(ProjectStructureNode node, ProjectStructureWebLink link)
    {
        if (isOpeningAttachmentPreview || previewNode is not null || PreviewInteraction is not null)
        {
            return;
        }

        CancelRuntimePreviewWait();
        webPreviewDialog = BuildWebPreviewDialog(node, link);
    }

    private ProjectStructureWebPreviewDialogState BuildWebPreviewDialog(
        ProjectStructureNode node,
        ProjectStructureWebLink link)
        => new(
            node.Id,
            node.Title,
            link.SourceLabel,
            link.Uri,
            node.Notes,
            link.CanEmbed,
            link.EmbedUnavailableReason,
            CanStopRuntime: ProjectStructureNodeActionCapabilityResolver.IsRuntimeCapable(node) && CurrentRuntimeIdentity(node) is not null) {
                Context = CaptureActionContext(), OriginalNode = node, RuntimeIdentity = CurrentRuntimeIdentity(node)
            };

    private ProjectStructureWebPreviewDialogState? RebuildWebPreviewDialog(
        IReadOnlyList<ProjectStructureNode> nodes)
    {
        if (webPreviewDialog is null)
        {
            return null;
        }

        var node = nodes.FirstOrDefault(candidate =>
            string.Equals(candidate.Id, webPreviewDialog.NodeId, StringComparison.Ordinal));
        return node is not null &&
               ProjectStructureWebLinkResolver.TryResolve(node, out ProjectStructureWebLink? link) &&
               link is not null
            && webPreviewDialog.OriginalNode?.RecordId == node.RecordId && link.Uri == webPreviewDialog.Url
            ? webPreviewDialog with { Title = node.Title, Notes = node.Notes, SourceLabel = link.SourceLabel,
                CanStopRuntime = webPreviewDialog.RuntimeIdentity is { } owned && CurrentRuntimeIdentity(node) == owned }
            : null;
    }

    private void CloseWebPreviewDialog(ProjectStructureWebPreviewDialogState? opening = null) {
        if (opening is null || webPreviewDialog?.OpeningId == opening.OpeningId) {
            webPreviewDialog = null;
            CancelRuntimePreviewWait();
        }
    }

    private ProjectStructureQuickActionButton BuildEditQuickAction(ProjectStructureNode node)
        => CanEditNode(node)
            ? new ProjectStructureQuickActionButton(
                ProjectStructureQuickActionExecutionKind.Edit,
                "Edit",
                "Open the shared editor with the current node values.",
                "draw",
                "accent")
            : new ProjectStructureQuickActionButton(
                ProjectStructureQuickActionExecutionKind.Edit,
                "Edit",
                "This node does not expose an editor or owning workspace.",
                "draw",
                "ghost",
                IsDisabled: true);

    private ProjectStructureQuickActionButton ResolvePrimaryQuickAction(ProjectStructureNode node)
    {
        if (CanOpenRelatedProjectStructure(node))
        {
            return BuildInspectorQuickAction(
                "Open Structure in New Tab",
                "Keep the current canvas open and launch the related project structure in another browser tab.",
                "open",
                "primary",
                "project:open-structure");
        }

        if (HasMermaidViewer(node))
        {
            return BuildInspectorQuickAction(
                "View Mermaid",
                "Open the diagram viewer with the current Mermaid source.",
                "schema",
                "accent",
                "mermaid:view");
        }

        var runtimeLaunch = RuntimeLauncher.Resolve(node);
        if (ProjectStructureNodeActionCapabilityResolver.IsRuntimeCapable(node))
        {
            if (!RuntimeLauncher.IsAvailable || !runtimeLaunch.IsSuccess)
            {
                return BuildUnavailableRuntimeQuickAction(runtimeLaunch.Message);
            }

            if (RuntimeLauncher.IsRunning(node.Id))
            {
                return BuildInspectorQuickAction(
                    "Stop",
                    "Stop the process identity owned by this Workbench runtime node.",
                    "stop_circle",
                    "warn",
                    "runtime:stop", isDisabled: CurrentRuntimeIdentity(node) is null);
            }

            var capabilities = runtimeLaunch.EffectiveCapabilities;
            if (capabilities.Direct.IsAvailable)
            {
                return BuildRuntimeQuickAction(ProjectStructureRuntimeLaunchMode.Direct);
            }

            if (capabilities.Terminal.IsAvailable)
            {
                return BuildRuntimeQuickAction(ProjectStructureRuntimeLaunchMode.Terminal);
            }

            if (capabilities.Elevation.IsAvailable)
            {
                return BuildRuntimeQuickAction(ProjectStructureRuntimeLaunchMode.Elevated);
            }

            return BuildUnavailableRuntimeQuickAction(
                string.Join(
                    " ",
                    new[]
                    {
                        capabilities.Direct.Message,
                        capabilities.Terminal.Message,
                        capabilities.Elevation.Message
                    }.Distinct(StringComparer.Ordinal)));
        }

        if (CanShowLocalOpen(node))
        {
            return BuildInspectorQuickAction(
                "Show in folder",
                "Open the trusted file location in the system file browser.",
                "folder_open",
                "primary",
                "open-local");
        }

        if (CanOpenIpfsNodeInNewTab(node))
        {
            return BuildInspectorQuickAction(
                "Open in New Tab",
                "Open the IPFS-backed file in a separate browser tab.",
                "open",
                "accent",
                "open-new-tab");
        }

        return node.ObjectType switch
        {
            ProjectObjectType.PromptFlow => BuildCommandQuickAction(
                "Open Prompt in New Tab",
                "Keep the canvas open and launch the bound Gallery prompt in a separate tab.",
                "prompt",
                "accent",
                ProjectStructureCommandKind.Wizard),
            ProjectObjectType.PromptSession or ProjectObjectType.PromptStep => BuildCommandQuickAction(
                "Open Prompt in New Tab",
                "Keep the canvas open and jump into the bound Gallery prompt in a separate tab.",
                "prompt",
                "accent",
                ProjectStructureCommandKind.Open),
            ProjectObjectType.ProcessDefinition or ProjectObjectType.ProcessRun => BuildCommandQuickAction(
                "Open Processes",
                "Open the process workspace in a separate tab without losing the current graph context.",
                "open",
                "primary",
                ProjectStructureCommandKind.Open),
            ProjectObjectType.Recording when CanCreateTranscript(node) => BuildInspectorQuickAction(
                "Create Transcript",
                "Create a transcript beneath this recording and keep the source relationship intact.",
                "transcript",
                "mint",
                "transcript:create"),
            ProjectObjectType.Transcript when HasTranscriptActions(node) => BuildInspectorQuickAction(
                "Summarize",
                "Open the provider-confirmed summarize flow for this transcript.",
                "summary",
                "accent",
                "transcript:summarize"),
            ProjectObjectType.TestPlan or ProjectObjectType.TestEvidence => BuildCommandQuickAction(
                "Open Test Lab",
                "Open the test workspace in a separate tab.",
                "test",
                "warn",
                ProjectStructureCommandKind.Test),
            _ when CanOpenNodeInNewTab(node) => BuildCommandQuickAction(
                "Open in New Tab",
                "Open the linked artifact without leaving the structure canvas.",
                "open",
                "primary",
                ProjectStructureCommandKind.Open),
            _ => BuildInspectorQuickAction(
                "Open Summary",
                "Review hierarchy, progress, and exports from the summary modal.",
                "summary",
                "sky",
                "summary")
        };
    }

    private IReadOnlyList<ProjectStructureQuickActionButton> ResolveSecondaryQuickActions(ProjectStructureNode node)
    {
        var runtimeLaunch = RuntimeLauncher.Resolve(node);
        if (!RuntimeLauncher.IsAvailable || !runtimeLaunch.IsSuccess)
        {
            return [];
        }

        var capabilities = runtimeLaunch.EffectiveCapabilities;
        var actions = new List<ProjectStructureQuickActionButton>();
        var primaryMode = ResolvePreferredRuntimeMode(capabilities);
        if (capabilities.Direct.IsAvailable && primaryMode != ProjectStructureRuntimeLaunchMode.Direct)
        {
            actions.Add(BuildRuntimeQuickAction(ProjectStructureRuntimeLaunchMode.Direct));
        }

        if (capabilities.Terminal.IsAvailable && primaryMode != ProjectStructureRuntimeLaunchMode.Terminal)
        {
            actions.Add(BuildRuntimeQuickAction(ProjectStructureRuntimeLaunchMode.Terminal));
        }

        if (capabilities.Elevation.IsAvailable && primaryMode != ProjectStructureRuntimeLaunchMode.Elevated)
        {
            actions.Add(BuildRuntimeQuickAction(ProjectStructureRuntimeLaunchMode.Elevated));
        }

        if (TryResolveRuntimePreviewLink(node, out var previewLink))
        {
            actions.Add(BuildInspectorQuickAction(
                "Open preview",
                $"Show {previewLink.Uri.AbsoluteUri} in the embedded preview. Run the node first when the application is not already listening there.",
                "preview",
                "sky",
                RuntimePreviewActionId));
        }

        return actions;
    }

    private static bool CanOpenRelatedProjectStructure(ProjectStructureNode node)
        => node.ProjectRole is ProjectStructureProjectRole.Subproject or
           ProjectStructureProjectRole.ParentProject or
           ProjectStructureProjectRole.AdditionalParentProject;

    private static bool CanOpenNodeInNewTab(ProjectStructureNode node)
        => !string.IsNullOrWhiteSpace(node.Route) &&
           !node.Route.EndsWith("/structure", StringComparison.OrdinalIgnoreCase);

    private static bool CanOpenIpfsNodeInNewTab(ProjectStructureNode node)
        => IsIpfsBackedNode(node) && CanOpenNodeInNewTab(node);

    private bool CanOfferFileQuickActions(ProjectStructureNode node)
        => CanShowLocalOpen(node) || CanOpenIpfsNodeInNewTab(node);

    private static bool IsIpfsBackedNode(ProjectStructureNode node)
    {
        if (StorageJson.TryParseReference(node.StorageObjectReferenceJson, out var storageReference) &&
            storageReference is not null &&
            storageReference.ProviderKind == StorageProviderKind.Ipfs)
        {
            return true;
        }

        return node.Route.Contains("/ipfs/", StringComparison.OrdinalIgnoreCase) ||
               (Uri.TryCreate(node.Route, UriKind.Absolute, out var routeUri) &&
                routeUri.Host.Contains("ipfs", StringComparison.OrdinalIgnoreCase));
    }

    private static ProjectStructureQuickActionButton BuildInspectorQuickAction(
        string label,
        string description,
        string icon,
        string tone,
        string actionId,
        bool isDisabled = false)
        => new(
            ProjectStructureQuickActionExecutionKind.InspectorAction,
            label,
            description,
            icon,
            tone,
            ActionId: actionId,
            IsDisabled: isDisabled);

    private static ProjectStructureQuickActionButton BuildRuntimeQuickAction(
        ProjectStructureRuntimeLaunchMode mode)
        => mode switch
        {
            ProjectStructureRuntimeLaunchMode.Direct => BuildInspectorQuickAction(
                "Run",
                "Execute the typed runtime plan directly through the owned process host.",
                "play_arrow",
                "accent",
                "runtime:open"),
            ProjectStructureRuntimeLaunchMode.Terminal => BuildInspectorQuickAction(
                "Open terminal",
                "Present the typed runtime plan in the configured terminal.",
                "terminal",
                "sky",
                "runtime:terminal"),
            ProjectStructureRuntimeLaunchMode.Elevated => BuildInspectorQuickAction(
                "Elevated launch",
                "Start the typed runtime plan through the supported elevation capability.",
                "admin_panel_settings",
                "warn",
                "runtime:admin"),
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null)
        };

    private static ProjectStructureQuickActionButton BuildUnavailableRuntimeQuickAction(string message)
        => BuildInspectorQuickAction(
            "Runtime unavailable",
            string.IsNullOrWhiteSpace(message)
                ? "No runtime launch capability is available on this host."
                : message,
            "block",
            "warn",
            string.Empty,
            isDisabled: true);

    private static ProjectStructureRuntimeLaunchMode? ResolvePreferredRuntimeMode(
        ProjectStructureRuntimeLaunchCapabilities capabilities)
        => capabilities.Direct.IsAvailable
            ? ProjectStructureRuntimeLaunchMode.Direct
            : capabilities.Terminal.IsAvailable
                ? ProjectStructureRuntimeLaunchMode.Terminal
                : capabilities.Elevation.IsAvailable
                    ? ProjectStructureRuntimeLaunchMode.Elevated
                    : null;

    private static ProjectStructureQuickActionButton BuildCommandQuickAction(
        string label,
        string description,
        string icon,
        string tone,
        ProjectStructureCommandKind commandKind)
        => new(
            ProjectStructureQuickActionExecutionKind.CommandInNewTab,
            label,
            description,
            icon,
            tone,
            CommandKind: commandKind);

    private async Task ExecuteQuickActionAsync(ProjectStructureQuickActionDialogState? opening, ProjectStructureQuickActionButton action) {
        if (opening is null || quickActionDialog?.OpeningId != opening.OpeningId || action.IsDisabled ||
            !opening.Actions.Contains(action) || opening.Context is not { } context || opening.OriginalNode is not { } targetNode ||
            !IsCurrentAction(context) || !HasOriginalContentAuthority(context) || opening.SelectionRevision != insightsSelectionRevision) {
            return;
        }
        quickActionDialog = null;
        try {
            await ProjectWorkbenchService.RequireContentCurrentAsync(context.Admission, targetNode);
            if (!IsCurrentAction(context) || !HasOriginalContentAuthority(context) || opening.SelectionRevision != insightsSelectionRevision) {
                return;
            }
            switch (action.ExecutionKind) {
                case ProjectStructureQuickActionExecutionKind.Edit:
                    await OpenEditDialogAsync(targetNode, context);
                    break;
                case ProjectStructureQuickActionExecutionKind.InspectorAction when action.ActionId == "runtime:stop":
                    await StopRuntimeAsync(targetNode, opening.RuntimeIdentity, context);
                    break;
                case ProjectStructureQuickActionExecutionKind.InspectorAction:
                    await ExecuteInspectorActionAsync(targetNode, action.ActionId, context);
                    break;
                case ProjectStructureQuickActionExecutionKind.CommandInNewTab when action.CommandKind.HasValue:
                    await ExecuteCommandAsync(action.CommandKind.Value, targetNode.Id, openInNewTab: true, capturedContext: context);
                    break;
            }
        } catch (Exception failure) when (failure is CanDoItAll.Modules.Projects.ProjectWriteAdmissionRejectedException or ProjectStructureEditConflictException) {
            ReportActionResult(context, "The original quick-action target changed. Reopen its actions.", "warn");
        }
    }

    private async Task OpenArtifactInNewTabAsync(string route)
    {
        if (string.IsNullOrWhiteSpace(route))
        {
            workflowFeedback = "The selected node does not expose a route that can open in a new tab.";
            workflowFeedbackTone = "warn";
            await InvokeAsync(StateHasChanged);
            return;
        }

        try
        {
            await JsRuntime.InvokeVoidAsync(
                "open",
                Navigation.ToAbsoluteUri(route).ToString(),
                "_blank",
                "noopener,noreferrer");
        }
        catch (JSException)
        {
            workflowFeedback = "The browser blocked opening the selected node in a new tab.";
            workflowFeedbackTone = "warn";
            await InvokeAsync(StateHasChanged);
        }
    }
}
