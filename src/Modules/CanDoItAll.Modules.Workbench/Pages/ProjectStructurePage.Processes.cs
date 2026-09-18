using System.Collections.Frozen;
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

    [Inject]
    private ProcessDefinitionCatalogProjectionService ProcessDefinitionCatalogService { get; set; } = default!;

    [Inject]
    private ProcessLaunchApplicationService ProcessLaunchService { get; set; } = default!;

    [Inject]
    private IProcessLaunchOperatorAuthoritySource ProcessLaunchAuthorities { get; set; } = default!;

    [Inject]
    private IProcessPreparedLaunchStore ProcessLaunchPreparations { get; set; } = default!;

    [Inject]
    private ProjectProcessLaunchTargetQuery ProcessLaunchTargets { get; set; } = default!;

    [Inject]
    private ProjectProcessLaunchDeliveryService ProcessLaunchDelivery { get; set; } = default!;

    [Inject]
    private IAgentReferenceDataProvider AgentReferenceDataProvider { get; set; } = default!;

    [Inject]
    private IProcessHistoricalRunCostReader ProcessHistoricalRunCostReader { get; set; } = default!;

    [Inject]
    private IProcessLaunchVariablePreparer ProcessLaunchVariablePreparationService { get; set; } = default!;

    [Inject]
    private ProjectStructureTaskResourceAttachmentService TaskResourceAttachmentService { get; set; } = default!;

    private ProjectStructureProcessLinkDialogState? processLinkDialog;
    private Guid processLinkRequestId;
    private AgentChatNavigationIdentity? processDialogNavigationIdentity;
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

    private async Task OpenLinkProcessDialogAsync(ProjectStructureNode node) {
        var openedSurface = surface ?? throw new InvalidOperationException("Reload the project before opening this editor.");
        var mutationOwner = CreateProjectStructureUiAgentContext() with { ExpectedProjectAdmission = openedSurface.ExpectedProjectAdmission };

        CloseQuickActionDialog();
        CloseProcessLinkDialog();
        var projectId = ProjectId;
        var navigation = AgentChatNavigationFence;
        var requestId = Guid.NewGuid();
        processLinkRequestId = requestId;
        try {
            var catalog = await ProcessDefinitionCatalogService.GetCatalogAsync(
                ProcessWorkspaceShellScope.ForProject(projectId),
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
            if (!IsCurrentProcessLinkRequest(requestId, projectId, navigation)) {
                return;
            }
            processLinkDialog = new ProjectStructureProcessLinkDialogState(
                node.Id,
                node.Title,
                options,
                options.FirstOrDefault()?.DefinitionId,
                options.Count == 0 ? "No process definitions are available in the process template catalog." : string.Empty) {
                ProjectId = projectId,
                OpenedSurface = openedSurface,
                MutationOwner = mutationOwner,
                DialogId = requestId,
                NavigationIdentity = navigation
            };
        } catch (Exception exception) when (exception is not OperationCanceledException) {
            Logger.LogWarning(exception, "Project structure process definition catalog load failed. ProjectId={ProjectId}", projectId);
            if (!IsCurrentProcessLinkRequest(requestId, projectId, navigation)) {
                return;
            }
            processLinkDialog = new ProjectStructureProcessLinkDialogState(
                node.Id,
                node.Title,
                [],
                null,
                $"Process definitions could not be loaded: {exception.Message}") {
                ProjectId = projectId,
                OpenedSurface = openedSurface,
                MutationOwner = mutationOwner,
                DialogId = requestId,
                NavigationIdentity = navigation
            };
        }

        await InvokeAsync(StateHasChanged);
    }

    private void CloseProcessLinkDialog() {
        processLinkDialog = null;
        processLinkRequestId = Guid.Empty;
    }

    private bool IsCurrentProcessLinkRequest(Guid requestId, Guid projectId, AgentChatNavigationIdentity navigation)
        => processLinkRequestId == requestId && ProjectId == projectId && AgentChatNavigationFence == navigation;

    private bool IsCurrentProcessLink(ProjectStructureProcessLinkDialogState dialog)
        => processLinkDialog?.DialogId == dialog.DialogId &&
           IsCurrentProcessLinkRequest(dialog.DialogId, dialog.ProjectId, dialog.NavigationIdentity);

    private void HandleProcessLinkSelectionChanged(Guid definitionId)
    {
        if (processLinkDialog is not { IsBusy: false })
        {
            return;
        }

        processLinkDialog = processLinkDialog with
        {
            SelectedDefinitionId = definitionId,
            Error = string.Empty
        };
    }

    private async Task ExecuteProcessLinkAsync() {
        if (processLinkDialog is not { IsBusy: false } dialog || !IsCurrentProcessLink(dialog)) {
            return;
        }
        if (!dialog.SelectedDefinitionId.HasValue)
        {
            processLinkDialog = dialog with { Error = "Select a process before continuing." };
            await InvokeAsync(StateHasChanged);
            return;
        }

        var selectedOption = dialog.Options
            .FirstOrDefault(option => option.DefinitionId == dialog.SelectedDefinitionId.Value);
        if (selectedOption is null)
        {
            processLinkDialog = dialog with { Error = "The selected process is no longer available." };
            await InvokeAsync(StateHasChanged);
            return;
        }

        try {
            var openedSurface = dialog.OpenedSurface ?? throw new InvalidOperationException("Reopen this editor to capture its project.");
            var mutationOwner = dialog.MutationOwner ?? throw new InvalidOperationException("Reopen this editor to capture its project.");
            var sourceNode = openedSurface.Nodes.FirstOrDefault(node => node.Id == dialog.SourceNodeId)
                ?? throw new InvalidOperationException("The selected project-structure node is no longer available.");
            processLinkDialog = dialog with { IsBusy = true };
            if (IsCanonicalTaskNode(sourceNode)) {
                var execution = ProjectStructureTaskEditStatePolicy.Read(sourceNode).Execution;
                await TaskResourceAttachmentService.AttachAsync(
                    dialog.ProjectId,
                    sourceNode.Id,
                    new ProjectStructureTaskResourceAttachRequest(
                        new ProjectStructureTaskResourceSelection(
                            ProjectStructureTaskResourceKind.Process,
                            selectedOption.DefinitionId),
                        execution) { ExpectedProjectAdmission = mutationOwner.ExpectedProjectAdmission },
                    mutationOwner);
            } else {
                await ProjectWorkbenchService.LinkObjectsAsync(
                    dialog.ProjectId,
                    sourceNode.Id,
                    ProjectStructureProcessNodeKeys.BuildProcessDefinitionNodeKey(selectedOption.DefinitionId),
                    ProjectObjectLinkKind.Uses, expectedProjectAdmission: mutationOwner.ExpectedProjectAdmission, processMutationAdmission: mutationOwner.ProcessMutationAdmission);
            }
        } catch (Exception exception) when (exception is not OperationCanceledException) {
            var message = exception is ProjectStructureAgentException or InvalidOperationException or ArgumentException
                ? exception.GetBaseException().Message
                : "The process could not be linked. Check the application logs and try again.";
            Logger.LogWarning(
                exception,
                "Project structure process definition link failed. ProjectId={ProjectId} SourceNodeId={SourceNodeId} ProcessDefinitionId={ProcessDefinitionId}",
                dialog.ProjectId,
                dialog.SourceNodeId,
                selectedOption.DefinitionId);
            if (!IsCurrentProcessLink(dialog)) {
                return;
            }
            processLinkDialog = dialog with { Error = message, IsBusy = false };
            await InvokeAsync(StateHasChanged);
            return;
        }
        if (!IsCurrentProcessLink(dialog)) {
            return;
        }
        var sourceNodeId = dialog.SourceNodeId;
        var processNodeId = ProjectStructureProcessNodeKeys.BuildProcessDefinitionNodeKey(selectedOption.DefinitionId);
        workflowFeedback = $"{selectedOption.DisplayName} was linked to {dialog.SourceNodeTitle}.";
        workflowFeedbackTone = "mint";
        await ReloadSurfaceAsync(sourceNodeId);
        if (!IsCurrentProcessLink(dialog)) {
            return;
        }
        CloseProcessLinkDialog();
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
        bool estimateOnly) {
        CloseQuickActionDialog();
        ResetProcessStartEstimateState();
        var definitionKey = ResolveProcessDefinitionKey(processDefinitionId);
        var dialog = new ProjectStructureProcessStartDialogState(
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
            string.Empty) {
            EstimateOnlyMode = estimateOnly,
            NavigationIdentity = AgentChatNavigationFence
        };
        processStartDialog = dialog;
        try {
            var caller = await ProcessLaunchAuthorities.CaptureUserInterfaceAsync(null);
            var storageKey = $"candoitall.process-launch.structure:{caller.DatabaseProfileId:D}:{dialog.ProjectId:D}:{dialog.TargetNodeId}:{dialog.ProcessDefinitionId:D}";
            if (await TryRestoreProcessStartAsync(dialog, caller, storageKey)) {
                return;
            }
            var authority = await ProcessLaunchAuthorities.CaptureUserInterfaceAsync(dialog.ProjectId);
            if (!IsCurrentProcessStart(dialog)) {
                return;
            }
            var launchSurface = surface;
            if (launchSurface?.ProjectId != dialog.ProjectId) {
                throw new InvalidOperationException("Wait for the selected project structure to finish loading before starting a process.");
            }

            var processNode = launchSurface.Nodes.FirstOrDefault(node =>
                string.Equals(node.Id, processNodeId, StringComparison.Ordinal));
            var launchTarget = targetNode ?? processNode;
            var variables = CreateProcessLaunchVariables(dialog, launchSurface, processNode, launchTarget);
            var linkStartedRun = launchTarget is null || !IsCanonicalTaskNode(launchTarget);
            var linkTarget = linkStartedRun
                ? await ProcessLaunchTargets.CaptureAsync(dialog.ProjectId, dialog.TargetNodeId) : null;
            if (!IsCurrentProcessStart(dialog)) {
                return;
            }
            processStartDialog = dialog with {
                LaunchAuthority = authority,
                LaunchLinkTarget = linkTarget,
                IntentStorageKey = storageKey,
                LaunchVariables = variables.ToFrozenDictionary(StringComparer.Ordinal),
                SourceSnapshot = launchTarget is null ? null : ProjectStructureProcessLaunchSourceSnapshotMapper.Create(
                    launchSurface, launchTarget, dialog.DefinitionKey, isSubprocess: false,
                    variables.GetValueOrDefault(ProjectStructureProcessLaunchContext.ContextSummaryVariableName)).Source,
                LinkStartedRun = linkStartedRun
            };
        } catch (Exception exception) when (exception is not OperationCanceledException) {
            await SetProcessActionExceptionAsync(exception, "preparing the process launch context", dialog);
            return;
        }

        await InvokeAsync(StateHasChanged);
        if (estimateOnly && IsCurrentProcessStart(dialog)) {
            await ExecuteProcessStartAsync();
        }
    }

    private void CloseProcessStartDialog()
    {
        ResetProcessStartEstimateState();
        processStartDialog = null;
    }

    private async Task ReviewAndStartProcessAsync() {
        if (processStartDialog is not { IsBusy: false } dialog) {
            return;
        }
        processStartDialog = dialog with { AssignmentsReviewed = true, Error = string.Empty };
        await ExecuteProcessStartAsync();
    }

    private async Task ExecuteProcessStartAsync() {
        if (processStartDialog is not { IsBusy: false } dialog || !IsCurrentProcessStart(dialog)) {
            return;
        }

        if (dialog.ProcessDefinitionId == Guid.Empty) {
            processStartDialog = dialog with { Error = "The selected process definition id is missing." };
            await InvokeAsync(StateHasChanged);
            return;
        }

        try {
            if (dialog.Stage == ProjectStructureProcessStartStage.Confirm) {
                await PreviewProcessStartAsync(
                    dialog,
                    "Launch plan prepared. Review the resolved assignments before starting.",
                    runReadiness: false);
                return;
            }

            if (dialog.PreparedRequest?.PreparedAdmissionId is null) {
                await PrepareReviewedProcessStartAsync(dialog);
                return;
            }

            if (!dialog.AssignmentsReviewed) {
                processStartDialog = dialog with {
                    IsBusy = false,
                    Error = "Review the proposed role assignments and confirm them before starting the process."
                };
                await InvokeAsync(StateHasChanged);
                return;
            }

            if (dialog.RequiredGapCount > 0 && !dialog.IsAccepted) {
                processStartDialog = dialog with {
                    IsBusy = false,
                    Error = "Resolve every required role before starting the process."
                };
                await InvokeAsync(StateHasChanged);
                return;
            }

            var launchRequest = dialog.PreparedRequest with { Execute = true };
            processStartDialog = dialog with {
                IsBusy = true,
                Error = string.Empty,
                ConfirmHrManagerMatch = false,
                StatusMessage = "Starting the reviewed process run."
            };
            await InvokeAsync(StateHasChanged);

            var result = await ProcessLaunchService.LaunchAsync(launchRequest);
            if (result.RunId is null) {
                if (IsCurrentProcessStart(dialog)) {
                    processStartDialog = dialog with {
                        IsBusy = false,
                        Error = result.Warnings.Count == 0
                            ? $"Process launch returned {result.Stage}."
                            : string.Join(" ", result.Warnings)
                    };
                    await InvokeAsync(StateHasChanged);
                }
                return;
            }

            var deliveryMessage = await ObserveProcessLinkDeliveryAsync(dialog, result);
            if (!IsCurrentProcessStart(dialog)) {
                return;
            }

            await InvokeAsync(() => {
                if (!IsCurrentProcessStart(dialog)) {
                    return;
                }

                CloseProcessStartDialog();
                workflowFeedback = $"Process {result.RunId.Value.Value:D} accepted for {dialog.TargetNodeTitle}. {string.Join(" ", result.Warnings)} {deliveryMessage}".Trim();
                workflowFeedbackTone = result.Warnings.Count == 0 && string.IsNullOrEmpty(deliveryMessage) ? "mint" : "warn";
                StateHasChanged();
                Navigation.NavigateTo(AppendProcessStartedQuery(result.Route));
            });
        } catch (Exception exception) when (exception is not OperationCanceledException) {
            await SetProcessActionExceptionAsync(exception, "starting the process", dialog);
        }
    }

    private async Task PreviewProcessStartAsync(
        ProjectStructureProcessStartDialogState dialog,
        string statusMessage,
        bool runReadiness) {
        if (!IsCurrentProcessStart(dialog)) {
            return;
        }

        dialog = dialog with { PreparedRequest = null, LaunchObservation = null, LaunchIntentId = new(Guid.NewGuid()), AssignmentsReviewed = false };
        var launchRequest = CreateProcessLaunchRequest(dialog, execute: false, runReadiness);
        processStartDialog = dialog with {
            IsBusy = true,
            Error = string.Empty,
            ConfirmHrManagerMatch = false,
            StatusMessage = statusMessage
        };
        CancelProcessStartHistoricalEstimateRefresh();
        await InvokeAsync(StateHasChanged);

        using var previewTimeout = new CancellationTokenSource(ProcessStartPreviewTimeout);
        try {
            var agentMetadata = await LoadProcessStartAgentMetadataAsync(previewTimeout.Token);
            var preview = await ProcessLaunchService.PreviewAsync(launchRequest, previewTimeout.Token);
            if (!IsCurrentProcessStart(dialog)) {
                return;
            }

            processStartAgentMetadataById = agentMetadata;
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
        } catch (OperationCanceledException exception) when (previewTimeout.IsCancellationRequested) {
            Logger.LogWarning(
                exception,
                "Project structure process launch preview timed out. ProjectId={ProjectId} ProcessDefinitionId={ProcessDefinitionId} NodeId={NodeId} RunReadiness={RunReadiness}",
                dialog.ProjectId,
                dialog.ProcessDefinitionId,
                dialog.NodeId,
                runReadiness);
            if (!IsCurrentProcessStart(dialog)) {
                return;
            }

            processStartDialog = dialog with {
                IsBusy = false,
                ConfirmHrManagerMatch = false,
                Error = $"Process launch preview did not finish within {ProcessStartPreviewTimeout.TotalSeconds:N0} seconds. Try again after checking the agent/provider catalog."
            };
            await InvokeAsync(StateHasChanged);
        } catch (Exception exception) when (exception is not OperationCanceledException) {
            await SetProcessActionExceptionAsync(exception, "preparing the process launch preview", dialog);
        }
    }

    private bool IsCurrentProcessStart(ProjectStructureProcessStartDialogState dialog)
        => processStartDialog?.DialogId == dialog.DialogId &&
           ProjectId == dialog.ProjectId &&
           AgentChatNavigationFence == dialog.NavigationIdentity;

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
        if (processStartDialog is not { IsBusy: false, IsAccepted: false })
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
            PreparedRequest = null,
            LaunchObservation = null,
            LaunchIntentId = new(Guid.NewGuid()),
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

    private async Task HandleProcessStartAssignmentsReviewedChanged(ChangeEventArgs args) {
        if (processStartDialog is not { IsBusy: false } dialog) {
            return;
        }
        var isChecked = args.Value switch {
            bool value => value,
            string value when bool.TryParse(value, out var parsed) => parsed,
            _ => false
        };
        if (isChecked && dialog.PreparedRequest?.PreparedAdmissionId is null) {
            await PrepareReviewedProcessStartAsync(dialog);
            return;
        }
        processStartDialog = dialog with {
            AssignmentsReviewed = isChecked,
            Error = string.Empty,
            StatusMessage = isChecked ? "The saved launch plan is confirmed and ready to start."
                : "Review the saved assignments before starting the process."
        };
        await InvokeAsync(StateHasChanged);
    }

    private Task RequestHrManagerMatchAsync()
    {
        if (processStartDialog is not { IsBusy: false, IsAccepted: false })
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

    private async Task ExecuteHrManagerMatchAsync() {
        if (processStartDialog is not { IsBusy: false, IsAccepted: false } dialog || !IsCurrentProcessStart(dialog)) {
            return;
        }

        try {
            await PreviewProcessStartAsync(
                dialog,
                $"{ProjectStructureHrManagerName} refreshed the staffing suggestions from the active agent directory.",
                runReadiness: true);
        } catch (Exception exception) when (exception is not OperationCanceledException) {
            await SetProcessActionExceptionAsync(exception, "requesting HR manager staffing", dialog);
        }
    }

    private ProcessLaunchRequest CreateProcessLaunchRequest(
        ProjectStructureProcessStartDialogState dialog,
        bool execute,
        bool runReadiness = true) {
        var sourceVariables = dialog.LaunchVariables
            ?? throw new InvalidOperationException("The process launch context could not be prepared. Close the dialog and try again.");
        var variables = new Dictionary<string, string>(sourceVariables, StringComparer.Ordinal);
        if (dialog.SourceSnapshot is { } snapshot && !string.IsNullOrWhiteSpace(dialog.DefinitionKey)) {
            ProcessLaunchVariablePreparationService.Enrich(
                new ProcessLaunchPreparationContext(dialog.DefinitionKey, IsSubprocess: false, snapshot), variables);
        }
        return new ProcessLaunchRequest(
            DefinitionKey: string.IsNullOrWhiteSpace(dialog.DefinitionKey) ? null : dialog.DefinitionKey,
            new ProcessDefinitionId(dialog.ProcessDefinitionId),
            LiveRunProfileKey: null,
            dialog.ProjectId,
            ProjectNodeId: dialog.TargetNodeId,
            RequestedBy: "project-structure",
            variables,
            RunReadiness: runReadiness,
            Execute: execute) {
            ExecutorOverrides = CreateExecutorOverrides(dialog)
        };
    }

    private IReadOnlyDictionary<string, string> CreateProcessLaunchVariables(
        ProjectStructureProcessStartDialogState dialog,
        ProjectStructureSurface? sourceSurface,
        ProjectStructureNode? processNode,
        ProjectStructureNode? targetNode) {
        var variables = new Dictionary<string, string>(StringComparer.Ordinal) {
            ["LaunchSource"] = "project-structure-ui",
            ["ProjectId"] = dialog.ProjectId.ToString("D"),
            ["ProjectName"] = sourceSurface?.ProjectName ?? string.Empty,
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

        var launchContext = ProjectStructureProcessLaunchContextBuilder.Build(sourceSurface, targetNode);
        launchContext.ApplyContextSummaryTo(variables);
        launchContext.ApplyOutputRootAliasesTo(variables);

        return variables;
    }

    private string ResolveProcessDefinitionKey(Guid processDefinitionId)
    {
        return processDefinitionId == Guid.Empty
            ? string.Empty
            : ProcessDefinitionCatalogService.ResolveDefinitionKey(new ProcessDefinitionId(processDefinitionId));
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

    private async Task<IReadOnlyDictionary<Guid, ProjectStructureProcessStartAgentMetadata>>
        LoadProcessStartAgentMetadataAsync(CancellationToken cancellationToken) {
        try
        {
            var referenceData = await AgentReferenceDataProvider.GetAsync(
                AgentReferenceDataRequest.AgentsAndProviders(activeAgentsOnly: true),
                cancellationToken);
            var providerById = referenceData.ProviderById;
            return referenceData.Agents
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
            return referenceData.Agents.ToDictionary(
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
               IsCurrentProcessStart(dialog) &&
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
        processStartAgentMetadataById = new Dictionary<Guid, ProjectStructureProcessStartAgentMetadata>();
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

    private async Task<string> ObserveProcessLinkDeliveryAsync(ProjectStructureProcessStartDialogState dialog, ProcessLaunchResult result) {
        if (!dialog.LinkStartedRun) {
            return string.Empty;
        }
        if (result.Observation is not { } observation) {
            return "The accepted process has no observable link admission; its Structure link requires reconciliation.";
        }
        try {
            var receipt = await ProcessLaunchDelivery.DeliverAsync(observation.AdmissionId);
            return receipt.State switch {
                ProcessLaunchLinkDeliveryState.Delivered => string.Empty,
                ProcessLaunchLinkDeliveryState.Removed => "The original Structure link was removed and has been preserved as removed.",
                ProcessLaunchLinkDeliveryState.Conflict => $"The process was accepted, but its Structure link requires reconciliation ({receipt.ConflictReason}).",
                _ => "The process was accepted; Structure link delivery is still pending."
            };
        } catch (Exception exception) {
            Logger.LogWarning(exception, "Accepted process Structure link delivery could not be observed. AdmissionId={AdmissionId} RunId={RunId}",
                observation.AdmissionId.Value, result.RunId?.Value);
            return "The process was accepted; Structure link delivery could not be confirmed and can be retried from its admission.";
        }
    }

    private Task SetProcessActionExceptionAsync(
        Exception exception,
        string action,
        ProjectStructureProcessStartDialogState dialog) {
        var message = exception.GetBaseException().Message;
        message = string.IsNullOrWhiteSpace(message)
            ? $"The process action failed unexpectedly while {action}."
            : $"The process action failed while {action}: {message}";
        Logger.LogWarning(
            exception,
            "Project structure process action failed while {Action}. ProjectId={ProjectId} ProcessDefinitionId={ProcessDefinitionId} LaunchPlanId={LaunchPlanId} Stage={Stage}",
            action,
            dialog.ProjectId,
            dialog.ProcessDefinitionId,
            dialog.LaunchPlanId,
            dialog.Stage);
        if (!IsCurrentProcessStart(dialog)) {
            return Task.CompletedTask;
        }

        return InvokeAsync(() => {
            if (!IsCurrentProcessStart(dialog)) {
                return;
            }

            processStartDialog = processStartDialog! with {
                IsBusy = false,
                ConfirmHrManagerMatch = false,
                Error = message
            };
            workflowFeedback = message;
            workflowFeedbackTone = "warn";
            StateHasChanged();
        });
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

    private static string FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;

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
