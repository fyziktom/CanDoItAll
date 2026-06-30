using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Components.BaseLib;
using Microsoft.AspNetCore.Components;

namespace CanDoItAll.Modules.AgentFramework.Pages.Components;

public partial class AgentDiagnosticsPanel
{
    [Inject]
    public IAgentFrameworkWorkspaceService WorkspaceService { get; set; } = default!;

    [Inject]
    public NotificationService NotificationService { get; set; } = default!;

    private SandboxDashboardSnapshot dashboard = new(
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        ExecutionBoundaryDescriptor.Unknown);
    private IReadOnlyList<AgentDefinition> agents = [];
    private IReadOnlyList<ExecutionRunRecord> recentRuns = [];
    private IReadOnlyList<ExecutionRunRecord> recentFailures = [];
    private bool isBusy;

    protected override async Task OnInitializedAsync()
    {
        await LoadAsync();
    }

    private async Task RefreshAsync()
    {
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        isBusy = true;
        try
        {
            dashboard = await WorkspaceService.GetDashboardAsync();
            agents = await WorkspaceService.ListAgentsAsync(includeTemplates: false);
            recentRuns = await WorkspaceService.ListExecutionRunsAsync(new ExecutionRunQuery(Take: 12));
            recentFailures = recentRuns
                .Where(item => item.Outcome == RunOutcome.Failed || item.State == ExecutionState.Failed)
                .ToList();
            SetMessage("Ready", "success", "Diagnostics refreshed from the integrated runtime.");
        }
        catch (Exception exception)
        {
            SetMessage("Attention", "danger", exception.Message);
        }
        finally
        {
            isBusy = false;
        }
    }

    private void SetMessage(string label, string tone, string value)
    {
        switch (tone)
        {
            case "success":
                NotificationService.Success(label, value);
                break;
            case "warning":
                NotificationService.Warning(label, value);
                break;
            case "danger":
                NotificationService.Error(label, value);
                break;
            default:
                NotificationService.Info(label, value);
                break;
        }
    }

    private string ResolveRunOwner(ExecutionRunRecord run)
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
}
