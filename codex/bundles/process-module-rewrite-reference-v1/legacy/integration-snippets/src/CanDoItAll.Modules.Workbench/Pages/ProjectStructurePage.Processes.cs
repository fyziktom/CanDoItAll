using CanDoItAll.AgentFramework.Components;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Modules.Processes;
using CanDoItAll.SharedKernel;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.Workbench.Pages;

public partial class ProjectStructurePage
{
    private const string ProjectStructureHrManagerName = "HR Staffing Manager";
    private static readonly TimeSpan ProcessStartLaunchPlanCreateTimeout = TimeSpan.FromSeconds(120);
    private static readonly TimeSpan ProcessStartLaunchPlanCreateRecoveryTimeout = TimeSpan.FromSeconds(90);
    private static readonly TimeSpan ProcessStartHrManagerMatchTimeout = TimeSpan.FromSeconds(45);

    [Inject]
    private ProcessesService ProcessesService { get; set; } = default!;

    [Inject]
    private IAgentFrameworkWorkspaceService AgentWorkspaceService { get; set; } = default!;

    [Inject]
    private DialogService DialogService { get; set; } = default!;

    private ProjectStructureProcessLinkDialogState? processLinkDialog;
    private ProjectStructureProcessStartDialogState? processStartDialog;
    private IReadOnlyDictionary<Guid, ProjectStructureProcessStartAgentMetadata> processStartAgentMetadataById =
        new Dictionary<Guid, ProjectStructureProcessStartAgentMetadata>();

    private async Task OpenAddProcessDialogAsync(ProjectStructureNode node)
    {
        CloseQuickActionDialog();

        var definitions = await ProcessesService.ListDefinitionsAsync(ProjectId);
        var options = definitions
            .OrderBy(item => item.ProjectId.HasValue && item.ProjectId.Value == ProjectId ? 0 : item.ProjectId.HasValue ? 1 : 2)
            .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Id)
            .Select(MapProcessLinkOption)
            .ToList();

