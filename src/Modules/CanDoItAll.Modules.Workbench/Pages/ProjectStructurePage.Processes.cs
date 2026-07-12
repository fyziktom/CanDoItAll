using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Globalization;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Projections;
using CanDoItAll.SharedKernel;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.Workbench.Pages;

public partial class ProjectStructurePage
{
    private const string ProjectStructureHrManagerName = "HR Staffing Manager";
    private static readonly TimeSpan ProcessStartPreviewTimeout = TimeSpan.FromSeconds(45);
    private static readonly TimeSpan ProcessStartHistoricalEstimateTimeout = TimeSpan.FromSeconds(15);
    private const int ProcessStartInlineCandidateLimit = 8;
    private static readonly string[] OutputRootMetadataKeys =
    [
        "outputRoot",
        "productRoot",
        "targetRoot",
        "targetPath",
        "repositoryRoot",
        "workspaceRoot"
    ];

    [Inject]
    private ProcessDefinitionCatalogProjectionService ProcessDefinitionCatalogService { get; set; } = default!;

    [Inject]
    private ProcessLaunchApplicationService ProcessLaunchService { get; set; } = default!;

    [Inject]
    private IAgentReferenceDataProvider AgentReferenceDataProvider { get; set; } = default!;

    [Inject]
    private IProcessHistoricalRunCostReader ProcessHistoricalRunCostReader { get; set; } = default!;

    [Inject]
    private ProcessLaunchVariablePreparationService ProcessLaunchVariablePreparationService { get; set; } = default!;

    private ProjectStructureProcessLinkDialogState? processLinkDialog;
    private ProjectStructureProcessStartDialogState? processStartDialog;
    private CancellationTokenSource? processStartHistoricalEstimateRefreshCts;
    private string processStartEstimateDefinitionKey = string.Empty;
    private int processStartEstimateAssignmentCount;
    private IReadOnlyDictionary<Guid, ProjectStructureProcessStartAgentMetadata> processStartAgentMetadataById =
        new Dictionary<Guid, ProjectStructureProcessStartAgentMetadata>();

    private Task OpenAddProcessDialogAsync(ProjectStructureNode node)
    {
        return OpenLinkProcessDialogAsync(node);
    }

    private async Task OpenLinkProcessDialogAsync(ProjectStructureNode node)
    {
        CloseQuickActionDialog();

        try
        {
            var catalog = await ProcessDefinitionCatalogService.GetCatalogAsync(
                ProcessWorkspaceShellScope.ForProject(ProjectId),
                new ProcessDefinitionCatalogQueryProjection(
                    SearchText: null,
                    SelectedDefinitionKey: null,
                    ProcessDefinitionCatalogScopeKind.All,
                    Take: 200));
            var options = catalog.Items
                .OrderBy(item => item.ScopeKind == ProcessDefinitionCatalogScopeKind.Project ? 0 : 1)
                .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.Key.Value, StringComparer.OrdinalIgnoreCase)
                .Select(MapProcessLinkOption)
                .ToList();

            processLinkDialog = new ProjectStructureProcessLinkDialogState(
                node.Id,
                node.Title,
                options,
                options.FirstOrDefault()?.DefinitionId,
                options.Count == 0 ? "No process definitions are available in the process template catalog." : string.Empty);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            Logger.LogWarning(exception, "Project structure process definition catalog load failed. ProjectId={ProjectId}", ProjectId);
            processLinkDialog = new ProjectStructureProcessLinkDialogState(
                node.Id,
                node.Title,
                [],
                null,
                $"Process definitions could not be loaded: {exception.Message}");
        }

