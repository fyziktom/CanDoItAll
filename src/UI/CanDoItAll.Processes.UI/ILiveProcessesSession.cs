using CanDoItAll.Components.Charts;
using CanDoItAll.Processes.Projections;
using CanDoItAll.Processes.Abstractions;
using Microsoft.AspNetCore.Components;

namespace CanDoItAll.Processes.UI;

public interface ILiveProcessesSession {
    IReadOnlyList<ProcessLiveProcessSnapshot> ActivityCardRuns { get; }
    IReadOnlyList<ProcessLiveProcessSnapshot> AttentionRuns { get; }
    string BuildAttentionSummary(ProcessLiveProcessSnapshot run);
    string BuildRunCardSummary(ProcessLiveProcessSnapshot run);
    Task ChangeRuntimeHistoryWindowAsync(ChangeEventArgs args);
    Task ChangeStatusFilterAsync(ChangeEventArgs args);
    Task CloseAgentDetailDialogAsync();
    Task CloseRunDetailDialogAsync();
    string DefinitionCountText { get; }
    Task ExecuteOperatorActionAsync(ProcessRuntimeOperatorActionProjection action);
    Task ForceRefreshAsync();
    string FormatCurrency(decimal value);
    string GetOperatorActionNote(ProcessRuntimeOperatorActionProjection action);
    Task HandleTabChangedAsync(int index);
    int HiddenRelatedRunCount { get; }
    Task HideRunGroupAsync(ProcessLiveProcessSnapshot run);
    bool IsMockProjection { get; }
    bool IsOperatorActionBusy(ProcessRuntimeOperatorActionProjection action);
    string LastEventText { get; }
    bool LaunchStarted { get; }
    Task NextRuntimeEventPageAsync();
    Task OpenAgentDetailDialogAsync(ProcessRuntimeActiveAgentProjection agent);
    Task OpenChildRunAsync(Guid runId);
    Task OpenDefinitionsAsync();
    Task OpenProcessControlAsync(Guid runId);
    Task OpenRunDetailDialogAsync(ProcessLiveProcessSnapshot run);
    Task OpenRunFilesAsync(Guid runId);
    string PageTitleText { get; }
    Task PreviousRuntimeEventPageAsync();
    string ProjectionStatusText { get; }
    string ProjectionTone { get; }
    string ProjectionTooltip { get; }
    IReadOnlyList<ProcessRuntimeActiveAgentProjection> ResolveActiveAgents(Guid runId);
    string ResolveAgentStatusTone(ProcessRuntimeActiveAgentProjection agent);
    string ResolveCurrentStepText(ProcessLiveProcessSnapshot run);
    IReadOnlyList<ProcessManagerMessageProjection> ResolveManagerMessages(ProcessRunId runId);
    string ResolveManagerSummary(ProcessLiveProcessSnapshot run);
    ProcessRuntimeActiveAgentProjection? ResolvePrimaryAgent(IReadOnlyList<ProcessRuntimeActiveAgentProjection> activeAgents);
    ProcessRuntimeOperatorActionProjection? ResolvePrimaryOperatorAction(ProcessLiveProcessSnapshot run);
    Guid? RunIdQuery { get; }
    CdaChartOptions RuntimeCostChartOptions { get; }
    string RuntimeRunSelectValue { get; }
    Task SelectRuntimeRunAsync(ChangeEventArgs args);
    Task SelectRuntimeRunAsync(Guid runId);
    IReadOnlyList<ProcessLiveProcessSnapshot> SelectableRuns { get; }
    void SetOperatorActionNote(ProcessRuntimeOperatorActionProjection action, string? value);
    Task ShowHiddenRunGroupsAsync();
    string StatusFilterValue { get; }
    IReadOnlyList<ToolFamilySummary> ToolFamilySummaries { get; }
    IReadOnlyList<ProcessRuntimeActiveAgentProjection> VisibleActiveAgents { get; }
    IReadOnlyList<ProcessLiveProcessSnapshot> VisibleRuns { get; }
    ProcessRuntimeActiveAgentProjection? AgentDetailAgent { get; }
    bool AgentDetailDialogOpen { get; }
    string? ErrorMessage { get; }
    bool IsLoading { get; }
    string? OperatorActionError { get; }
    string? OperatorActionMessage { get; }
    bool RunDetailDialogOpen { get; }
    ProcessLiveProcessSnapshot? RunDetailSnapshot { get; }
    int RuntimeEventPage { get; }
    ProcessRuntimeHistoryWindow RuntimeHistoryWindow { get; }
    Guid? SelectedRuntimeRunId { get; }
    int SelectedTabIndex { get; }
    ProcessWorkspaceShellProjection? Shell { get; }
}

public enum LiveProcessToolFamily {
        Browser,
        DotNet,
        ProjectStructure,
        Workspace,
        ProcessRuntime,
        AgentRuntime,
        Storage,
        Other
    }


public sealed record RunDisplayModel(
        string ProcessName,
        string ShortProcessName,
        string RunLabel,
        string Title,
        bool IsProcessNameTruncated);


public sealed record LiveRunProgressModel(
        int Percent,
        string Label,
        string Tone,
        string CssClass,
        string Tooltip);


public sealed record ToolFamilySummary(
        LiveProcessToolFamily Family,
        string Label,
        string Icon,
        string Tone,
        string CssClass,
        int ToolCount,
        int CallCount,
        DateTimeOffset LastUsedAtUtc,
        IReadOnlyList<ProcessRuntimeToolUsageProjection> Tools);