        processLinkDialog = new ProjectStructureProcessLinkDialogState(
            node.Id,
            node.Title,
            options,
            options.FirstOrDefault()?.DefinitionId,
            string.Empty);

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
            return;
        }

        var selectedOption = processLinkDialog.Options
            .FirstOrDefault(option => option.DefinitionId == processLinkDialog.SelectedDefinitionId.Value);
        if (selectedOption is null)
        {
            processLinkDialog = processLinkDialog with { Error = "The selected process is no longer available." };
            return;
        }

        try
        {
            await ProjectWorkbenchService.LinkObjectsAsync(
                ProjectId,
                processLinkDialog.SourceNodeId,
                BuildProcessDefinitionNodeKey(selectedOption.DefinitionId),
                ProjectObjectLinkKind.Uses);
        }
        catch (InvalidOperationException exception)
        {
            processLinkDialog = processLinkDialog with { Error = exception.Message };
            return;
        }

        var sourceNodeTitle = processLinkDialog.SourceNodeTitle;
        var sourceNodeId = processLinkDialog.SourceNodeId;
        processLinkDialog = null;
        workflowFeedback = $"{selectedOption.DisplayName} was linked to {sourceNodeTitle}.";
        workflowFeedbackTone = "mint";
        await ReloadSurfaceAsync(sourceNodeId);
        await InvokeAsync(StateHasChanged);
    }

    private async Task OpenStartProcessDialogAsync(ProjectStructureNode node)
    {
        await OpenProcessDialogAsync(node, estimateOnly: false);
    }

    private async Task OpenEstimateProcessDialogAsync(ProjectStructureNode node)
    {
        await OpenProcessDialogAsync(node, estimateOnly: true);
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
        var startContext = CreateProcessStartContext(node);
        processStartDialog = new ProjectStructureProcessStartDialogState(
            ProjectId,
            processDefinitionId.Value,
            node.Id,
            node.Title,
            startContext.ParentNodeId,
            startContext.ParentNodeTitle,
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

        try
        {
            var dialog = processStartDialog;
            var node = ResolveNode(dialog.NodeId);
            if (node is null)
            {
                processStartDialog = dialog with { Error = "The selected process node could not be found. Reload the project structure and try again." };
                return;
            }

            var startContext = CreateProcessStartContext(node);
            if (dialog.Stage == ProjectStructureProcessStartStage.Staffing &&
                DateTimeOffset.UtcNow - dialog.StageActivatedAtUtc < TimeSpan.FromMilliseconds(400))
            {
                return;
            }

            processStartDialog = dialog with
            {
                IsBusy = true,
                Error = string.Empty,
                ConfirmHrManagerMatch = false
            };
            await InvokeAsync(StateHasChanged);

            if (dialog.Stage == ProjectStructureProcessStartStage.Confirm)
            {
                var launchName = $"{startContext.ResolveTargetNodeTitle()} / {node.Title}";
                var createRequest = new ProcessLaunchCreateRequest
                {
                    ProcessDefinitionId = dialog.ProcessDefinitionId,
                    ProjectId = startContext.ProjectId,
                    LaunchName = launchName,
                    OperatingMode = ProcessOperatingMode.AssistedExecution,
                    TriggerReason = "Started from project structure.",
                    ProjectStructureContext = startContext,
                    RequestedBy = "project-structure"
                };
                var createTask = ProcessesService.CreateLaunchPlanAsync(createRequest);
                Result<Guid> createResult;
                try
                {
                    createResult = await createTask.WaitAsync(ProcessStartLaunchPlanCreateTimeout);
                }
                catch (TimeoutException exception)
                {
                    var delayedCreateResult = await TryCompleteTimedOutLaunchPlanCreationAsync(createTask);
                    if (delayedCreateResult is not null)
                    {
                        createResult = delayedCreateResult;
                        Logger.LogWarning(
                            exception,
                            "Recovered project-structure process start by awaiting the original launch plan creation after timeout. ProjectId={ProjectId} ProcessDefinitionId={ProcessDefinitionId}",
                            ProjectId,
                            dialog.ProcessDefinitionId);
                    }
                    else
                    {
                        ObserveTimedOutLaunchPlanCreation(createTask, dialog);
                        processStartDialog = dialog with
                        {
                            IsBusy = true,
                            StatusMessage = "Launch plan creation is still running. Staffing review will open here when it is ready.",
                            Error = string.Empty
                        };
                        Logger.LogWarning(
                            exception,
                            "Project-structure process start launch plan creation exceeded the interactive wait. The dialog remains pending and will recover when the launch plan is ready. ProjectId={ProjectId} ProcessDefinitionId={ProcessDefinitionId}",
                            ProjectId,
                            dialog.ProcessDefinitionId);
                        await InvokeAsync(StateHasChanged);
                        return;
                    }
                }

                if (createResult.IsFailure)
                {
                    await SetProcessActionErrorAsync(createResult.Errors);
                    return;
                }

                var launchPlan = await ProcessesService.GetLaunchPlanAsync(createResult.Value);
                if (launchPlan is null)
                {
                    processStartDialog = dialog with
                    {
                        LaunchPlanId = createResult.Value,
                        IsBusy = false,
                        Error = "The launch plan was created but could not be loaded for staffing."
                    };
                    return;
                }

                await RefreshProcessStartAgentMetadataAsync();
                processStartDialog = MapProcessStartDialogState(
                    dialog,
                    launchPlan,
                    HasRequiredRoleGaps(launchPlan)
                        ? "Assign the required roles before the process can start."
                        : "Review the planned assignments before starting the process.");
                await InvokeAsync(StateHasChanged);
                return;
            }
            else if (!dialog.LaunchPlanId.HasValue)
            {
                processStartDialog = dialog with
                {
                    IsBusy = false,
                    Error = "The launch plan is missing. Close the dialog and try again."
                };
                return;
            }
            else
            {
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

                var currentLaunchPlan = await ProcessesService.GetLaunchPlanAsync(dialog.LaunchPlanId.Value);
                if (currentLaunchPlan is null)
                {
                    processStartDialog = dialog with
                    {
                        IsBusy = false,
                        Error = "The launch plan could not be reloaded. Close the dialog and try again."
                    };
                    return;
                }

                if (HasRequiredRoleGaps(currentLaunchPlan))
                {
                    await RefreshProcessStartAgentMetadataAsync();
                    processStartDialog = MapProcessStartDialogState(
                        dialog,
                        currentLaunchPlan,
                        error: "Resolve every required role before starting the process.");
                    await InvokeAsync(StateHasChanged);
                    return;
                }
            }

            await ContinueProcessStartAsync(processStartDialog!, startContext, node);
        }
        catch (Exception exception)
        {
            await SetProcessActionExceptionAsync(exception, "starting the process");
        }
    }

    private static async Task<Result<Guid>?> TryCompleteTimedOutLaunchPlanCreationAsync(Task<Result<Guid>> createTask)
    {
        try
        {
            return await createTask.WaitAsync(ProcessStartLaunchPlanCreateRecoveryTimeout);
        }
        catch (TimeoutException)
        {
            return null;
        }
    }

    private void ObserveTimedOutLaunchPlanCreation(
        Task<Result<Guid>> createTask,
        ProjectStructureProcessStartDialogState originalDialog)
    {
        _ = RecoverTimedOutLaunchPlanCreationAsync(createTask, originalDialog);
    }

    private async Task RecoverTimedOutLaunchPlanCreationAsync(
        Task<Result<Guid>> createTask,
        ProjectStructureProcessStartDialogState originalDialog)
    {
        Result<Guid> result;
        try
        {
            result = await createTask;
        }
        catch (Exception exception)
        {
            Logger.LogWarning(
                exception,
                "Timed-out project-structure launch plan creation later faulted. ProjectId={ProjectId} ProcessDefinitionId={ProcessDefinitionId}",
                originalDialog.ProjectId,
                originalDialog.ProcessDefinitionId);
            await ApplyTimedOutLaunchPlanCreationFailureAsync(
                originalDialog,
                "Launch plan creation failed after the initial wait. Review the logs and try again.");
            return;
        }

        if (result.IsFailure)
        {
            var message = result.Errors.FirstOrDefault()?.Message ?? "Unknown launch-plan creation error.";
            Logger.LogWarning(
                "Timed-out project-structure launch plan creation later failed. ProjectId={ProjectId} ProcessDefinitionId={ProcessDefinitionId} Error={Error}",
                originalDialog.ProjectId,
                originalDialog.ProcessDefinitionId,
                message);
            await ApplyTimedOutLaunchPlanCreationFailureAsync(originalDialog, message);
            return;
        }

        Logger.LogWarning(
            "Timed-out project-structure launch plan creation recovered after the initial UI wait. ProjectId={ProjectId} ProcessDefinitionId={ProcessDefinitionId} LaunchPlanId={LaunchPlanId}",
            originalDialog.ProjectId,
            originalDialog.ProcessDefinitionId,
            result.Value);

        try
        {
            await InvokeAsync(async () =>
            {
                if (!IsCurrentPendingLaunchPlanDialog(originalDialog))
                {
                    return;
                }

                var launchPlan = await ProcessesService.GetLaunchPlanAsync(result.Value);
                if (launchPlan is null)
                {
                    processStartDialog = originalDialog with
                    {
                        LaunchPlanId = result.Value,
                        IsBusy = false,
                        Error = "The launch plan was created but could not be loaded for staffing."
                    };
                    StateHasChanged();
                    return;
                }

                await RefreshProcessStartAgentMetadataAsync();
                processStartDialog = MapProcessStartDialogState(
                    originalDialog,
                    launchPlan,
                    HasRequiredRoleGaps(launchPlan)
                        ? "Assign the required roles before the process can start."
                        : "Review the planned assignments before starting the process.");
                StateHasChanged();
            });
        }
        catch (InvalidOperationException exception)
        {
            Logger.LogDebug(
                exception,
                "Skipped timed-out project-structure launch plan UI recovery because the component was no longer available. ProjectId={ProjectId} ProcessDefinitionId={ProcessDefinitionId} LaunchPlanId={LaunchPlanId}",
                originalDialog.ProjectId,
                originalDialog.ProcessDefinitionId,
                result.Value);
        }
    }

    private Task ApplyTimedOutLaunchPlanCreationFailureAsync(
        ProjectStructureProcessStartDialogState originalDialog,
        string message)
    {
        return InvokeAsync(() =>
        {
            if (!IsCurrentPendingLaunchPlanDialog(originalDialog))
            {
                return;
            }

            processStartDialog = originalDialog with
            {
                IsBusy = false,
                Error = message
            };
            StateHasChanged();
        });
    }

    private bool IsCurrentPendingLaunchPlanDialog(ProjectStructureProcessStartDialogState originalDialog)
    {
        return processStartDialog is
        {
            LaunchPlanId: null,
            Stage: ProjectStructureProcessStartStage.Confirm
        } current &&
        current.ProjectId == originalDialog.ProjectId &&
        current.ProcessDefinitionId == originalDialog.ProcessDefinitionId &&
        string.Equals(current.NodeId, originalDialog.NodeId, StringComparison.Ordinal);
    }

    private async Task SelectProcessStartCandidateAsync(ProjectStructureProcessStartCandidateSelection selection)
    {
        if (processStartDialog is null || !processStartDialog.LaunchPlanId.HasValue)
        {
            return;
        }

        try
        {
            processStartDialog = processStartDialog with
            {
                IsBusy = true,
                Error = string.Empty,
                ConfirmHrManagerMatch = false
            };
            await InvokeAsync(StateHasChanged);

            var result = await ProcessesService.SelectLaunchCandidateAsync(
                new ProcessLaunchCandidateSelectionRequest
                {
                    LaunchPlanId = processStartDialog.LaunchPlanId.Value,
                    LaunchPlanRoleId = selection.LaunchPlanRoleId,
                    CandidateId = selection.CandidateId
                });
            if (result.IsFailure)
            {
                await SetProcessActionErrorAsync(result.Errors);
                return;
            }

            await ReloadProcessStartLaunchPlanAsync(
                processStartDialog.LaunchPlanId.Value,
                "Role selection updated.");
        }
        catch (Exception exception)
        {
            await SetProcessActionExceptionAsync(exception, "selecting a staffing candidate");
        }
    }

    private async Task OpenManualProcessStartAgentPickerAsync(Guid launchPlanRoleId)
    {
        if (processStartDialog is null || !processStartDialog.LaunchPlanId.HasValue)
        {
            return;
        }

        var launchPlanId = processStartDialog.LaunchPlanId.Value;
        var role = processStartDialog.Roles.FirstOrDefault(item => item.LaunchPlanRoleId == launchPlanRoleId);
        if (role is null)
        {
            processStartDialog = processStartDialog with { Error = "The selected launch role was not found. Reload the launch plan and try again." };
            await InvokeAsync(StateHasChanged);
            return;
        }

        try
        {
            var agentsTask = AgentWorkspaceService.ListAgentsAsync(includeTemplates: false);
            var providersTask = AgentWorkspaceService.ListProvidersAsync();
            var agents = await agentsTask;
            var privateProviderIds = (await providersTask)
                .Where(provider => provider.IsPrivateProvider)
                .Select(provider => provider.Id)
                .ToHashSet();
            var privateAgentIds = agents
                .Where(agent => agent.ProviderProfileId.HasValue && privateProviderIds.Contains(agent.ProviderProfileId.Value))
                .Select(agent => agent.Id)
                .ToHashSet();
            await RefreshProcessStartAgentMetadataAsync(agents);
            var selectedAgentId = role.Candidates
                .FirstOrDefault(candidate => candidate.IsSelected && candidate.TechnicalAgentId.HasValue)
                ?.TechnicalAgentId;
            var result = await DialogService.OpenAsync<AgentSwitchDialog>(
                $"Assign {role.DisplayName}",
                new Dictionary<string, object?>
                {
                    [nameof(AgentSwitchDialog.Agents)] = agents,
                    [nameof(AgentSwitchDialog.SelectedAgentId)] = selectedAgentId,
                    [nameof(AgentSwitchDialog.PrivateAgentIds)] = privateAgentIds,
                    [nameof(AgentSwitchDialog.FavoriteToggled)] =
                        (Func<AgentDefinition, Task<AgentDefinition>>)ToggleProcessStartAgentFavoriteAsync
                },
                new DialogOptions
                {
                    Eyebrow = "Process assignment",
                    Subtitle = "Choose the technical AI agent for this process role.",
                    Size = ModalSize.Wide,
                    DenseChrome = true,
                    TestId = "project-structure-process-assignment-agent-switch-dialog",
                    AriaLabel = "Assign process role agent"
                });

            if (result is not Guid agentId)
            {
                return;
            }

            if (processStartDialog?.LaunchPlanId != launchPlanId)
            {
                return;
            }

            await ApplyManualProcessStartAgentSelectionAsync(launchPlanId, launchPlanRoleId, agentId);
        }
        catch (Exception exception)
        {
            await SetProcessActionExceptionAsync(exception, "opening the AI agent directory");
        }
    }

    private async Task ApplyManualProcessStartAgentSelectionAsync(
        Guid launchPlanId,
        Guid launchPlanRoleId,
        Guid technicalAgentId)
    {
        if (processStartDialog is null || processStartDialog.LaunchPlanId != launchPlanId)
        {
            return;
        }

        var role = processStartDialog.Roles.FirstOrDefault(item => item.LaunchPlanRoleId == launchPlanRoleId);
        if (role is null)
        {
            processStartDialog = processStartDialog with { Error = "The selected launch role was not found. Reload the launch plan and try again." };
            await InvokeAsync(StateHasChanged);
            return;
        }

        var existingCandidate = role.Candidates.FirstOrDefault(candidate =>
            candidate.IsResolvable &&
            candidate.TechnicalAgentId == technicalAgentId);
        if (existingCandidate is not null)
        {
            if (existingCandidate.IsSelected)
            {
                return;
            }

            await SelectProcessStartCandidateAsync(
                new ProjectStructureProcessStartCandidateSelection(launchPlanRoleId, existingCandidate.CandidateId));
            return;
        }

        try
        {
            processStartDialog = processStartDialog with
            {
                IsBusy = true,
                Error = string.Empty,
                ConfirmHrManagerMatch = false
            };
            await InvokeAsync(StateHasChanged);

            var result = await ProcessesService.SelectLaunchTechnicalAgentAsync(
                new ProcessLaunchTechnicalAgentSelectionRequest
                {
                    LaunchPlanId = launchPlanId,
                    LaunchPlanRoleId = launchPlanRoleId,
                    TechnicalAgentId = technicalAgentId
                });
            if (result.IsFailure)
            {
                await SetProcessActionErrorAsync(result.Errors);
                return;
            }

            await ReloadProcessStartLaunchPlanAsync(
                launchPlanId,
                "Role selection updated from the AI agent directory.");
        }
        catch (Exception exception)
        {
            await SetProcessActionExceptionAsync(exception, "assigning the selected AI agent");
        }
    }

    private async Task<AgentDefinition> ToggleProcessStartAgentFavoriteAsync(AgentDefinition agent)
    {
        var editor = await AgentWorkspaceService.GetAgentEditorAsync(agent.Id);
        if (editor.Id is null)
        {
            throw new InvalidOperationException("Agent was not found.");
        }

        if (editor.Tags.Any(AgentSpecialTags.IsFavorite))
        {
            editor.Tags = editor.Tags
                .Where(item => !AgentSpecialTags.IsFavorite(item))
                .ToList();
        }
        else
        {
            editor.Tags = editor.Tags
                .Append(AgentSpecialTags.Favorite)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        await AgentWorkspaceService.SaveAgentAsync(editor);
        var agents = await AgentWorkspaceService.ListAgentsAsync(includeTemplates: false);
        await RefreshProcessStartAgentMetadataAsync(agents);
        return agents.FirstOrDefault(item => item.Id == agent.Id)
            ?? throw new InvalidOperationException("Agent was not found after saving favorite state.");
    }

    private Task HandleProcessStartAssignmentsReviewedChanged(ChangeEventArgs args)
    {
        if (processStartDialog is null)
        {
            return Task.CompletedTask;
        }

        var reviewed = args.Value switch
        {
            bool boolValue => boolValue,
            string stringValue when bool.TryParse(stringValue, out var parsed) => parsed,
            _ => false
        };

        processStartDialog = processStartDialog with
        {
            AssignmentsReviewed = reviewed,
            Error = string.Empty,
            StatusMessage = reviewed
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
        if (processStartDialog is null || !processStartDialog.LaunchPlanId.HasValue)
        {
            return;
        }

        try
        {
            var launchPlanId = processStartDialog.LaunchPlanId.Value;
            processStartDialog = processStartDialog with
            {
                IsBusy = true,
                StatusMessage = $"{ProjectStructureHrManagerName} is matching roles from CRM-HR and the projected AI agent directory.",
                Error = string.Empty
            };
            await InvokeAsync(StateHasChanged);

            using var matchTimeout = new CancellationTokenSource(ProcessStartHrManagerMatchTimeout);
            Result result;
            try
            {
                result = await ProcessesService.MatchLaunchPlanWithHrManagerAsync(
                    launchPlanId,
                    "project-structure",
                    matchTimeout.Token);
            }
            catch (OperationCanceledException exception) when (matchTimeout.IsCancellationRequested)
            {
                await SetHrManagerMatchTimeoutAsync(exception, launchPlanId);
                return;
            }

            if (result.IsFailure)
            {
                await SetProcessActionErrorAsync(result.Errors);
                return;
            }

            await ReloadProcessStartLaunchPlanAsync(
                launchPlanId,
                $"{ProjectStructureHrManagerName} refreshed the staffing suggestions.");
        }
        catch (Exception exception)
        {
            await SetProcessActionExceptionAsync(exception, "requesting HR manager staffing");
        }
    }

    private async Task ContinueProcessStartAsync(
        ProjectStructureProcessStartDialogState dialog,
        ProcessProjectStructureContext startContext,
        ProjectStructureNode node)
    {
        if (!dialog.LaunchPlanId.HasValue)
        {
            processStartDialog = dialog with
            {
                IsBusy = false,
                Error = "The launch plan is missing. Close the dialog and try again."
            };
            return;
        }

        var launchPlanId = dialog.LaunchPlanId.Value;
        var submitResult = await ProcessesService.SubmitLaunchPlanForApprovalAsync(launchPlanId, "project-structure");
        if (submitResult.IsFailure)
        {
            await SetProcessActionErrorAsync(submitResult.Errors);
            return;
        }

        var approvalResult = await ProcessesService.DecideLaunchPlanApprovalAsync(
            new ProcessLaunchApprovalDecisionRequest
            {
                LaunchPlanId = launchPlanId,
                Status = ProcessLaunchApprovalStatus.Approved,
                ResolutionSummary = $"Approved from project structure start for '{startContext.ResolveTargetNodeTitle()}' using '{node.Title}'.",
                DecidedBy = "project-structure"
            });
        if (approvalResult.IsFailure)
        {
            await SetProcessActionErrorAsync(approvalResult.Errors);
            return;
        }

        var provisioningResult = await ProcessesService.ProvisionLaunchPlanAsync(launchPlanId, "project-structure");
        if (provisioningResult.IsFailure)
        {
            await SetProcessActionErrorAsync(provisioningResult.Errors);
            return;
        }

        var executionResult = await ProcessesService.ExecuteLaunchPlanAsync(
            new ProcessLaunchExecutionRequest
            {
                LaunchPlanId = launchPlanId,
                RequestedBy = "project-structure"
            });
        if (executionResult.IsFailure)
        {
            await SetProcessActionErrorAsync(executionResult.Errors);
            return;
        }

        processStartDialog = null;
        await TryLinkStartedProcessRunAsync(startContext, executionResult.Value);
        workflowFeedback = $"{node.Title} started for {startContext.ResolveTargetNodeTitle()}.";
        workflowFeedbackTone = "mint";
        Navigation.NavigateTo($"/projects/{ProjectId:D}/processes?processId={dialog.ProcessDefinitionId:D}&runId={executionResult.Value:D}");
    }

    private async Task ReloadProcessStartLaunchPlanAsync(Guid launchPlanId, string statusMessage)
    {
        if (processStartDialog is null)
        {
            return;
        }

        var launchPlan = await ProcessesService.GetLaunchPlanAsync(launchPlanId);
        if (launchPlan is null)
        {
            processStartDialog = processStartDialog with
            {
                IsBusy = false,
                Error = "The launch plan could not be reloaded. Close the dialog and try again."
            };
            await InvokeAsync(StateHasChanged);
            return;
        }

        await RefreshProcessStartAgentMetadataAsync();
        processStartDialog = MapProcessStartDialogState(processStartDialog, launchPlan, statusMessage);
        await InvokeAsync(StateHasChanged);
    }

    private Task SetProcessActionErrorAsync(IReadOnlyCollection<Error> errors)
    {
        var message = errors.FirstOrDefault()?.Message ?? "The process action could not be completed.";
        if (processStartDialog is not null)
        {
            processStartDialog = processStartDialog with
            {
                IsBusy = false,
                ConfirmHrManagerMatch = false,
                Error = message
            };
        }

        workflowFeedback = message;
        workflowFeedbackTone = "warn";
        return InvokeAsync(StateHasChanged);
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

    private Task SetHrManagerMatchTimeoutAsync(Exception exception, Guid launchPlanId)
    {
        var message = $"{ProjectStructureHrManagerName} did not finish matching roles within {ProcessStartHrManagerMatchTimeout.TotalSeconds:0} seconds. The process has not started. Try the HR match again after Agent Framework catalog recovery settles.";
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
            "Project structure HR manager matching timed out. ProjectId={ProjectId} ProcessDefinitionId={ProcessDefinitionId} LaunchPlanId={LaunchPlanId} TimeoutSeconds={TimeoutSeconds}",
            ProjectId,
            processStartDialog?.ProcessDefinitionId,
            launchPlanId,
            ProcessStartHrManagerMatchTimeout.TotalSeconds);
        workflowFeedback = message;
        workflowFeedbackTone = "warn";
        return InvokeAsync(StateHasChanged);
    }

    private static bool HasRequiredRoleGaps(ProcessLaunchPlanDetails launchPlan)
    {
        return launchPlan.Roles.Any(role => role.IsRequired && !role.IsResolved);
    }

    private async Task<IReadOnlyList<AgentDefinition>> RefreshProcessStartAgentMetadataAsync(
        IReadOnlyList<AgentDefinition>? knownAgents = null)
    {
        var agents = knownAgents?.ToList()
            ?? (await AgentWorkspaceService.ListAgentsAsync(includeTemplates: false)).ToList();

        IReadOnlyList<ProviderProfile> providers = [];
        try
        {
            providers = await AgentWorkspaceService.ListProvidersAsync();
        }
        catch (Exception exception)
        {
            Logger.LogDebug(exception, "Agent provider metadata could not be loaded for process assignment badges.");
        }

        var providerById = providers.ToDictionary(item => item.Id);
        processStartAgentMetadataById = agents.ToDictionary(
            item => item.Id,
            item =>
            {
                ProviderProfile? provider = null;
                if (item.ProviderProfileId.HasValue)
                {
                    providerById.TryGetValue(item.ProviderProfileId.Value, out provider);
                }

                return ProjectStructureProcessStartAgentMetadata.FromAgent(item, provider);
            });

        return agents;
    }

    private ProjectStructureProcessStartDialogState MapProcessStartDialogState(
        ProjectStructureProcessStartDialogState dialog,
        ProcessLaunchPlanDetails launchPlan,
        string statusMessage = "",
        string error = "")
    {
        return dialog with
        {
            LaunchPlanId = launchPlan.Id,
            Stage = ProjectStructureProcessStartStage.Staffing,
            IsBusy = false,
            ConfirmHrManagerMatch = false,
            StatusMessage = string.IsNullOrWhiteSpace(statusMessage)
                ? $"Resolved {launchPlan.Roles.Count(role => role.IsResolved)} of {launchPlan.Roles.Count} roles."
                : statusMessage,
            StageActivatedAtUtc = DateTimeOffset.UtcNow,
            AssignmentsReviewed = false,
            Roles = launchPlan.Roles
                .Select(role => new ProjectStructureProcessStartRoleState(
                    role.Id,
                    role.DisplayName,
                    role.PreferredExecutorKind,
                    role.IsRequired,
                    role.IsResolved,
                    role.RequiresProvisioning,
                    role.SelectionSummary,
                    role.ReadinessSummary,
                    role.Candidates
                        .Select(candidate => MapProcessStartCandidateState(role, candidate))
                        .ToList()))
                .ToList(),
            Estimate = launchPlan.Estimate,
            Error = error
        };
    }

    private ProjectStructureProcessStartCandidateState MapProcessStartCandidateState(
        ProcessLaunchRoleViewModel role,
        ProcessLaunchCandidateViewModel candidate)
    {
        var metadata = candidate.TechnicalAgentId.HasValue &&
                       processStartAgentMetadataById.TryGetValue(candidate.TechnicalAgentId.Value, out var match)
            ? match
            : ProjectStructureProcessStartAgentMetadata.Empty;

        return new ProjectStructureProcessStartCandidateState(
            candidate.Id,
            candidate.TechnicalAgentId,
            candidate.DisplayName,
            candidate.CandidateKind.ToString(),
            candidate.ExecutorKind,
            $"{candidate.Score:0.0} score",
            role.SelectedCandidateId == candidate.Id,
            candidate.IsRecommended,
            candidate.RequiresProvisioning,
            candidate.CandidateKind != ProcessLaunchCandidateKind.Gap,
            candidate.RecommendationSummary,
            candidate.AvailabilitySummary,
            candidate.SourceRegistryKey,
            metadata.ProviderName,
            metadata.Model,
            metadata.RoleTitle,
            metadata.Summary,
            metadata.StatusLabel,
            metadata.WorkloadLabel,
            metadata.AvatarImageUrl,
            metadata.ToolNames,
            metadata.SkillNames);
    }

    private sealed record ProjectStructureProcessStartAgentMetadata(
        string ProviderName,
        string Model,
        string RoleTitle,
        string Summary,
        string StatusLabel,
        string WorkloadLabel,
        string AvatarImageUrl,
        IReadOnlyList<string> ToolNames,
        IReadOnlyList<string> SkillNames)
    {
        public static ProjectStructureProcessStartAgentMetadata Empty { get; } = new(
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            [],
            []);

        public static ProjectStructureProcessStartAgentMetadata FromAgent(
            AgentDefinition agent,
            ProviderProfile? provider)
        {
            var toolNames = agent.Capabilities
                .Where(item => item.Kind is not CapabilityKind.Skill)
                .Select(item => ResolveCapabilityDisplayName(item))
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var skillNames = agent.Capabilities
                .Where(item => item.Kind == CapabilityKind.Skill)
                .Select(item => ResolveCapabilityDisplayName(item))
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return new ProjectStructureProcessStartAgentMetadata(
                provider?.Name ?? string.Empty,
                agent.Model,
                agent.RoleTitle,
                agent.Summary,
                agent.Status.ToString(),
                agent.Workload.ToString(),
                agent.AvatarImageUrl ?? string.Empty,
                toolNames,
                skillNames);
        }

        private static string ResolveCapabilityDisplayName(AgentCapabilityAssignment capability)
        {
            if (!string.IsNullOrWhiteSpace(capability.CapabilityKey))
            {
                return capability.CapabilityKey;
            }

            return capability.Kind.ToString();
        }
    }

    private ProcessProjectStructureContext CreateProcessStartContext(ProjectStructureNode node)
    {
        var parentNode = ResolveProcessStartTargetNode(node) ?? ResolveNode(node.ParentId);
        return new ProcessProjectStructureContext
        {
            ProjectId = ProjectId,
            NodeId = node.Id,
            NodeTitle = node.Title,
            ParentNodeId = parentNode?.Id,
            ParentNodeTitle = parentNode?.Title ?? string.Empty
        };
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

    private async Task TryLinkStartedProcessRunAsync(ProcessProjectStructureContext startContext, Guid runId)
    {
        var sourceNodeId = startContext.ResolveTargetNodeId();
        if (string.IsNullOrWhiteSpace(sourceNodeId))
        {
            return;
        }

        try
        {
            await ProjectWorkbenchService.LinkObjectsAsync(
                ProjectId,
                sourceNodeId,
                BuildProcessRunNodeKey(runId),
                ProjectObjectLinkKind.Uses);
        }
        catch (InvalidOperationException)
        {
            // Keep the process run alive even if the graph relation already exists or cannot be added.
        }
    }

    private static ProjectStructureProcessLinkOption MapProcessLinkOption(ProcessDefinitionListItem definition)
    {
        var scopeLabel = definition.ProjectId.HasValue ? "Project" : "Global";
        return new ProjectStructureProcessLinkOption(
            definition.Id,
            definition.Name,
            scopeLabel,
            definition.Status.ToString(),
            definition.HasPublishedVersion);
    }

    private static Guid? ResolveProcessDefinitionId(ProjectStructureNode node)
    {
        if (node.ArtifactId.HasValue)
        {
            return node.ArtifactId.Value;
        }

        return TryParsePrefixedGuidNodeKey(node.Id, ProjectStructureProcessNodeKeys.ProcessDefinitionPrefix, out var definitionId)
            ? definitionId
            : null;
    }

    private static string BuildProcessDefinitionNodeKey(Guid definitionId)
    {
        return ProjectStructureProcessNodeKeys.BuildProcessDefinitionNodeKey(definitionId);
    }

    private static string BuildProcessRunNodeKey(Guid runId)
    {
        return ProjectStructureProcessNodeKeys.BuildProcessRunNodeKey(runId);
    }

    private static bool TryParsePrefixedGuidNodeKey(string nodeKey, string prefix, out Guid value)
    {
        if (nodeKey.StartsWith(prefix, StringComparison.Ordinal) &&
            Guid.TryParse(nodeKey[prefix.Length..], out value))
        {
            return true;
        }

        value = Guid.Empty;
        return false;
    }
}