        await InvokeAsync(StateHasChanged);
    }

    private void CloseProcessLinkDialog()
    {
        processLinkDialog = null;
    }

    private void HandleProcessLinkSelectionChanged(ChangeEventArgs args)
    {
        if (processLinkDialog is null)
        {
            return;
        }

        var selectedDefinitionId = Guid.TryParse(args.Value?.ToString(), out var parsedDefinitionId)
            ? parsedDefinitionId
            : (Guid?)null;
        processLinkDialog = processLinkDialog with
        {
            SelectedDefinitionId = selectedDefinitionId,
            Error = string.Empty
        };
    }

    private async Task ExecuteProcessLinkAsync()
    {
        if (processLinkDialog is null)
        {
            return;
        }

        if (!processLinkDialog.SelectedDefinitionId.HasValue)
        {
            processLinkDialog = processLinkDialog with { Error = "Select a process before continuing." };
            await InvokeAsync(StateHasChanged);
            return;
        }

        var selectedOption = processLinkDialog.Options
            .FirstOrDefault(option => option.DefinitionId == processLinkDialog.SelectedDefinitionId.Value);
        if (selectedOption is null)
        {
            processLinkDialog = processLinkDialog with { Error = "The selected process is no longer available." };
            await InvokeAsync(StateHasChanged);
            return;
        }

        try
        {
            await ProjectWorkbenchService.LinkObjectsAsync(
                ProjectId,
                processLinkDialog.SourceNodeId,
                ProjectStructureProcessNodeKeys.BuildProcessDefinitionNodeKey(selectedOption.DefinitionId),
                ProjectObjectLinkKind.Uses);
        }
        catch (InvalidOperationException exception)
        {
            processLinkDialog = processLinkDialog with { Error = exception.Message };
            await InvokeAsync(StateHasChanged);
            return;
        }

        var sourceNodeId = processLinkDialog.SourceNodeId;
        var processNodeId = ProjectStructureProcessNodeKeys.BuildProcessDefinitionNodeKey(selectedOption.DefinitionId);
        workflowFeedback = $"{selectedOption.DisplayName} was linked to {processLinkDialog.SourceNodeTitle}.";
        workflowFeedbackTone = "mint";
        processLinkDialog = null;
        await ReloadSurfaceAsync(sourceNodeId);
        await OpenProcessDialogAsync(
            selectedOption.DefinitionId,
            processNodeId,
            selectedOption.DisplayName,
            ResolveNode(sourceNodeId),
            estimateOnly: false);
    }

    private Task OpenStartProcessDialogAsync(ProjectStructureNode node)
    {
        return OpenProcessDialogAsync(node, estimateOnly: false);
    }

    private Task OpenEstimateProcessDialogAsync(ProjectStructureNode node)
    {
        return OpenProcessDialogAsync(node, estimateOnly: true);
    }

    private async Task OpenProcessDialogAsync(ProjectStructureNode node, bool estimateOnly)
    {
        var processDefinitionId = ResolveProcessDefinitionId(node);
        if (!processDefinitionId.HasValue)
        {
            workflowFeedback = "The selected process node is missing its process definition id.";
            workflowFeedbackTone = "warn";
            await InvokeAsync(StateHasChanged);
            return;
        }

        CloseQuickActionDialog();
        var targetNode = ResolveProcessStartTargetNode(node);
        await OpenProcessDialogAsync(processDefinitionId.Value, node.Id, node.Title, targetNode, estimateOnly);
    }

    private async Task OpenProcessDialogAsync(
        Guid processDefinitionId,
        string processNodeId,
        string processNodeTitle,
        ProjectStructureNode? targetNode,
        bool estimateOnly)
    {
        CloseQuickActionDialog();
        ResetProcessStartEstimateState();
        var definitionKey = ResolveProcessDefinitionKey(processDefinitionId);
        processStartDialog = new ProjectStructureProcessStartDialogState(
            ProjectId,
            processDefinitionId,
            definitionKey,
            processNodeId,
            processNodeTitle,
            targetNode?.Id,
            targetNode?.Title ?? string.Empty,
            null,
            ProjectStructureProcessStartStage.Confirm,
            false,
            false,
            string.Empty,
            [],
            ProjectStructureHrManagerName,
            DateTimeOffset.UtcNow,
            false,
            string.Empty)
        {
            EstimateOnlyMode = estimateOnly
        };

        await InvokeAsync(StateHasChanged);
        if (estimateOnly)
        {
            await ExecuteProcessStartAsync();
        }
    }

    private void CloseProcessStartDialog()
    {
        ResetProcessStartEstimateState();
        processStartDialog = null;
    }

    private async Task ReviewAndStartProcessAsync()
    {
        if (processStartDialog is null)
        {
            return;
        }

        processStartDialog = processStartDialog with
        {
            AssignmentsReviewed = true,
            Error = string.Empty
        };
        await ExecuteProcessStartAsync();
    }

    private async Task ExecuteProcessStartAsync()
    {
        if (processStartDialog is null)
        {
            return;
        }

        var dialog = processStartDialog;
        if (dialog.ProcessDefinitionId == Guid.Empty)
        {
            processStartDialog = dialog with { Error = "The selected process definition id is missing." };
            await InvokeAsync(StateHasChanged);
            return;
        }

        try
        {
            if (dialog.Stage == ProjectStructureProcessStartStage.Confirm)
            {
                await PreviewProcessStartAsync(
                    dialog,
                    "Launch plan prepared. Review the resolved assignments before starting.",
                    runReadiness: false);
                return;
            }

            if (!dialog.AssignmentsReviewed)
            {
                processStartDialog = dialog with
                {
                    IsBusy = false,
                    Error = "Review the proposed role assignments and confirm them before starting the process."
                };
                await InvokeAsync(StateHasChanged);
                return;
            }

            if (dialog.RequiredGapCount > 0)
            {
                processStartDialog = dialog with
                {
                    IsBusy = false,
                    Error = "Resolve every required role before starting the process."
                };
                await InvokeAsync(StateHasChanged);
                return;
            }

            processStartDialog = dialog with
            {
                IsBusy = true,
                Error = string.Empty,
                ConfirmHrManagerMatch = false,
                StatusMessage = "Starting the reviewed process run."
            };
            await InvokeAsync(StateHasChanged);

            var launchRequest = CreateProcessLaunchRequest(dialog, execute: true, runReadiness: true);
            var result = await ProcessLaunchService.LaunchAsync(launchRequest);
            if (result.Stage is ProcessLaunchStage.Blocked or ProcessLaunchStage.Failed || result.RunId is null)
            {
                processStartDialog = dialog with
                {
                    IsBusy = false,
                    Error = result.Warnings.Count == 0
                        ? $"Process launch returned {result.Stage}."
                        : string.Join(" ", result.Warnings)
                };
                await InvokeAsync(StateHasChanged);
                return;
            }

            var feedbackMessage = $"{dialog.NodeTitle} started for {dialog.TargetNodeTitle}.";
            processStartDialog = null;
            workflowFeedback = feedbackMessage;
            workflowFeedbackTone = "mint";
            await InvokeAsync(StateHasChanged);
            await TryLinkStartedProcessRunAsync(dialog.TargetNodeId, result.RunId.Value);
            Navigation.NavigateTo(AppendProcessStartedQuery(result.Route));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            await SetProcessActionExceptionAsync(exception, "starting the process");
        }
    }

    private async Task PreviewProcessStartAsync(
        ProjectStructureProcessStartDialogState dialog,
        string statusMessage,
        bool runReadiness)
    {
        processStartDialog = dialog with
        {
            IsBusy = true,
            Error = string.Empty,
            ConfirmHrManagerMatch = false,
            StatusMessage = statusMessage
        };
        CancelProcessStartHistoricalEstimateRefresh();
        await InvokeAsync(StateHasChanged);

        using var previewTimeout = new CancellationTokenSource(ProcessStartPreviewTimeout);
        try
        {
            await RefreshProcessStartAgentMetadataAsync(previewTimeout.Token);
            var preview = await ProcessLaunchService.PreviewAsync(
                CreateProcessLaunchRequest(dialog, execute: false, runReadiness),
                previewTimeout.Token);
            var previewStatusMessage = preview.Stage == ProcessLaunchStage.Blocked && preview.Warnings.Count > 0
                ? string.Join(" ", preview.Warnings)
                : statusMessage;
            processStartDialog = MapProcessStartDialogState(
                dialog,
                preview.LaunchPlan,
                previewStatusMessage,
                string.Empty);
            QueueProcessStartHistoricalEstimateRefresh(preview.LaunchPlan, processStartEstimateAssignmentCount);
            await InvokeAsync(StateHasChanged);
        }
        catch (OperationCanceledException exception) when (previewTimeout.IsCancellationRequested)
        {
            Logger.LogWarning(
                exception,
                "Project structure process launch preview timed out. ProjectId={ProjectId} ProcessDefinitionId={ProcessDefinitionId} NodeId={NodeId} RunReadiness={RunReadiness}",
                ProjectId,
                dialog.ProcessDefinitionId,
                dialog.NodeId,
                runReadiness);
            processStartDialog = dialog with
            {
                IsBusy = false,
                ConfirmHrManagerMatch = false,
                Error = $"Process launch preview did not finish within {ProcessStartPreviewTimeout.TotalSeconds:N0} seconds. Try again after checking the agent/provider catalog."
            };
            await InvokeAsync(StateHasChanged);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            await SetProcessActionExceptionAsync(exception, "preparing the process launch preview");
        }
    }

    private static string AppendProcessStartedQuery(string route)
    {
        if (string.IsNullOrWhiteSpace(route))
        {
            return "/processes/live?processStarted=1";
        }

        var separator = route.Contains('?', StringComparison.Ordinal) ? '&' : '?';
        return $"{route}{separator}processStarted=1";
    }

    private Task SelectProcessStartCandidateAsync(ProjectStructureProcessStartCandidateSelection selection)
    {
        if (processStartDialog is null)
        {
            return Task.CompletedTask;
        }

        var roles = processStartDialog.Roles
            .Select(role => role.LaunchPlanRoleId == selection.LaunchPlanRoleId
                ? SelectCandidate(role, selection.CandidateId)
                : role)
            .ToList();
        processStartDialog = processStartDialog with
        {
            Roles = roles,
            Estimate = BuildCurrentProcessStartEstimate(roles),
            AssignmentsReviewed = false,
            Error = string.Empty,
            StatusMessage = "Role selection updated. Review the assignments before starting."
        };
        return InvokeAsync(StateHasChanged);
    }

    private Task OpenManualProcessStartAgentPickerAsync(Guid launchPlanRoleId)
    {
        if (processStartDialog is null)
        {
            return Task.CompletedTask;
        }

        processStartDialog = processStartDialog with
        {
            StatusMessage = "Agent picker opened with compatible active agents from the directory.",
            Error = string.Empty
        };
        return InvokeAsync(StateHasChanged);
    }

    private Task HandleProcessStartAssignmentsReviewedChanged(ChangeEventArgs args)
    {
        if (processStartDialog is null)
        {
            return Task.CompletedTask;
        }

        var isChecked = args.Value switch
        {
            bool value => value,
            string value when bool.TryParse(value, out var parsed) => parsed,
            _ => false
        };
        processStartDialog = processStartDialog with
        {
            AssignmentsReviewed = isChecked,
            Error = string.Empty,
            StatusMessage = isChecked
                ? "Assignments confirmed. The process can start when every required role is resolved."
                : "Review the assignments below and confirm them before starting the process."
        };
        return InvokeAsync(StateHasChanged);
    }

    private Task RequestHrManagerMatchAsync()
    {
        if (processStartDialog is null)
        {
            return Task.CompletedTask;
        }

        processStartDialog = processStartDialog with
        {
            ConfirmHrManagerMatch = true,
            Error = string.Empty
        };
        return InvokeAsync(StateHasChanged);
    }

    private Task CancelHrManagerMatchAsync()
    {
        if (processStartDialog is null)
        {
            return Task.CompletedTask;
        }

        processStartDialog = processStartDialog with
        {
            ConfirmHrManagerMatch = false,
            Error = string.Empty
        };
        return InvokeAsync(StateHasChanged);
    }

    private async Task ExecuteHrManagerMatchAsync()
    {
        if (processStartDialog is null)
        {
            return;
        }

        try
        {
            await PreviewProcessStartAsync(
                processStartDialog,
                $"{ProjectStructureHrManagerName} refreshed the staffing suggestions from the active agent directory.",
                runReadiness: true);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            await SetProcessActionExceptionAsync(exception, "requesting HR manager staffing");
        }
    }

    private ProcessLaunchRequest CreateProcessLaunchRequest(
        ProjectStructureProcessStartDialogState dialog,
        bool execute,
        bool runReadiness = true)
    {
        var processNode = ResolveNode(dialog.NodeId);
        var targetNode = ResolveNode(dialog.TargetNodeId) ?? processNode;
        var variables = CreateProcessLaunchVariables(dialog, processNode, targetNode);
        return new ProcessLaunchRequest(
            DefinitionKey: string.IsNullOrWhiteSpace(dialog.DefinitionKey) ? null : dialog.DefinitionKey,
            new ProcessDefinitionId(dialog.ProcessDefinitionId),
            LiveRunProfileKey: null,
            ProjectId,
            ProjectNodeId: targetNode?.Id ?? dialog.TargetNodeId,
            RequestedBy: "project-structure",
            variables,
            RunReadiness: runReadiness,
            execute)
        {
            ExecutorOverrides = CreateExecutorOverrides(dialog)
        };
    }

    private IReadOnlyDictionary<string, string> CreateProcessLaunchVariables(
        ProjectStructureProcessStartDialogState dialog,
        ProjectStructureNode? processNode,
        ProjectStructureNode? targetNode)
    {
        var variables = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["LaunchSource"] = "project-structure-ui",
            ["ProjectId"] = ProjectId.ToString("D"),
            ["ProjectName"] = surface?.ProjectName ?? string.Empty,
            ["AgentId"] = "project-structure-ui",
            ["AgentName"] = "Project structure UI",
            ["MachineName"] = Environment.MachineName,
            ["BranchName"] = string.Empty,
            ["SessionId"] = $"project-structure-ui-{Guid.NewGuid():N}"
        };

        AddNodeVariables(variables, "ProcessNode", processNode);
        AddNodeVariables(variables, "ProjectNode", targetNode);
        if (processNode is null)
        {
            variables["ProcessNodeId"] = dialog.NodeId;
            variables["ProcessNodeTitle"] = dialog.NodeTitle;
            variables["ProcessNodeSubtitle"] = string.Empty;
            variables["ProcessNodeStatus"] = string.Empty;
            variables["ProcessNodeNotes"] = string.Empty;
            variables["ProcessNodeObjectType"] = ProjectObjectType.ProcessDefinition.ToString();
            variables["ProcessNodeObjectSubtype"] = string.Empty;
        }

        var contextSummary = BuildProjectStructureContextSummary(surface, targetNode);
        if (!string.IsNullOrWhiteSpace(contextSummary))
        {
            variables["ProjectStructureContextSummary"] = contextSummary;
        }

        var outputRoot = targetNode is null || surface is null
            ? ResolveOutputRoot(targetNode)
            : ResolveOutputRoot(surface, targetNode);
        if (!string.IsNullOrWhiteSpace(outputRoot))
        {
            ApplyProductRootLaunchVariables(variables, outputRoot);
        }

        ApplyProcessLaunchVariableContributors(
            variables,
            dialog,
            targetNode);

        return variables;
    }

    private void ApplyProcessLaunchVariableContributors(
        IDictionary<string, string> variables,
        ProjectStructureProcessStartDialogState dialog,
        ProjectStructureNode? targetNode)
    {
        if (surface is null ||
            targetNode is null ||
            string.IsNullOrWhiteSpace(dialog.DefinitionKey))
        {
            return;
        }

        var contextSummary = variables.TryGetValue("ProjectStructureContextSummary", out var value)
            ? value
            : string.Empty;
        var context = ProjectStructureProcessLaunchSourceSnapshotMapper.Create(
            surface,
            targetNode,
            dialog.DefinitionKey,
            isSubprocess: false,
            contextSummary);
        ProcessLaunchVariablePreparationService.Enrich(context, variables);
    }

    private string ResolveProcessDefinitionKey(Guid processDefinitionId)
    {
        return processDefinitionId == Guid.Empty
            ? string.Empty
            : ProcessDefinitionCatalogService.ResolveDefinitionKey(new ProcessDefinitionId(processDefinitionId));
    }

    private static string BuildProjectStructureContextSummary(
        ProjectStructureSurface? currentSurface,
        ProjectStructureNode? focusNode)
    {
        if (currentSurface is null || focusNode is null)
        {
            return string.Empty;
        }

        var contextRows = EnumerateProjectStructureContextNodes(currentSurface, focusNode);
        var rows = contextRows
            .Take(40)
            .ToArray();
        if (rows.Length == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        builder.AppendLine($"Project structure source: {currentSurface.ProjectName} ({currentSurface.ProjectId:D}).");
        builder.AppendLine($"Selected node: {focusNode.Title} ({focusNode.Id}).");
        AppendVisualTargetAssetSummary(builder, contextRows);
        foreach (var (node, depth) in rows)
        {
            var marker = string.Equals(node.Id, focusNode.Id, StringComparison.Ordinal)
                ? " [selected]"
                : string.Empty;
            var subtype = string.IsNullOrWhiteSpace(node.ObjectSubtype)
                ? node.ObjectType.ToString()
                : $"{node.ObjectType}/{node.ObjectSubtype}";
            var notes = NormalizeProcessContextText(string.Join(" ", node.Subtitle, node.Notes), 420);
            var indent = depth <= 0 ? string.Empty : new string(' ', Math.Min(depth, 8) * 2);

            builder.Append("- ");
            builder.Append(indent);
            builder.Append(node.Title);
            builder.Append(marker);
            builder.Append(" [");
            builder.Append(subtype);
            builder.Append("; ");
            builder.Append(string.IsNullOrWhiteSpace(node.Status) ? "Draft" : node.Status);
            builder.Append(']');
            if (!string.IsNullOrWhiteSpace(notes))
            {
                builder.Append(": ");
                builder.Append(notes);
            }

            builder.AppendLine();
        }

        return builder.ToString().TrimEnd();
    }

    private static void AppendVisualTargetAssetSummary(
        StringBuilder builder,
        IReadOnlyList<(ProjectStructureNode Node, int Depth)> contextRows)
    {
        var assets = contextRows
            .Select(row => row.Node)
            .Where(IsVisualTargetAsset)
            .Take(8)
            .ToArray();
        if (assets.Length == 0)
        {
            return;
        }

        builder.AppendLine("Visual target assets:");
        foreach (var asset in assets)
        {
            var subtype = string.IsNullOrWhiteSpace(asset.ObjectSubtype)
                ? asset.ObjectType.ToString()
                : $"{asset.ObjectType}/{asset.ObjectSubtype}";
            var media = string.IsNullOrWhiteSpace(asset.MediaRelativePath)
                ? "no media path"
                : asset.MediaRelativePath;
            var fileName = string.IsNullOrWhiteSpace(asset.MediaOriginalFileName)
                ? "unknown file"
                : asset.MediaOriginalFileName;
            var contentType = string.IsNullOrWhiteSpace(asset.MediaContentType)
                ? "unknown content type"
                : asset.MediaContentType;
            var notes = NormalizeProcessContextText(string.Join(" ", asset.Subtitle, asset.Notes), 360);

            builder.Append("- ");
            builder.Append(asset.Title);
            builder.Append(" (");
            builder.Append(asset.Id);
            builder.Append(") [");
            builder.Append(subtype);
            builder.Append("; ");
            builder.Append(contentType);
            builder.Append("; media=");
            builder.Append(media);
            builder.Append("; file=");
            builder.Append(fileName);
            builder.Append("; parent=");
            builder.Append(asset.ParentId ?? "none");
            builder.Append(']');
            if (!string.IsNullOrWhiteSpace(notes))
            {
                builder.Append(": ");
                builder.Append(notes);
            }

            builder.AppendLine();
        }

        builder.AppendLine("Visual target rule: implementation and QA must fetch or analyze the relevant asset content before accepting visual alignment; do not rely only on this text summary or on generated app screenshots in isolation.");
    }

    private static bool IsVisualTargetAsset(ProjectStructureNode node)
    {
        if (!ProjectStructureProcessContextNodeFilter.ShouldIncludeInProcessContext(node))
        {
            return false;
        }

        if (node.ObjectType != ProjectObjectType.ImageAsset)
        {
            return false;
        }

        if (string.Equals(node.ObjectSubtype, "screenshot", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(node.ArtifactKind, "process-run-screenshot", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (string.Equals(node.ObjectSubtype, "generated", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(node.ObjectSubtype, "layout-recommendation", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var searchableText = string.Join(" ", node.Title, node.Subtitle, node.Notes, node.ObjectSubtype, node.ArtifactKind);
        return ContainsVisualTargetKeyword(searchableText);
    }

    private static bool ContainsVisualTargetKeyword(string text)
        => text.Contains("visual", StringComparison.OrdinalIgnoreCase) ||
           text.Contains("target", StringComparison.OrdinalIgnoreCase) ||
           text.Contains("proposal", StringComparison.OrdinalIgnoreCase) ||
           text.Contains("mockup", StringComparison.OrdinalIgnoreCase) ||
           text.Contains("wireframe", StringComparison.OrdinalIgnoreCase) ||
           text.Contains("layout", StringComparison.OrdinalIgnoreCase) ||
           text.Contains("design", StringComparison.OrdinalIgnoreCase) ||
           text.Contains("look", StringComparison.OrdinalIgnoreCase) ||
           text.Contains("ui", StringComparison.OrdinalIgnoreCase);

    private static IReadOnlyList<(ProjectStructureNode Node, int Depth)> EnumerateProjectStructureContextNodes(
        ProjectStructureSurface currentSurface,
        ProjectStructureNode focusNode)
    {
        var projectRootNodeId = ProjectWorkbenchGraphConventions.BuildProjectRootNodeKey(currentSurface.ProjectId);
        var contextNodes = currentSurface.Nodes
            .Where(node =>
                string.Equals(node.Id, focusNode.Id, StringComparison.Ordinal) ||
                ProjectStructureProcessContextNodeFilter.ShouldIncludeInProcessContext(node))
            .ToArray();
        var childrenByParent = contextNodes
            .Where(node => !string.IsNullOrWhiteSpace(node.ParentId))
            .GroupBy(node => node.ParentId!, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.OrderBy(node => node.Y).ThenBy(node => node.X).ThenBy(node => node.Title, StringComparer.OrdinalIgnoreCase).ToArray(),
                StringComparer.Ordinal);
        var rows = new List<(ProjectStructureNode Node, int Depth)>();
        var visited = new HashSet<string>(StringComparer.Ordinal);

        void Visit(ProjectStructureNode node, int depth)
        {
            if (!visited.Add(node.Id))
            {
                return;
            }

            rows.Add((node, depth));
            if (!childrenByParent.TryGetValue(node.Id, out var children))
            {
                return;
            }

            foreach (var child in children)
            {
                Visit(child, depth + 1);
            }
        }

        if (childrenByParent.TryGetValue(projectRootNodeId, out var rootChildren))
        {
            foreach (var rootChild in rootChildren)
            {
                Visit(rootChild, 0);
            }
        }

        foreach (var node in contextNodes.OrderBy(node => node.Title, StringComparer.OrdinalIgnoreCase))
        {
            Visit(node, 0);
        }

        if (rows.Any(row => string.Equals(row.Node.Id, focusNode.Id, StringComparison.Ordinal)))
        {
            return rows;
        }

        return [(focusNode, 0), .. rows];
    }

    private static void AddNodeVariables(
        IDictionary<string, string> variables,
        string prefix,
        ProjectStructureNode? node)
    {
        if (node is null)
        {
            return;
        }

        variables[$"{prefix}Id"] = node.Id;
        variables[$"{prefix}Title"] = node.Title;
        variables[$"{prefix}Subtitle"] = node.Subtitle;
        variables[$"{prefix}Status"] = node.Status;
        variables[$"{prefix}Notes"] = node.Notes;
        variables[$"{prefix}ObjectType"] = node.ObjectType.ToString();
        variables[$"{prefix}ObjectSubtype"] = node.ObjectSubtype;
        if (node.RelatedProjectId is { } relatedProjectId)
        {
            variables[$"{prefix}RelatedProjectId"] = relatedProjectId.ToString("D");
        }
    }

    private IReadOnlyList<ProcessLaunchExecutorOverride> CreateExecutorOverrides(ProjectStructureProcessStartDialogState dialog)
    {
        if (dialog.Stage != ProjectStructureProcessStartStage.Staffing)
        {
            return [];
        }

        return dialog.Roles
            .Select(role =>
            {
                var selectedCandidate = role.Candidates.FirstOrDefault(candidate => candidate.IsSelected && candidate.IsResolvable);
                if (selectedCandidate is null ||
                    selectedCandidate.TechnicalAgentId is not { } agentId ||
                    string.IsNullOrWhiteSpace(role.StepKey) ||
                    string.IsNullOrWhiteSpace(role.RoleKey))
                {
                    return null;
                }

                return new ProcessLaunchExecutorOverride(
                    role.StepKey,
                    role.RoleKey,
                    ProcessLaunchExecutorKinds.Agent,
                    agentId.ToString("D"),
                    selectedCandidate.DisplayName,
                    selectedCandidate.IsRecommended
                        ? "Accepted HR manager recommendation during project-structure launch review."
                        : "Selected during project-structure launch review.");
            })
            .Where(item => item is not null)
            .Cast<ProcessLaunchExecutorOverride>()
            .ToList();
    }

    private ProjectStructureProcessStartDialogState MapProcessStartDialogState(
        ProjectStructureProcessStartDialogState dialog,
        ProcessLaunchPlanView launchPlan,
        string statusMessage,
        string error)
    {
        var steps = launchPlan.Steps
            .Where(step =>
                !string.IsNullOrWhiteSpace(step.RoleKey) ||
                !string.IsNullOrWhiteSpace(step.ExecutorKind) ||
                step.IsBlocked)
            .ToList();
        if (steps.Count == 0)
        {
            steps = launchPlan.Steps.ToList();
        }

        var roles = steps.Select(MapProcessStartRoleState).ToList();
        processStartEstimateDefinitionKey = launchPlan.DefinitionKey;
        processStartEstimateAssignmentCount = steps.Count;
        var estimate = BuildProcessStartEstimate(
            launchPlan.DefinitionKey,
            steps.Count,
            roles,
            historicalCostEstimate: null);
        var resolvedAssignmentMessage =
            $"Resolved {steps.Count(step => !step.IsBlocked && !string.IsNullOrWhiteSpace(step.ExecutorId))} of {steps.Count} assignments.";
        var baseStatusMessage = string.IsNullOrWhiteSpace(statusMessage)
            ? resolvedAssignmentMessage
            : statusMessage;
        return dialog with
        {
            DefinitionKey = string.IsNullOrWhiteSpace(dialog.DefinitionKey) ? launchPlan.DefinitionKey : dialog.DefinitionKey,
            LaunchPlanId = launchPlan.PlanId.Value,
            Stage = ProjectStructureProcessStartStage.Staffing,
            IsBusy = false,
            ConfirmHrManagerMatch = false,
            StatusMessage = AppendProcessStartEstimateStatusMessage(
                baseStatusMessage,
                "Provider price estimate is visible while historical run costs load."),
            StageActivatedAtUtc = DateTimeOffset.UtcNow,
            AssignmentsReviewed = false,
            Roles = roles,
            Estimate = estimate,
            Error = error
        };
    }

    private ProjectStructureProcessStartRoleState MapProcessStartRoleState(ProcessLaunchStepView step)
    {
        var directoryCandidates = BuildProcessStartDirectoryCandidates(step);
        var candidates = BuildProcessStartCandidates(step, directoryCandidates);
        var selected = candidates.FirstOrDefault(candidate => candidate.IsSelected);
        var isResolved = !step.IsBlocked && selected?.IsResolvable == true;
        return new ProjectStructureProcessStartRoleState(
            step.StepInstanceId.Value,
            ResolveStepDisplayName(step),
            string.IsNullOrWhiteSpace(step.ExecutorKind) ? ProcessLaunchExecutorKinds.Agent : step.ExecutorKind,
            IsRequired: true,
            isResolved,
            RequiresProvisioning: false,
            isResolved
                ? $"Selected {selected!.DisplayName}."
                : step.BlockedReason ?? "No active executor is resolved for this step.",
            step.BlockedReason ?? "Resolved from active process launch executor catalog.",
            candidates)
        {
            StepKey = step.StepKey,
            RoleKey = step.RoleKey,
            DirectoryCandidates = directoryCandidates
        };
    }

    private IReadOnlyList<ProjectStructureProcessStartCandidateState> BuildProcessStartCandidates(
        ProcessLaunchStepView step,
        IReadOnlyList<ProjectStructureProcessStartCandidateState> directoryCandidates)
    {
        var candidates = new List<ProjectStructureProcessStartCandidateState>();
        var selectedCandidate = directoryCandidates.FirstOrDefault(candidate => candidate.IsSelected);

        if (selectedCandidate is not null)
        {
            candidates.Add(selectedCandidate);
        }

        foreach (var candidate in directoryCandidates
            .Where(candidate => selectedCandidate is null || candidate.CandidateId != selectedCandidate.CandidateId)
            .OrderByDescending(candidate => candidate.IsRecommended)
            .ThenByDescending(ResolveCandidateScore)
            .ThenBy(candidate => candidate.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Take(ProcessStartInlineCandidateLimit))
        {
            candidates.Add(candidate);
        }

        if (candidates.Count == 0)
        {
            candidates.Add(new ProjectStructureProcessStartCandidateState(
                step.StepInstanceId.Value,
                TechnicalAgentId: null,
                "No active agent available",
                "Gap",
                string.IsNullOrWhiteSpace(step.ExecutorKind) ? ProcessLaunchExecutorKinds.Agent : step.ExecutorKind,
                "0.0 score",
                IsSelected: true,
                IsRecommended: false,
                RequiresProvisioning: true,
                IsResolvable: false,
                step.BlockedReason ?? "No active agent with an enabled provider was found for this role.",
                "Provision or enable an agent before launch.",
                "process-launch/gap",
                MatchScore: 0));
        }

        return candidates;
    }

    private IReadOnlyList<ProjectStructureProcessStartCandidateState> BuildProcessStartDirectoryCandidates(ProcessLaunchStepView step)
    {
        var selectedAgentId = Guid.TryParse(step.ExecutorId, out var parsedAgentId)
            ? parsedAgentId
            : (Guid?)null;
        var candidates = processStartAgentMetadataById.Values
            .Select(agent => new { Agent = agent, Readiness = EvaluateAgentForStep(agent, step) })
            .Where(item => item.Readiness.IsExecutionReady && item.Readiness.HasRoleFit)
            .OrderByDescending(item => item.Readiness.Score)
            .ThenBy(item => item.Agent.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Select(item => CreateCandidateState(
                step,
                item.Agent.AgentId,
                isSelected: selectedAgentId.HasValue && item.Agent.AgentId == selectedAgentId.Value,
                isRecommended: true))
            .ToList();

        if (selectedAgentId.HasValue &&
            candidates.All(candidate => candidate.CandidateId != selectedAgentId.Value))
        {
            candidates.Insert(
                0,
                CreateCandidateState(
                    step,
                    selectedAgentId.Value,
                    isSelected: true,
                    isRecommended: true));
        }

        return candidates;
    }

    private ProjectStructureProcessStartCandidateState CreateCandidateState(
        ProcessLaunchStepView step,
        Guid agentId,
        bool isSelected,
        bool isRecommended)
    {
        processStartAgentMetadataById.TryGetValue(agentId, out var metadata);
        var displayName = metadata?.DisplayName ??
            (!string.IsNullOrWhiteSpace(step.ExecutorDisplayName) ? step.ExecutorDisplayName : $"Agent {agentId:D}");
        var readiness = metadata is null ? null : EvaluateAgentForStep(metadata, step);
        var isResolvable = metadata is null || readiness?.IsExecutionReady == true;
        var isRoleFit = metadata is null || readiness?.HasRoleFit == true;
        var isReadyRecommendation = isRecommended && isResolvable && isRoleFit;
        var roleScore = readiness?.Score ?? 0;
        var summary = readiness is null
            ? $"Available active agent for step '{step.StepKey}'."
            : readiness.IsExecutionReady && readiness.HasRoleFit
                ? $"{readiness.MatchSummary}. {readiness.ReadinessSummary}"
                : readiness.ReadinessSummary;
        return new ProjectStructureProcessStartCandidateState(
            agentId,
            agentId,
            displayName,
            "Agent",
            string.IsNullOrWhiteSpace(step.ExecutorKind) ? ProcessLaunchExecutorKinds.Agent : step.ExecutorKind,
            FormatProcessStartCandidateScore(roleScore),
            isSelected,
            isReadyRecommendation,
            RequiresProvisioning: !isResolvable,
            IsResolvable: isResolvable,
            summary,
            metadata?.StatusLabel ?? "Active",
            metadata?.ProviderName ?? "agent-directory",
            metadata?.ProviderName ?? string.Empty,
            metadata?.Model ?? string.Empty,
            metadata?.RoleTitle ?? string.Empty,
            metadata?.Summary ?? string.Empty,
            metadata?.StatusLabel ?? string.Empty,
            metadata?.WorkloadLabel ?? string.Empty,
            metadata?.AvatarImageUrl ?? string.Empty,
            metadata?.ToolNames,
            metadata?.SkillNames,
            roleScore);
    }

    private async Task RefreshProcessStartAgentMetadataAsync(CancellationToken cancellationToken)
    {
        try
        {
            var referenceData = await AgentReferenceDataProvider.GetAsync(
                AgentReferenceDataRequest.AgentsAndProviders(activeAgentsOnly: true),
                cancellationToken);
            var providerById = referenceData.ProviderById;
            processStartAgentMetadataById = referenceData.Agents
                .ToDictionary(
                    agent => agent.Id,
                    agent =>
                    {
                        ProviderProfile? provider = null;
                        if (agent.ProviderProfileId.HasValue)
                        {
                            providerById.TryGetValue(agent.ProviderProfileId.Value, out provider);
                        }

                        return ProjectStructureProcessStartAgentMetadata.FromAgent(agent, provider);
                    });
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            Logger.LogDebug(exception, "Agent provider metadata could not be loaded for process assignment badges.");
            var referenceData = await AgentReferenceDataProvider.GetAsync(
                new AgentReferenceDataRequest(
                    AgentReferenceDataSections.Agents,
                    ActiveAgentsOnly: true),
                cancellationToken);
            processStartAgentMetadataById = referenceData.Agents.ToDictionary(
                agent => agent.Id,
                agent => ProjectStructureProcessStartAgentMetadata.FromAgent(agent, provider: null));
        }
    }

    private ProjectStructureProcessStartRoleState SelectCandidate(
        ProjectStructureProcessStartRoleState role,
        Guid candidateId)
    {
        var selectedCandidate = role.DirectoryCandidates
            .Concat(role.Candidates)
            .FirstOrDefault(candidate => candidate.CandidateId == candidateId);
        if (selectedCandidate is null || !selectedCandidate.IsResolvable)
        {
            return role;
        }

        var candidates = EnsureCandidatePresent(role.Candidates, selectedCandidate)
            .Select(candidate => candidate with { IsSelected = candidate.CandidateId == candidateId })
            .ToList();
        var directoryCandidates = EnsureCandidatePresent(role.DirectoryCandidates, selectedCandidate)
            .Select(candidate => candidate with { IsSelected = candidate.CandidateId == candidateId })
            .ToList();
        return role with
        {
            IsResolved = true,
            RequiresProvisioning = selectedCandidate.RequiresProvisioning,
            SelectionSummary = $"Selected {selectedCandidate.DisplayName}.",
            Candidates = candidates,
            DirectoryCandidates = directoryCandidates
        };
    }

    private static IReadOnlyList<ProjectStructureProcessStartCandidateState> EnsureCandidatePresent(
        IReadOnlyList<ProjectStructureProcessStartCandidateState> candidates,
        ProjectStructureProcessStartCandidateState selectedCandidate)
    {
        if (candidates.Any(candidate => candidate.CandidateId == selectedCandidate.CandidateId))
        {
            return candidates;
        }

        return [selectedCandidate, .. candidates];
    }

    private ProjectStructureProcessEstimateSummary? BuildCurrentProcessStartEstimate(
        IReadOnlyList<ProjectStructureProcessStartRoleState> roles,
        ProcessHistoricalRunCostEstimate? historicalCostEstimate = null)
    {
        var definitionKey = FirstNonEmpty(processStartEstimateDefinitionKey, processStartDialog?.NodeTitle);
        if (string.IsNullOrWhiteSpace(definitionKey))
        {
            return null;
        }

        var assignmentCount = processStartEstimateAssignmentCount > 0
            ? processStartEstimateAssignmentCount
            : roles.Count;
        return BuildProcessStartEstimate(definitionKey, assignmentCount, roles, historicalCostEstimate);
    }

    private ProjectStructureProcessEstimateSummary BuildProcessStartEstimate(
        string definitionKey,
        int assignmentCount,
        IReadOnlyList<ProjectStructureProcessStartRoleState> roles,
        ProcessHistoricalRunCostEstimate? historicalCostEstimate)
    {
        var assignments = BuildProcessStartEstimateAssignments(roles);
        return ProjectStructureProcessStartEstimateCalculator.Calculate(
            definitionKey,
            assignmentCount,
            assignments,
            historicalCostEstimate);
    }

    private IReadOnlyList<ProjectStructureProcessStartEstimateAssignment> BuildProcessStartEstimateAssignments(
        IReadOnlyList<ProjectStructureProcessStartRoleState> roles)
    {
        return roles
            .Select(role =>
            {
                var selectedCandidate = role.Candidates.FirstOrDefault(candidate => candidate.IsSelected);
                if (selectedCandidate?.TechnicalAgentId is not { } agentId ||
                    !processStartAgentMetadataById.TryGetValue(agentId, out var metadata))
                {
                    return new ProjectStructureProcessStartEstimateAssignment(null, string.Empty, null);
                }

                return new ProjectStructureProcessStartEstimateAssignment(
                    agentId,
                    FirstNonEmpty(selectedCandidate.AgentModel, metadata.Model),
                    metadata.ProviderProfile);
            })
            .ToList();
    }

    private void QueueProcessStartHistoricalEstimateRefresh(ProcessLaunchPlanView launchPlan, int assignmentCount)
    {
        CancelProcessStartHistoricalEstimateRefresh();
        var refreshCts = new CancellationTokenSource(ProcessStartHistoricalEstimateTimeout);
        processStartHistoricalEstimateRefreshCts = refreshCts;
        _ = RefreshProcessStartHistoricalEstimateAsync(
            launchPlan.DefinitionId,
            launchPlan.DefinitionKey,
            launchPlan.PlanId,
            assignmentCount,
            refreshCts);
    }

    private async Task RefreshProcessStartHistoricalEstimateAsync(
        ProcessDefinitionId definitionId,
        string definitionKey,
        ProcessInstancePlanId launchPlanId,
        int assignmentCount,
        CancellationTokenSource refreshCts)
    {
        try
        {
            var historicalCostEstimate = await ProcessHistoricalRunCostReader.ReadAsync(
                new ProcessHistoricalRunCostQuery(
                    definitionId,
                    definitionKey,
                    DateTimeOffset.UtcNow),
                refreshCts.Token);
            if (!ReferenceEquals(processStartHistoricalEstimateRefreshCts, refreshCts))
            {
                return;
            }

            await InvokeAsync(() =>
            {
                if (!CanApplyProcessStartHistoricalEstimate(definitionId, launchPlanId, refreshCts) ||
                    processStartDialog is null)
                {
                    return;
                }

                var estimate = BuildProcessStartEstimate(
                    definitionKey,
                    assignmentCount,
                    processStartDialog.Roles,
                    historicalCostEstimate);
                processStartDialog = processStartDialog with
                {
                    Estimate = estimate,
                    StatusMessage = AppendProcessStartEstimateStatusMessage(
                        processStartDialog.StatusMessage,
                        ResolveProcessStartHistoricalEstimateStatus(historicalCostEstimate))
                };
                StateHasChanged();
            });
        }
        catch (OperationCanceledException exception) when (refreshCts.IsCancellationRequested)
        {
            Logger.LogDebug(
                exception,
                "Project structure historical process estimate was cancelled or timed out. ProjectId={ProjectId} ProcessDefinitionId={ProcessDefinitionId} LaunchPlanId={LaunchPlanId} TimeoutSeconds={TimeoutSeconds}",
                ProjectId,
                definitionId.Value,
                launchPlanId.Value,
                ProcessStartHistoricalEstimateTimeout.TotalSeconds);
            if (!ReferenceEquals(processStartHistoricalEstimateRefreshCts, refreshCts))
            {
                return;
            }

            await InvokeAsync(() =>
            {
                if (!CanApplyProcessStartHistoricalEstimate(definitionId, launchPlanId, refreshCts) ||
                    processStartDialog is null)
                {
                    return;
                }

                processStartDialog = processStartDialog with
                {
                    StatusMessage = AppendProcessStartEstimateStatusMessage(
                        processStartDialog.StatusMessage,
                        $"Historical cost lookup did not finish within {ProcessStartHistoricalEstimateTimeout.TotalSeconds:N0} seconds; provider price estimate remains visible.")
                };
                StateHasChanged();
            });
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            Logger.LogWarning(
                exception,
                "Project structure historical process estimate failed. ProjectId={ProjectId} ProcessDefinitionId={ProcessDefinitionId} LaunchPlanId={LaunchPlanId}",
                ProjectId,
                definitionId.Value,
                launchPlanId.Value);
            if (!ReferenceEquals(processStartHistoricalEstimateRefreshCts, refreshCts))
            {
                return;
            }

            await InvokeAsync(() =>
            {
                if (!CanApplyProcessStartHistoricalEstimate(definitionId, launchPlanId, refreshCts) ||
                    processStartDialog is null)
                {
                    return;
                }

                processStartDialog = processStartDialog with
                {
                    StatusMessage = AppendProcessStartEstimateStatusMessage(
                        processStartDialog.StatusMessage,
                        "Historical cost lookup failed; provider price estimate remains visible.")
                };
                StateHasChanged();
            });
        }
        finally
        {
            if (ReferenceEquals(processStartHistoricalEstimateRefreshCts, refreshCts))
            {
                processStartHistoricalEstimateRefreshCts = null;
            }

            refreshCts.Dispose();
        }
    }

    private bool CanApplyProcessStartHistoricalEstimate(
        ProcessDefinitionId definitionId,
        ProcessInstancePlanId launchPlanId,
        CancellationTokenSource refreshCts)
    {
        return ReferenceEquals(processStartHistoricalEstimateRefreshCts, refreshCts) &&
               processStartDialog is { } dialog &&
               dialog.ProcessDefinitionId == definitionId.Value &&
               dialog.LaunchPlanId == launchPlanId.Value;
    }

    private void CancelProcessStartHistoricalEstimateRefresh()
    {
        var refreshCts = processStartHistoricalEstimateRefreshCts;
        processStartHistoricalEstimateRefreshCts = null;
        refreshCts?.Cancel();
    }

    private void ResetProcessStartEstimateState()
    {
        CancelProcessStartHistoricalEstimateRefresh();
        processStartEstimateDefinitionKey = string.Empty;
        processStartEstimateAssignmentCount = 0;
    }

    private static string ResolveProcessStartHistoricalEstimateStatus(ProcessHistoricalRunCostEstimate historicalCostEstimate)
    {
        if (historicalCostEstimate.HasActualCost)
        {
            return "Historical run costs are now included in the estimate.";
        }

        return historicalCostEstimate.CompletedRunCount > 0
            ? "Provider price estimate is ready; historical runs had no resolved usage cost."
            : "Provider price estimate is ready; no historical completed runs were found.";
    }

    private static string AppendProcessStartEstimateStatusMessage(string currentMessage, string addition)
    {
        if (string.IsNullOrWhiteSpace(addition))
        {
            return currentMessage;
        }

        if (string.IsNullOrWhiteSpace(currentMessage))
        {
            return addition;
        }

        return currentMessage.Contains(addition, StringComparison.Ordinal)
            ? currentMessage
            : $"{currentMessage} {addition}";
    }

    private static decimal ResolveCandidateScore(ProjectStructureProcessStartCandidateState candidate)
    {
        if (candidate.MatchScore != 0)
        {
            return candidate.MatchScore;
        }

        var token = candidate.ScoreLabel.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        if (decimal.TryParse(token, NumberStyles.Number, CultureInfo.CurrentCulture, out var localizedScore))
        {
            return localizedScore * 10m;
        }

        if (decimal.TryParse(token, NumberStyles.Number, CultureInfo.InvariantCulture, out var invariantScore))
        {
            return invariantScore * 10m;
        }

        return 0m;
    }

    private static string FormatProcessStartCandidateScore(int matchScore)
    {
        var displayScore = Math.Clamp(Math.Max(0, matchScore) / 10m, 0m, 10m);
        return $"{displayScore:0.0} score";
    }

    private async Task TryLinkStartedProcessRunAsync(string sourceNodeId, ProcessRunId runId)
    {
        if (string.IsNullOrWhiteSpace(sourceNodeId))
        {
            return;
        }

        try
        {
            await ProjectWorkbenchService.LinkObjectsAsync(
                ProjectId,
                sourceNodeId,
                ProjectStructureProcessNodeKeys.BuildProcessRunNodeKey(runId.Value),
                ProjectObjectLinkKind.Uses);
        }
        catch (InvalidOperationException exception)
        {
            Logger.LogDebug(
                exception,
                "Project structure process run link could not be created. ProjectId={ProjectId} SourceNodeId={SourceNodeId} RunId={RunId}",
                ProjectId,
                sourceNodeId,
                runId.Value);
        }
    }

    private Task SetProcessActionExceptionAsync(Exception exception, string action)
    {
        var message = exception.GetBaseException().Message;
        if (string.IsNullOrWhiteSpace(message))
        {
            message = $"The process action failed unexpectedly while {action}.";
        }
        else
        {
            message = $"The process action failed while {action}: {message}";
        }

        if (processStartDialog is not null)
        {
            processStartDialog = processStartDialog with
            {
                IsBusy = false,
                ConfirmHrManagerMatch = false,
                Error = message
            };
        }

        Logger.LogWarning(
            exception,
            "Project structure process action failed while {Action}. ProjectId={ProjectId} ProcessDefinitionId={ProcessDefinitionId} LaunchPlanId={LaunchPlanId} Stage={Stage}",
            action,
            ProjectId,
            processStartDialog?.ProcessDefinitionId,
            processStartDialog?.LaunchPlanId,
            processStartDialog?.Stage);
        workflowFeedback = message;
        workflowFeedbackTone = "warn";
        return InvokeAsync(StateHasChanged);
    }

    private ProjectStructureNode? ResolveProcessStartTargetNode(ProjectStructureNode node)
    {
        if (surface is null)
        {
            return ResolveNode(node.ParentId);
        }

        var projectRootNodeId = ProjectWorkbenchGraphConventions.BuildProjectRootNodeKey(ProjectId);
        var authoredTargetLink = surface.Links
            .Where(link =>
                link.IsUserAuthored &&
                string.Equals(link.TargetId, node.Id, StringComparison.Ordinal))
            .OrderBy(link => link.Kind == ProjectObjectLinkKind.Uses ? 0 : 1)
            .ThenBy(link => string.Equals(link.SourceId, projectRootNodeId, StringComparison.Ordinal) ? 1 : 0)
            .Select(link => ResolveNode(link.SourceId))
            .FirstOrDefault(candidate => candidate is not null);
        if (authoredTargetLink is not null)
        {
            return authoredTargetLink;
        }

        return ResolveNode(node.ParentId);
    }

    private static ProjectStructureProcessLinkOption MapProcessLinkOption(ProcessDefinitionCatalogItemProjection definition)
    {
        var definitionId = ProcessDefinitionCatalogProjectionService.CreateDefinitionId(definition.Key).Value;
        return new ProjectStructureProcessLinkOption(
            definitionId,
            definition.Name,
            definition.ScopeKind == ProcessDefinitionCatalogScopeKind.Project ? "Project" : "Global",
            definition.Status.ToString(),
            definition.Status is ProcessDefinitionCatalogItemStatus.TemplateDefault or ProcessDefinitionCatalogItemStatus.Published);
    }

    private static Guid? ResolveProcessDefinitionId(ProjectStructureNode node)
    {
        if (node.ArtifactId.HasValue)
        {
            return node.ArtifactId.Value;
        }

        return ProjectStructureProcessNodeKeys.TryParseProcessDefinitionNodeKey(node.Id, out var definitionId)
            ? definitionId
            : null;
    }

    private static string ResolveOutputRoot(ProjectStructureNode? node)
    {
        if (node is null)
        {
            return string.Empty;
        }

        var metadataOutputRoot = TryReadOutputRootFromMetadata(node.MetadataJson);
        if (!string.IsNullOrWhiteSpace(metadataOutputRoot))
        {
            return metadataOutputRoot;
        }

        var text = string.Join(Environment.NewLine, node.Title, node.Subtitle, node.Notes);
        var match = Regex.Match(text, @"[A-Za-z]:\\[^\r\n""<>|]+");
        return match.Success
            ? match.Value.Trim().TrimEnd('.', ',', ';', ')', ']')
            : string.Empty;
    }

    private static void ApplyProductRootLaunchVariables(
        IDictionary<string, string> variables,
        string outputRoot)
    {
        var normalizedOutputRoot = outputRoot.Trim();
        variables["OutputRoot"] = normalizedOutputRoot;
        variables["ProductRoot"] = normalizedOutputRoot;
    }

    private static string ResolveOutputRoot(ProjectStructureSurface currentSurface, ProjectStructureNode targetNode)
    {
        var direct = ResolveOutputRoot(targetNode);
        if (!string.IsNullOrWhiteSpace(direct))
        {
            return direct;
        }

        foreach (var (node, _) in EnumerateProjectStructureContextNodes(currentSurface, targetNode))
        {
            var candidate = ResolveOutputRoot(node);
            if (!string.IsNullOrWhiteSpace(candidate))
            {
                return candidate;
            }
        }

        return string.Empty;
    }

    private static string TryReadOutputRootFromMetadata(string? metadataJson)
    {
        if (string.IsNullOrWhiteSpace(metadataJson))
        {
            return string.Empty;
        }

        try
        {
            var metadata = ProjectObjectMetadataSerializer.Parse(metadataJson);
            var typedOutputRoot = FirstNonEmpty(
                metadata.ProjectBlock?.OutputRoot,
                metadata.ProjectBlock?.ProductRoot,
                metadata.ProjectBlock?.TargetRoot,
                metadata.ProjectBlock?.RepositoryRoot,
                metadata.ProjectBlock?.WorkspaceRoot);
            if (!string.IsNullOrWhiteSpace(typedOutputRoot))
            {
                return typedOutputRoot;
            }

            using var document = JsonDocument.Parse(metadataJson);
            return TryReadOutputRootFromElement(document.RootElement);
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException)
        {
        }

        return string.Empty;
    }

    private static string TryReadOutputRootFromElement(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var key in OutputRootMetadataKeys)
            {
                if (element.TryGetProperty(key, out var property) &&
                    property.ValueKind == JsonValueKind.String)
                {
                    var value = property.GetString();
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        return value.Trim();
                    }
                }
            }

            foreach (var property in element.EnumerateObject())
            {
                var value = TryReadOutputRootFromElement(property.Value);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }
        }

        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                var value = TryReadOutputRootFromElement(item);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }
        }

        return string.Empty;
    }

    private static string FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;

    private static string NormalizeProcessContextText(string value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = RedactNonCitableProcessContextPaths(Regex.Replace(value, @"\s+", " ").Trim());
        return normalized.Length <= maxLength
            ? normalized
            : normalized[..maxLength].TrimEnd() + "...";
    }

    private static string RedactNonCitableProcessContextPaths(string value)
    {
        var withoutNativePaths = Regex.Replace(
            value,
            @"(?:file://[^\s""'<>]+|[A-Za-z]:\\[^\s""'<>|]+|\\\\[^\s""'<>|]+)",
            "[storage-path]");
        return Regex.Replace(
            withoutNativePaths,
            @"\b(?:artifacts/scopes|project-media|managed-files|tool-runs)[^\s""'<>]*",
            "[storage-path]",
            RegexOptions.IgnoreCase);
    }

    private static string ResolveStepDisplayName(ProcessLaunchStepView step)
        => string.IsNullOrWhiteSpace(ResolveStepRoleLabel(step))
            ? step.Title
            : $"{step.Title} ({ResolveStepRoleLabel(step)})";

    private static AgentProcessRoleReadinessResult EvaluateAgentForStep(
        ProjectStructureProcessStartAgentMetadata metadata,
        ProcessLaunchStepView step)
    {
        return AgentProcessReadinessEvaluator.Evaluate(
            metadata.Agent,
            new AgentProcessRoleReadinessRequest(
                step.StepKey,
                step.Title,
                step.RoleKey,
                step.RoleResourceKey,
                ResolveStepRoleLabel(step),
                step.AllowedOperations,
                step.OperationTargetScope,
                step.RequiredRuntimeToolNames));
    }

    private static string ResolveStepRoleLabel(ProcessLaunchStepView step)
        => FirstNonEmpty(step.RoleDisplayName, step.RoleResourceKey, step.RoleKey);

    private sealed record ProjectStructureProcessStartAgentMetadata(
        AgentDefinition Agent,
        Guid AgentId,
        string DisplayName,
        string ProviderName,
        string Model,
        string RoleTitle,
        string Summary,
        string StatusLabel,
        string WorkloadLabel,
        string AvatarImageUrl,
        IReadOnlyList<string> ToolNames,
        IReadOnlyList<string> SkillNames,
        IReadOnlyList<string> Tags,
        ProviderProfile? ProviderProfile)
    {
        public static ProjectStructureProcessStartAgentMetadata FromAgent(
            AgentDefinition agent,
            ProviderProfile? provider)
        {
            var toolNames = agent.Capabilities
                .Where(item => item.Kind is not CapabilityKind.Skill)
                .Select(ResolveCapabilityDisplayName)
                .Concat(AgentProcessReadinessEvaluator.ResolveWorkspaceToolNames(agent))
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
                .ToList();
            var skillNames = agent.Capabilities
                .Where(item => item.Kind == CapabilityKind.Skill)
                .Select(ResolveCapabilityDisplayName)
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return new ProjectStructureProcessStartAgentMetadata(
                agent,
                agent.Id,
                agent.Name,
                provider?.Name ?? string.Empty,
                agent.Model,
                agent.RoleTitle,
                agent.Summary,
                agent.Status.ToString(),
                agent.Workload.ToString(),
                agent.AvatarImageUrl ?? string.Empty,
                toolNames,
                skillNames,
                agent.Tags,
                provider);
        }

        private static string ResolveCapabilityDisplayName(AgentCapabilityAssignment capability)
            => !string.IsNullOrWhiteSpace(capability.CapabilityKey)
                ? capability.CapabilityKey
                : capability.Kind.ToString();
    }
}
