using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using Microsoft.AspNetCore.Components;

namespace CanDoItAll.Modules.AgentFramework.Pages.Components;

public partial class AgentGovernancePanel
{
    [Parameter]
    public Guid? PreferredAgentId { get; set; }

    [Parameter]
    public EventCallback<AgentDefinition?> SelectedAgentChanged { get; set; }

    [Parameter]
    public EventCallback<AgentChatContextAccessState> ContextAccessStateChanged { get; set; }

    [Inject]
    public IAgentFrameworkWorkspaceService WorkspaceService { get; set; } = default!;

    private IReadOnlyList<AgentDefinition> agents = [];
    private IReadOnlyList<ExecutionRunRecord> runs = [];
    private ExecutionRunDetail? selectedDetail;
    private Guid? selectedAgentId;
    private Guid? selectedRunId;
    private bool isBusy;
    private long agentSelectionGeneration;
    private AgentChatContextAccessState? publishedAccessState;
    private Guid? appliedPreferredAgentId;
    private bool preferredAgentApplied;

    protected override async Task OnInitializedAsync()
    {
        await LoadAsync();
    }

    protected override async Task OnParametersSetAsync()
    {
        if (preferredAgentApplied && appliedPreferredAgentId == PreferredAgentId)
        {
            return;
        }

        preferredAgentApplied = true;
        appliedPreferredAgentId = PreferredAgentId;
        if (PreferredAgentId.HasValue &&
            agents.All(item => item.Id != PreferredAgentId.Value))
        {
            selectedAgentId = null;
            runs = [];
            selectedRunId = null;
            selectedDetail = null;
            await NotifySelectedAgentChangedAsync();
            await PublishAccessStateAsync(AgentChatContextAccessState.Failed);
            return;
        }

        selectedAgentId = PreferredAgentId;
        await RefreshRunsAsync();
        await NotifySelectedAgentChangedAsync();
    }

    private async Task LoadAsync()
    {
        isBusy = true;
        await PublishAccessStateAsync(AgentChatContextAccessState.Loading);
        try
        {
            agents = await WorkspaceService.ListAgentsAsync(includeTemplates: false);
            preferredAgentApplied = true;
            appliedPreferredAgentId = PreferredAgentId;
            if (PreferredAgentId.HasValue &&
                agents.Any(item => item.Id == PreferredAgentId.Value))
            {
                selectedAgentId = PreferredAgentId.Value;
            }
            else if (PreferredAgentId.HasValue)
            {
                runs = [];
                selectedRunId = null;
                selectedDetail = null;
                await NotifySelectedAgentChangedAsync();
                await PublishAccessStateAsync(AgentChatContextAccessState.Failed);
                return;
            }

            await RefreshRunsAsync();
            await NotifySelectedAgentChangedAsync();
        }
        catch
        {
            await PublishAccessStateAsync(AgentChatContextAccessState.Failed);
            throw;
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
        await NotifySelectedAgentChangedAsync();
    }

    private Task NotifySelectedAgentChangedAsync()
        => SelectedAgentChanged.InvokeAsync(
            selectedAgentId.HasValue
                ? agents.FirstOrDefault(item => item.Id == selectedAgentId.Value)
                : null);

    private async Task PublishAccessStateAsync(AgentChatContextAccessState state)
    {
        if (publishedAccessState == state)
        {
            return;
        }

        publishedAccessState = state;
        await ContextAccessStateChanged.InvokeAsync(state);
    }

    private async Task RefreshAsync()
    {
        await LoadAsync();
    }

    private async Task RefreshRunsAsync()
    {
        var generation = Interlocked.Increment(ref agentSelectionGeneration);
        var requestedAgentId = selectedAgentId;
        await PublishAccessStateAsync(AgentChatContextAccessState.Loading);
        IReadOnlyList<ExecutionRunRecord> loadedRuns;
        try
        {
            loadedRuns = await WorkspaceService.ListExecutionRunsAsync(
                new ExecutionRunQuery(
                    AgentId: requestedAgentId,
                    Take: 30));
        }
        catch
        {
            if (IsCurrentAgentSelection(generation, requestedAgentId))
            {
                await PublishAccessStateAsync(AgentChatContextAccessState.Failed);
            }

            throw;
        }

        if (!IsCurrentAgentSelection(generation, requestedAgentId))
        {
            return;
        }

        if (loadedRuns.Count == 0)
        {
            runs = loadedRuns;
            selectedRunId = null;
            selectedDetail = null;
            await PublishAccessStateAsync(AgentChatContextAccessState.Ready);
            return;
        }

        var runId = selectedRunId is { } currentRunId &&
                    loadedRuns.Any(item => item.Id == currentRunId)
            ? currentRunId
            : loadedRuns[0].Id;
        ExecutionRunDetail loadedDetail;
        try
        {
            loadedDetail = await WorkspaceService.GetExecutionRunDetailAsync(runId);
        }
        catch
        {
            if (IsCurrentAgentSelection(generation, requestedAgentId))
            {
                await PublishAccessStateAsync(AgentChatContextAccessState.Failed);
            }

            throw;
        }

        if (!IsCurrentAgentSelection(generation, requestedAgentId))
        {
            return;
        }

        runs = loadedRuns;
        selectedRunId = runId;
        selectedDetail = loadedDetail;
        await PublishAccessStateAsync(AgentChatContextAccessState.Ready);
    }

    private bool IsCurrentAgentSelection(long generation, Guid? requestedAgentId)
        => generation == Volatile.Read(ref agentSelectionGeneration) &&
           selectedAgentId == requestedAgentId;

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
