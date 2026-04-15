using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using Microsoft.AspNetCore.Components;

namespace CanDoItAll.Modules.AgentFramework.Pages.Components;

public partial class AgentGovernancePanel
{
    [Parameter]
    public Guid? PreferredAgentId { get; set; }

    [Inject]
    public IAgentFrameworkWorkspaceService WorkspaceService { get; set; } = default!;

    private IReadOnlyList<AgentDefinition> agents = [];
    private IReadOnlyList<ExecutionRunRecord> runs = [];
    private ExecutionRunDetail? selectedDetail;
    private Guid? selectedAgentId;
    private Guid? selectedRunId;
    private bool isBusy;

    protected override async Task OnInitializedAsync()
    {
        await LoadAsync();
    }

    protected override async Task OnParametersSetAsync()
    {
        if (PreferredAgentId.HasValue &&
            PreferredAgentId != selectedAgentId &&
            agents.Any(item => item.Id == PreferredAgentId.Value))
        {
            selectedAgentId = PreferredAgentId.Value;
            await RefreshRunsAsync();
        }
    }

    private async Task LoadAsync()
    {
        isBusy = true;
        try
        {
            agents = await WorkspaceService.ListAgentsAsync(includeTemplates: false);
            if (PreferredAgentId.HasValue &&
                agents.Any(item => item.Id == PreferredAgentId.Value))
            {
                selectedAgentId = PreferredAgentId.Value;
            }

            await RefreshRunsAsync();
        }
        finally
        {
            isBusy = false;
        }
    }

    private async Task HandleAgentFilterChangedAsync(Guid? agentId)
    {
        selectedAgentId = agentId;
        await RefreshRunsAsync();
    }

    private async Task RefreshAsync()
    {
        await LoadAsync();
    }

    private async Task RefreshRunsAsync()
    {
        runs = await WorkspaceService.ListExecutionRunsAsync(
            new ExecutionRunQuery(
                AgentId: selectedAgentId,
                Take: 30));

        if (runs.Count == 0)
        {
            selectedRunId = null;
            selectedDetail = null;
            return;
        }

        var runId = selectedRunId is { } currentRunId &&
                    runs.Any(item => item.Id == currentRunId)
            ? currentRunId
            : runs[0].Id;

        await SelectRunAsync(runId);
    }

    private async Task SelectRunAsync(Guid runId)
    {
        isBusy = true;
        selectedRunId = runId;
        try
        {
            selectedDetail = await WorkspaceService.GetExecutionRunDetailAsync(runId);
        }
        finally
        {
            isBusy = false;
        }
    }

    private string ResolveRunEyebrow(ExecutionRunRecord run)
    {
        var agentName = agents.FirstOrDefault(item => item.Id == run.AgentId)?.Name ?? "Unknown agent";
        return $"{agentName} / {run.ProviderName}";
    }

    private static string ResolveRunSummary(ExecutionRunRecord run)
    {
        return string.IsNullOrWhiteSpace(run.ResultSummary)
            ? run.InputSummary
            : run.ResultSummary;
    }

    private static string ResolveRunTone(ExecutionRunRecord run)
    {
        if (run.Outcome == RunOutcome.Succeeded || run.State == ExecutionState.Completed)
        {
            return "success";
        }

        if (run.Outcome == RunOutcome.Failed || run.State == ExecutionState.Failed)
        {
            return "danger";
        }

        return run.State switch
        {
            ExecutionState.WaitingOnTool => "warning",
            _ => "info"
        };
    }

    private static string ResolveApprovalTone(ExecutionApprovalStatus status)
    {
        return status switch
        {
            ExecutionApprovalStatus.Approved => "success",
            ExecutionApprovalStatus.Rejected => "danger",
            _ => "warning"
        };
    }
}
