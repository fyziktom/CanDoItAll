using Microsoft.AspNetCore.Components;

namespace CanDoItAll.Modules.Processes;

public partial class ProcessWorkspace
{
    [SupplyParameterFromQuery(Name = "launchPlanId")]
    private Guid? LaunchPlanIdQuery { get; set; }

    private IReadOnlyList<ProcessLaunchPlanListItem> launchPlans = [];
    private ProcessLaunchPlanDetails? selectedLaunchPlan;
    private Guid? selectedLaunchPlanId;
    private string launchNameDraft = string.Empty;
    private string launchDecisionSummary = string.Empty;
    private Guid? loadedLaunchPlanQueryId;

    private ProcessLaunchPlanListItem? SelectedLaunchPlanSummary
        => selectedLaunchPlanId.HasValue
            ? launchPlans.FirstOrDefault(item => item.Id == selectedLaunchPlanId.Value)
            : null;

    private Guid? ResolveSelectedLaunchPlanId()
    {
        if (LaunchPlanIdQuery.HasValue && launchPlans.Any(item => item.Id == LaunchPlanIdQuery.Value))
        {
            return LaunchPlanIdQuery.Value;
        }

        if (selectedLaunchPlanId.HasValue && launchPlans.Any(item => item.Id == selectedLaunchPlanId.Value))
        {
            return selectedLaunchPlanId.Value;
        }

        return launchPlans.FirstOrDefault()?.Id;
    }

    private async Task LoadLaunchPlanDetailsAsync()
    {
        if (!selectedLaunchPlanId.HasValue)
        {
            selectedLaunchPlan = null;
            return;
        }

        selectedLaunchPlan = await ProcessesService.GetLaunchPlanAsync(selectedLaunchPlanId.Value);
    }

    private async Task SelectLaunchPlanAsync(Guid launchPlanId)
    {
        selectedLaunchPlanId = launchPlanId;
        await LoadLaunchPlanDetailsAsync();
    }

    private async Task CreateLaunchPlanAsync()
    {
        if (!selectedProcessId.HasValue)
        {
            SetError("Select a process definition before creating a launch plan.");
            return;
        }

        var result = await ProcessesService.CreateLaunchPlanAsync(
            new ProcessLaunchCreateRequest
            {
                ProcessDefinitionId = selectedProcessId.Value,
                ProjectId = ProjectId,
                LaunchName = launchNameDraft,
                OperatingMode = runOperatingMode,
                TriggerReason = "Created from the process workspace.",
                RequestedBy = "process-workspace"
            });
        if (result.IsFailure)
        {
            SetError(result.Errors);
            return;
        }

        selectedLaunchPlanId = result.Value;
        detailTab = "runs";
        launchNameDraft = string.Empty;
        await LoadWorkspaceAsync();
        SetMessage("Launch plan created.");
    }

    private async Task SelectLaunchCandidateAsync(Guid launchPlanRoleId, Guid candidateId)
    {
        if (!selectedLaunchPlanId.HasValue)
        {
            SetError("Select a launch plan before choosing candidates.");
            return;
        }

        var result = await ProcessesService.SelectLaunchCandidateAsync(
            new ProcessLaunchCandidateSelectionRequest
            {
                LaunchPlanId = selectedLaunchPlanId.Value,
                LaunchPlanRoleId = launchPlanRoleId,
                CandidateId = candidateId
            });
        if (result.IsFailure)
        {
            SetError(result.Errors);
            return;
        }

        await LoadWorkspaceAsync();
        detailTab = "runs";
        SetMessage("Launch candidate selected.");
    }

    private async Task SubmitLaunchPlanForApprovalAsync()
    {
        if (!selectedLaunchPlanId.HasValue)
        {
            SetError("Select a launch plan before sending it for approval.");
            return;
        }

        var result = await ProcessesService.SubmitLaunchPlanForApprovalAsync(selectedLaunchPlanId.Value, "process-workspace");
        if (result.IsFailure)
        {
            SetError(result.Errors);
            return;
        }

        launchDecisionSummary = string.Empty;
        await LoadWorkspaceAsync();
        detailTab = "runs";
        SetMessage("Launch plan submitted for approval.");
    }

