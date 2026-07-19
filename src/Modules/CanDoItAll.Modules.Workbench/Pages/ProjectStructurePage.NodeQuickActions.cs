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

    private void CloseQuickActionDialog()
        => quickActionDialog = null;

    private ProjectStructureQuickActionDialogState BuildQuickActionDialog(ProjectStructureNode node)
    {
        var nodeLabel = ProjectStructureCanvasCatalog.ResolveNodeLabel(node);
        return new ProjectStructureQuickActionDialogState(
            node.Id,
            node.Title,
            nodeLabel,
            $"Choose the next step for this {nodeLabel.ToLowerInvariant()} without leaving the canvas.",
            node.Notes,
            BuildEditQuickAction(node),
            ResolvePrimaryQuickAction(node),
            ResolveSecondaryQuickActions(node));
    }

    private void OpenWebPreviewDialog(ProjectStructureNode node, ProjectStructureWebLink link)
    {
        if (isOpeningAttachmentPreview || previewNode is not null || PreviewInteraction is not null)
        {
            return;
        }

        webPreviewDialog = BuildWebPreviewDialog(node, link);
    }

    private static ProjectStructureWebPreviewDialogState BuildWebPreviewDialog(
        ProjectStructureNode node,
        ProjectStructureWebLink link)
        => new(
            node.Id,
            node.Title,
            link.SourceLabel,
            link.Uri,
            node.Notes,
            link.CanEmbed,
            link.EmbedUnavailableReason);

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
            ? BuildWebPreviewDialog(node, link)
            : null;
    }

    private void CloseWebPreviewDialog()
        => webPreviewDialog = null;

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
        if (RuntimeLauncher.IsAvailable && runtimeLaunch.IsSuccess)
        {
            return BuildInspectorQuickAction(
                "Run normally",
                "Launch the resolved workspace command in PowerShell.",
                "powershell",
                "accent",
                "runtime:open");
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

        return
        [
            BuildInspectorQuickAction(
                "Run as administrator",
                "Launch the resolved workspace command in an elevated PowerShell window.",
                "admin_panel_settings",
                "warn",
                "runtime:admin")
        ];
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
        string actionId)
        => new(
            ProjectStructureQuickActionExecutionKind.InspectorAction,
            label,
            description,
            icon,
            tone,
            ActionId: actionId);

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

    private async Task ExecuteQuickActionAsync(ProjectStructureQuickActionButton action)
    {
        if (quickActionDialog is null || action.IsDisabled)
        {
            return;
        }

        var targetNode = ResolveNode(quickActionDialog.NodeId);
        quickActionDialog = null;
        if (targetNode is null)
        {
            return;
        }

        switch (action.ExecutionKind)
        {
            case ProjectStructureQuickActionExecutionKind.Edit:
                await OpenEditDialogAsync(targetNode);
                break;
            case ProjectStructureQuickActionExecutionKind.InspectorAction:
                await ExecuteInspectorActionAsync(targetNode, action.ActionId);
                break;
            case ProjectStructureQuickActionExecutionKind.CommandInNewTab when action.CommandKind.HasValue:
                await ExecuteCommandAsync(action.CommandKind.Value, targetNode.Id, openInNewTab: true);
                break;
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
