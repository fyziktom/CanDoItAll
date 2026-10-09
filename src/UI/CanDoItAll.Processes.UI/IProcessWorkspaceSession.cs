using CanDoItAll.Processes.Projections;
using Microsoft.AspNetCore.Components;

namespace CanDoItAll.Processes.UI;

public interface IProcessWorkspaceSession {
    bool AgentContextDisabled { get; }
    string AgentContextTooltip { get; }
    Task ApplyDefinitionSearchAsync();
    Task ApplyTemplateCatalogQueryAsync(ProcessTemplateCatalogQueryProjection query);
    bool CanStartAnotherLaunch { get; }
    Task ChangeManagerChatHistoryWindowAsync(ChangeEventArgs args);
    Task ChangeRuntimeHistoryWindowAsync(ChangeEventArgs args);
    Task ClearDefinitionSearchAsync();
    Task CloseEventDetailDialogAsync();
    Task CloseRunDetailDialogAsync();
    ProcessWorkspaceShellProjection CurrentShell { get; }
    ProcessDefinitionCatalogProjection DefinitionCatalog { get; }
    string DefinitionTooltip { get; }
    string DefinitionTotalText { get; }
    IReadOnlyList<ProcessWorkspaceDetailTabDescriptor> DetailTabs { get; }
    Task ExecuteCommandAsync(ProcessWorkspaceCommandProjection command);
    Task ExecuteDefinitionCanvasCommandAsync(ProcessDefinitionCanvasCommand command);
    Task ExecuteDefinitionEditorCommandAsync(ProcessDefinitionEditorCommandKind commandKind);
    Task ExecuteDefinitionRoleEditorCommandAsync(ProcessDefinitionRoleEditorCommand command);
    Task ExecuteDefinitionStepEditorCommandAsync(ProcessDefinitionStepEditorCommand command);
    Task ExecuteTemplateImportCommandAsync(ProcessTemplateImportCommand command);
    string FormatCurrency(decimal value);
    Task HandleDetailTabChangedAsync(int index);
    Task HandleSelectedRunViewChangedAsync(int index);
    string HeaderTitle { get; }
    Task LaunchSelectedDefinitionAsync();
    string LiveRunActiveText { get; }
    string LiveRunAttentionText { get; }
    string LiveRunTooltip { get; }
    Task LoadProcessGraphsHistoryAsync();
    string ManagerChatAgentSelectValue { get; }
    string ManagerChatManagerLabel { get; }
    bool ManagerChatReloadDisabled { get; }
    Task NextRuntimeEventPageAsync();
    void OpenAgentContext();
    Task OpenChildRunAsync(Guid runId);
    Task OpenEventDetailDialogAsync(ProcessTimelineEventProjection runtimeEvent);
    Task OpenManagerChatTabAsync();
    Task OpenRunDetailDialogAsync(ProcessLiveProcessSnapshot run);
    Task PreviousRuntimeEventPageAsync();
    string ProjectionStatusText { get; }
    string ProjectionTone { get; }
    string ProjectionTooltip { get; }
    Task RefreshAsync();
    Task ReloadManagerChatAsync();
    ProcessWorkspaceShellScope ResolveScope();
    string ScopeEyebrow { get; }
    Task SelectDefinitionAsync(ProcessDefinitionCatalogItemKey key);
    Task SelectDefinitionScopeAsync(ProcessDefinitionCatalogScopeKind scopeKind);
    Task SelectManagerChatAgentAsync(ChangeEventArgs args);
    Task SelectRuntimeRunAsync(ChangeEventArgs args);
    Task SelectRuntimeRunAsync(Guid runId);
    string? SelectedDefinitionKey { get; }
    Task StartNewProcessLaunchAsync();
    void UpdateDefinitionSearchText(ChangeEventArgs args);
    int ActiveDetailTabIndex { get; }
    string? AgentContextNotice { get; }
    string? CatalogCommandNotice { get; }
    ProcessDefinitionDraft DefinitionDraft { get; }
    string? EditorCommandNotice { get; }
    string? ErrorMessage { get; }
    ProcessTimelineEventProjection? EventDetail { get; }
    bool EventDetailDialogOpen { get; }
    bool IsBusy { get; }
    bool HasManagerChatAgent { get; }
    IReadOnlyList<ProcessManagerAgentOption> ManagerChatAgents { get; }
    string? ManagerChatErrorMessage { get; }
    ProcessRuntimeHistoryWindow ManagerChatHistoryWindow { get; }
    bool ManagerChatIsLoading { get; }
    string? PendingLaunchStorageKey { get; }
    string PendingSearchText { get; }
    bool ProcessGraphsHistoryLoaded { get; }
    ProcessRoleEditorState RoleEditorState { get; }
    ProcessTemplateBrowserState TemplateBrowserState { get; }
    bool RunDetailDialogOpen { get; }
    ProcessLiveProcessSnapshot? RunDetailSnapshot { get; }
    int RuntimeEventPage { get; }
    ProcessRuntimeHistoryWindow RuntimeHistoryWindow { get; }
    ProcessDefinitionCatalogScopeKind SelectedDefinitionScope { get; }
    int SelectedRunViewIndex { get; }
    Guid? SelectedRuntimeRunId { get; }
    ProcessWorkspaceShellProjection? Shell { get; }
    ProcessStepEditorState StepEditorState { get; }
}

public enum ProcessWorkspaceDetailTabKey {
        Definition,
        Roles,
        Steps,
        Runs,
        Graphs,
        Analytics,
        Exchange,
        ManagerChat
    }


public sealed record ProcessWorkspaceDetailTabDescriptor(
        ProcessWorkspaceDetailTabKey Key,
        string Text,
        string Icon,
        string Description,
        string? BadgeText);

public sealed record ProcessManagerAgentOption(Guid Id, string Name);
