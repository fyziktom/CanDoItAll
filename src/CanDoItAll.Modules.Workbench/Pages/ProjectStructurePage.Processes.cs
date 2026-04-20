using CanDoItAll.Modules.Processes;
using CanDoItAll.SharedKernel;
using Microsoft.AspNetCore.Components;

namespace CanDoItAll.Modules.Workbench.Pages;

public partial class ProjectStructurePage
{
    private const string ProjectStructureHrManagerName = "HR Staffing Manager";

    [Inject]
    private ProcessesService ProcessesService { get; set; } = default!;

    private ProjectStructureProcessLinkDialogState? processLinkDialog;
    private ProjectStructureProcessStartDialogState? processStartDialog;

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
            string.Empty);
        await InvokeAsync(StateHasChanged);
    }

    private void CloseProcessStartDialog()
    {
        processStartDialog = null;
    }

    private async Task ExecuteProcessStartAsync()
    {
        if (processStartDialog is null)
        {
            return;
        }

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
            var createResult = await ProcessesService.CreateLaunchPlanAsync(
                new ProcessLaunchCreateRequest
                {
                    ProcessDefinitionId = dialog.ProcessDefinitionId,
                    ProjectId = startContext.ProjectId,
                    LaunchName = $"{startContext.ResolveTargetNodeTitle()} / {node.Title}",
                    OperatingMode = ProcessOperatingMode.AssistedExecution,
                    TriggerReason = "Started from project structure.",
                    ProjectStructureContext = startContext,
                    RequestedBy = "project-structure"
                });
            if (createResult.IsFailure)
            {
                SetProcessActionError(createResult.Errors);
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

            processStartDialog = MapProcessStartDialogState(
                processStartDialog!,
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

    private async Task SelectProcessStartCandidateAsync(ProjectStructureProcessStartCandidateSelection selection)
    {
        if (processStartDialog is null || !processStartDialog.LaunchPlanId.HasValue)
        {
            return;
        }

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
            SetProcessActionError(result.Errors);
            return;
        }

        await ReloadProcessStartLaunchPlanAsync(
            processStartDialog.LaunchPlanId.Value,
            "Role selection updated.");
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

        processStartDialog = processStartDialog with
        {
            IsBusy = true,
            Error = string.Empty
        };
        await InvokeAsync(StateHasChanged);

        var result = await ProcessesService.MatchLaunchPlanWithHrManagerAsync(
            processStartDialog.LaunchPlanId.Value,
            "project-structure");
        if (result.IsFailure)
        {
            SetProcessActionError(result.Errors);
            return;
        }

        await ReloadProcessStartLaunchPlanAsync(
            processStartDialog.LaunchPlanId.Value,
            $"{ProjectStructureHrManagerName} refreshed the staffing suggestions.");
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
            SetProcessActionError(submitResult.Errors);
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
            SetProcessActionError(approvalResult.Errors);
            return;
        }

        var provisioningResult = await ProcessesService.ProvisionLaunchPlanAsync(launchPlanId, "project-structure");
        if (provisioningResult.IsFailure)
        {
            SetProcessActionError(provisioningResult.Errors);
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
            SetProcessActionError(executionResult.Errors);
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

        processStartDialog = MapProcessStartDialogState(processStartDialog, launchPlan, statusMessage);
        await InvokeAsync(StateHasChanged);
    }

    private void SetProcessActionError(IReadOnlyCollection<Error> errors)
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
    }

    private static bool HasRequiredRoleGaps(ProcessLaunchPlanDetails launchPlan)
    {
        return launchPlan.Roles.Any(role => role.IsRequired && !role.IsResolved);
    }

    private static ProjectStructureProcessStartDialogState MapProcessStartDialogState(
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
                        .Select(candidate => new ProjectStructureProcessStartCandidateState(
                            candidate.Id,
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
                            candidate.SourceRegistryKey))
                        .ToList()))
                .ToList(),
            Error = error
        };
    }

    private ProcessProjectStructureContext CreateProcessStartContext(ProjectStructureNode node)
    {
        var parentNode = ResolveNode(node.ParentId);
        return new ProcessProjectStructureContext
        {
            ProjectId = ProjectId,
            NodeId = node.Id,
            NodeTitle = node.Title,
            ParentNodeId = node.ParentId,
            ParentNodeTitle = parentNode?.Title ?? string.Empty
        };
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

        return TryParsePrefixedGuidNodeKey(node.Id, "process-definition:", out var definitionId)
            ? definitionId
            : null;
    }

    private static string BuildProcessDefinitionNodeKey(Guid definitionId)
    {
        return $"process-definition:{definitionId:D}";
    }

    private static string BuildProcessRunNodeKey(Guid runId)
    {
        return $"process-run:{runId:D}";
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
