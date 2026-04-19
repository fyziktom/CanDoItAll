using CanDoItAll.Modules.Processes;
using CanDoItAll.SharedKernel;
using Microsoft.AspNetCore.Components;

namespace CanDoItAll.Modules.Workbench.Pages;

public partial class ProjectStructurePage
{
    [Inject]
    private ProcessesService ProcessesService { get; set; } = default!;

    private ProjectStructureProcessLinkDialogState? processLinkDialog;

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

    private async Task ExecuteProcessNodeAsync(ProjectStructureNode node)
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

        var createResult = await ProcessesService.CreateLaunchPlanAsync(
            new ProcessLaunchCreateRequest
            {
                ProcessDefinitionId = processDefinitionId.Value,
                ProjectId = ProjectId,
                LaunchName = $"{node.Title} execution",
                OperatingMode = ProcessOperatingMode.AssistedExecution,
                TriggerReason = $"Started from project structure node '{node.Title}'.",
                RequestedBy = "project-structure"
            });
        if (createResult.IsFailure)
        {
            SetProcessActionError(createResult.Errors);
            return;
        }

        var launchPlanId = createResult.Value;

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
                ResolutionSummary = $"Approved from project structure execution for '{node.Title}'.",
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

        workflowFeedback = $"{node.Title} execution started.";
        workflowFeedbackTone = "mint";
        Navigation.NavigateTo($"/projects/{ProjectId:D}/processes?processId={processDefinitionId.Value:D}&runId={executionResult.Value:D}");
    }

    private void SetProcessActionError(IReadOnlyCollection<Error> errors)
    {
        workflowFeedback = errors.FirstOrDefault()?.Message ?? "The process action could not be completed.";
        workflowFeedbackTone = "warn";
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