    private async Task DecideLaunchPlanAsync(ProcessLaunchApprovalStatus status)
    {
        if (!selectedLaunchPlanId.HasValue)
        {
            SetError("Select a launch plan before recording an approval decision.");
            return;
        }

        var result = await ProcessesService.DecideLaunchPlanApprovalAsync(
            new ProcessLaunchApprovalDecisionRequest
            {
                LaunchPlanId = selectedLaunchPlanId.Value,
                Status = status,
                ResolutionSummary = launchDecisionSummary,
                DecidedBy = "process-workspace"
            });
        if (result.IsFailure)
        {
            SetError(result.Errors);
            return;
        }

        launchDecisionSummary = string.Empty;
        await LoadWorkspaceAsync();
        detailTab = "runs";
        SetMessage($"Launch plan updated to {status}.");
    }

    private async Task ProvisionLaunchPlanAsync()
    {
        if (!selectedLaunchPlanId.HasValue)
        {
            SetError("Select a launch plan before provisioning.");
            return;
        }

        var result = await ProcessesService.ProvisionLaunchPlanAsync(selectedLaunchPlanId.Value, "process-workspace");
        if (result.IsFailure)
        {
            SetError(result.Errors);
            return;
        }

        await LoadWorkspaceAsync();
        detailTab = "runs";
        SetMessage("Launch provisioning completed.");
    }

    private async Task ExecuteLaunchPlanAsync()
    {
        if (!selectedLaunchPlanId.HasValue)
        {
            SetError("Select a ready launch plan before executing it.");
            return;
        }

        var result = await ProcessesService.ExecuteLaunchPlanAsync(
            new ProcessLaunchExecutionRequest
            {
                LaunchPlanId = selectedLaunchPlanId.Value,
                RequestedBy = "process-workspace"
            });
        if (result.IsFailure)
        {
            SetError(result.Errors);
            return;
        }

        selectedRunId = result.Value;
        selectedCanvasNodeId = null;
        ResetRuntimeCanvasState();
        await LoadWorkspaceAsync();
        detailTab = "runs";
        SetMessage("Launch plan executed into a process run.");
    }

    private static string BuildLaunchPlanSummary(ProcessLaunchPlanListItem plan)
    {
        var statusText = string.IsNullOrWhiteSpace(plan.StatusBadgeText)
            ? plan.Status.ToString()
            : plan.StatusBadgeText;
        return $"{statusText} / {plan.ResolvedRoleCount} of {plan.TotalRoleCount} roles resolved / {plan.PendingProvisioningCount} provisioning";
    }

    private static string ResolveLaunchPlanTone(ProcessLaunchPlanStatus status)
    {
        return status switch
        {
            ProcessLaunchPlanStatus.Ready => "success",
            ProcessLaunchPlanStatus.Completed => "mint",
            ProcessLaunchPlanStatus.PendingApproval => "warning",
            ProcessLaunchPlanStatus.Approved => "info",
            ProcessLaunchPlanStatus.Executing => "info",
            ProcessLaunchPlanStatus.Rejected => "danger",
            ProcessLaunchPlanStatus.Cancelled => "neutral",
            _ => "neutral"
        };
    }

    private static string ResolveLaunchCandidateTone(ProcessLaunchCandidateViewModel candidate)
    {
        if (candidate.CandidateKind == ProcessLaunchCandidateKind.Gap)
        {
            return "danger";
        }

        return candidate.RequiresProvisioning
            ? "warning"
            : candidate.IsRecommended
                ? "success"
                : "neutral";
    }

    private static string ResolveLaunchApprovalTone(ProcessLaunchApprovalStatus status)
    {
        return status switch
        {
            ProcessLaunchApprovalStatus.Approved => "success",
            ProcessLaunchApprovalStatus.Pending => "warning",
            ProcessLaunchApprovalStatus.Rejected => "danger",
            _ => "info"
        };
    }

    private static string ResolveProvisioningTone(ProcessLaunchProvisioningStatus status)
    {
        return status switch
        {
            ProcessLaunchProvisioningStatus.Provisioned => "success",
            ProcessLaunchProvisioningStatus.Pending => "warning",
            ProcessLaunchProvisioningStatus.Rejected => "danger",
            _ => "neutral"
        };
    }
}
