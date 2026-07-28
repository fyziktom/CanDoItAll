using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Components;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Voice;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Modules.Prompts;
using CanDoItAll.Modules.Prompts.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace CanDoItAll.Modules.AgentFramework.Pages.Components;

public partial class AgentChatPanel : IAsyncDisposable
{
    private const string AgentThreadsHelpText =
        "Search and select threads for the active technical agent. Use Switch Agent when you need another agent's thread list.";
    private const string UpdatingWorkspaceContextRunState = "Updating current workspace context...";

    private readonly object voiceOwnerGate = new();
    private CancellationTokenSource voiceOperationCancellation = new();
    private string voiceOwnerId = Guid.NewGuid().ToString("N");
    private long voiceOwnerGeneration;
    private bool hasBrowserVoiceOwner;

    [Parameter]
    public Guid? PreferredAgentId { get; set; }

    [Parameter]
    public AgentDefinition? PreferredAgent { get; set; }

    [Parameter]
    public EventCallback<AgentDefinition?> SelectedAgentChanged { get; set; }

    [Parameter]
    public EventCallback<AgentChatContextAccessState> ContextAccessStateChanged { get; set; }

    [Parameter]
    public Guid? PreferredSessionId { get; set; }

    [Parameter]
    public AgentChatHandleId? ActiveChatHandleId { get; set; }

    [Parameter]
    public ActiveAgentChatRunState PersistedActiveChatRunState { get; set; }

    [Parameter]
    public AgentChatPanelDisplayMode DisplayMode { get; set; }

    [Inject]
    public IAgentFrameworkWorkspaceService WorkspaceService { get; set; } = default!;

    [Inject]
    public IAgentVoiceService VoiceService { get; set; } = default!;

    [Inject]
    public IAgentChatAttachmentStagingService AttachmentStagingService { get; set; } = default!;

    [Inject]
    public IFloatingAgentChatCoordinator FloatingChatCoordinator { get; set; } = default!;

    [Inject]
    public IAgentChatExecutionOrchestrator ChatExecutionOrchestrator { get; set; } = default!;

    [Inject]
    public IJSRuntime JsRuntime { get; set; } = default!;

    [Inject]
    public DialogService DialogService { get; set; } = default!;

    [Inject]
    public NotificationService NotificationService { get; set; } = default!;

    [Inject]
    public IPromptGalleryService PromptGallery { get; set; } = default!;

    [Inject]
    public ILogger<AgentChatPanel> Logger { get; set; } = default!;

    private IReadOnlyList<AgentDefinition> agents = [];
    private IReadOnlyList<ProviderProfile> providers = [];
    private IReadOnlyCollection<Guid> privateAgentIds = [];
    private ChatAgentWorkspaceSnapshot? workspace;
    private IReadOnlyList<ChatSessionSummaryRecord> filteredSessions = [];
    private IReadOnlyList<ExecutionLogEntry> executionLog = [];
    private IReadOnlyList<AgentRunMetric> metrics = [];
    private AgentDefinition? selectedAgent;
    private Guid? selectedAgentId;
    private Guid? selectedSessionId;
    private string draftPrompt = string.Empty;
    private string pendingUserPrompt = string.Empty;
    private IReadOnlyList<string> draftAttachmentPaths = [];
    private bool isBusy;
    private int composerKey;
    private string runStateText = string.Empty;
    private string runStateTone = "neutral";
    private string threadSearchText = string.Empty;
    private bool isVoiceModeEnabled;
    private bool isVoiceRecording;
    private bool isVoiceTranscribing;
    private bool isVoiceSpeaking;
    private bool isDisposed;
    private long workspaceLoadGeneration;
    private AgentChatContextAccessState? publishedAccessState;
    private string voiceStatusText = string.Empty;
    private string voiceStatusTone = "neutral";
    private string focusedAgentLoadError = string.Empty;
    private Task trackedChatOperation = Task.CompletedTask;
    private AgentExecutionActivityStreamId? activeActivityStreamId;
    private Guid? terminalWorkspaceRefreshRunId;
    private readonly HashSet<Guid> sessionsWithVoiceIdentifierOmissionNotice = [];
    private bool hasVoiceIdentifierOmissionNoticeWithoutSession;

    private IReadOnlyList<ChatSessionSummaryRecord> FilteredSessions => filteredSessions;

    private bool CanOpenRuntimeDetails
        => workspace?.SelectedRun is not null ||
           executionLog.Count > 0 ||
           metrics.Count > 0;

    private bool IsChatInteractionBusy
        => isBusy ||
           PersistedActiveChatRunState == ActiveAgentChatRunState.Running ||
           workspace?.SelectedRun?.State is
               ExecutionState.Preparing or
               ExecutionState.Running or
               ExecutionState.Persisting;

    private string? SelectedAgentProviderKind
        => selectedAgent?.ProviderProfileId is { } providerProfileId
            ? providers.FirstOrDefault(provider => provider.Id == providerProfileId)?.Kind.ToString()
            : null;

    private bool BlocksNewMessage
        => isBusy ||
           !trackedChatOperation.IsCompleted ||
           PersistedActiveChatRunState != ActiveAgentChatRunState.Idle ||
           workspace?.SelectedRun?.State is
               ExecutionState.Preparing or
               ExecutionState.Running or
               ExecutionState.WaitingOnTool or
               ExecutionState.Persisting;

    private AgentVoiceAccessSettings SelectedAgentVoiceAccess
        => selectedAgent is null
            ? new AgentVoiceAccessSettings()
            : AgentVoiceAccessMetadata.Read(selectedAgent.ConfigurationJson);

    private bool CanUseSelectedAgentVoiceMode
        => SelectedAgentVoiceAccess.CanUseVoiceMode;

    private bool IsFocusedFloating
        => DisplayMode == AgentChatPanelDisplayMode.FocusedFloating;

    private string ChatGridColumnTemplate
        => IsFocusedFloating
            ? "minmax(0,1fr)"
            : "minmax(18rem,0.58fr) minmax(0,1.8fr)";

    protected override async Task OnInitializedAsync()
    {
        WorkspaceService.ExecutionUpdated += HandleExecutionUpdated;
        if (TryUsePreferredFocusedAgent())
        {
            await LoadWorkspaceAsync(PreferredAgentId!.Value, PreferredSessionId);
            return;
        }

        await LoadAsync();
    }

    protected override async Task OnParametersSetAsync()
    {
        if (IsFocusedFloating)
        {
            TryUsePreferredFocusedAgent();
            if (!PreferredAgentId.HasValue ||
                agents.All(item => item.Id != PreferredAgentId.Value))
            {
                await ResetWorkspaceAsync();
                focusedAgentLoadError = "The requested focused agent is not available.";
                return;
            }

            focusedAgentLoadError = string.Empty;
            if (PreferredAgentId != selectedAgentId ||
                PreferredSessionId.HasValue && PreferredSessionId != selectedSessionId)
            {
                await LoadWorkspaceAsync(PreferredAgentId.Value, PreferredSessionId);
            }

            return;
        }

        if (PreferredAgentId.HasValue &&
            agents.All(item => item.Id != PreferredAgentId.Value))
        {
            await ResetWorkspaceAsync();
            return;
        }

        if (PreferredAgentId.HasValue &&
            PreferredAgentId != selectedAgentId)
        {
            await SelectAgentAsync(PreferredAgentId.Value);
        }
    }

    private async Task LoadAsync()
    {
        agents = await WorkspaceService.ListAgentsAsync(includeTemplates: false);
        if (agents.Count == 0)
        {
            await ResetWorkspaceAsync();
            focusedAgentLoadError = IsFocusedFloating
                ? "The requested focused agent is not available."
                : string.Empty;
            return;
        }

        if (IsFocusedFloating)
        {
            if (!PreferredAgentId.HasValue ||
                agents.All(item => item.Id != PreferredAgentId.Value))
            {
                await ResetWorkspaceAsync();
                focusedAgentLoadError = "The requested focused agent is not available.";
                return;
            }

            focusedAgentLoadError = string.Empty;
            await LoadWorkspaceAsync(PreferredAgentId.Value, PreferredSessionId);
            return;
        }

        focusedAgentLoadError = string.Empty;
        if (PreferredAgentId.HasValue &&
            agents.All(item => item.Id != PreferredAgentId.Value))
        {
            await ResetWorkspaceAsync();
            return;
        }

        var initialAgentId = PreferredAgentId.HasValue &&
                             agents.Any(item => item.Id == PreferredAgentId.Value)
            ? PreferredAgentId.Value
            : selectedAgentId is { } currentAgentId &&
              agents.Any(item => item.Id == currentAgentId)
                ? currentAgentId
                : agents[0].Id;

        await LoadWorkspaceAsync(initialAgentId, selectedSessionId);
    }

    private async Task ResetWorkspaceAsync()
    {
        Interlocked.Increment(ref workspaceLoadGeneration);
        var hadSelectedAgent = selectedAgentId.HasValue;
        await ResetVoiceOwnerAsync();
        workspace = null;
        filteredSessions = [];
        selectedAgent = null;
        selectedAgentId = null;
        selectedSessionId = null;
        activeActivityStreamId = null;
        executionLog = [];
        metrics = [];
        if (hadSelectedAgent)
        {
            await SelectedAgentChanged.InvokeAsync(null);
        }

        await PublishAccessStateAsync(AgentChatContextAccessState.Failed);
    }

    private string ResolveShellClass()
    {
        return IsFocusedFloating
            ? "agents-chat-panel-shell agents-chat-panel-shell--focused-floating"
            : "agents-chat-panel-shell";
    }

    private async Task RefreshAsync()
    {
        await LoadAsync();
    }

    private async Task SelectAgentAsync(Guid agentId)
    {
        threadSearchText = string.Empty;
        await LoadWorkspaceAsync(agentId, preferredSessionId: null);
    }

    private async Task SelectSessionAsync(Guid sessionId)
    {
        if (!selectedAgentId.HasValue)
        {
            return;
        }

        await LoadWorkspaceAsync(selectedAgentId.Value, sessionId);
    }

    private async Task CreateThreadAsync()
    {
        if (!selectedAgentId.HasValue)
        {
            return;
        }

        isBusy = true;
        try
        {
            if (ActiveChatHandleId.HasValue)
            {
                await FloatingChatCoordinator.StartNewChatAsync(selectedAgentId.Value);
                return;
            }

            var session = await WorkspaceService.GetOrCreateChatSessionAsync(selectedAgentId.Value);
            await LoadWorkspaceAsync(selectedAgentId.Value, session.Id);
            SetMessage("Ready", "success", "New thread created.");
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

    private Task HandleDraftPromptChangedAsync(string value)
    {
        draftPrompt = value;
        return Task.CompletedTask;
    }

    private async Task HandlePromptGallerySelectionAsync(PromptGallerySelection selection)
    {
        var compatibilityResult = await PromptGallery.EvaluateCompatibilityAsync(
            selection.ArtifactId,
            new PromptGalleryConsumerContext(
                PromptGalleryConsumer.Chat,
                PromptGalleryCompatibilityPurpose.Selection,
                Provider: SelectedAgentProviderKind,
                Model: selectedAgent?.Model));
        if (compatibilityResult.IsFailure || compatibilityResult.Value is null)
        {
            NotificationService.Warning(
                "Prompt compatibility unavailable",
                DescribePromptGalleryErrors(compatibilityResult.Errors));
            return;
        }

        var compatibility = compatibilityResult.Value;
        var decision = PromptCompatibilityWarningDecision.InsertAnyway;
        if (!compatibility.CanUse || compatibility.HasVisibleWarnings)
        {
            var dialogResult = await DialogService.OpenAsync<PromptCompatibilityWarningDialog>(
                "Prompt compatibility",
                new Dictionary<string, object?>
                {
                    [nameof(PromptCompatibilityWarningDialog.Selection)] = selection,
                    [nameof(PromptCompatibilityWarningDialog.Compatibility)] = compatibility
                },
                new DialogOptions
                {
                    Eyebrow = "Provider and model check",
                    Subtitle = "Review declared compatibility before inserting this prompt.",
                    Size = ModalSize.Medium,
                    DenseChrome = true,
                    TestId = "prompt-gallery-chat-compatibility-dialog",
                    AriaLabel = "Prompt compatibility warning"
                });
            if (dialogResult is not PromptCompatibilityWarningDecision selectedDecision ||
                selectedDecision == PromptCompatibilityWarningDecision.Cancel)
            {
                return;
            }

            decision = selectedDecision;
        }

        if (decision == PromptCompatibilityWarningDecision.InsertAndSuppress)
        {
            foreach (var issue in compatibility.Issues.Where(issue =>
                         !issue.IsSuppressed && issue.IsSuppressible))
            {
                var suppression = await PromptGallery.SetWarningSuppressionAsync(
                    selection.ArtifactId,
                    PromptGalleryConsumer.Chat,
                    issue.Code,
                    suppressed: true);
                if (suppression.IsFailure)
                {
                    NotificationService.Warning(
                        "Warning preference was not saved",
                        DescribePromptGalleryErrors(suppression.Errors));
                }
            }
        }

        var content = selection.Content.Trim();
        if (content.Length == 0)
        {
            NotificationService.Warning("Prompt is empty", "The selected Gallery item has no content to insert.");
            return;
        }

        draftPrompt = string.IsNullOrWhiteSpace(draftPrompt)
            ? content
            : $"{draftPrompt.TrimEnd()}{Environment.NewLine}{Environment.NewLine}{content}";
        composerKey++;
    }

    private static string DescribePromptGalleryErrors(IReadOnlyList<CanDoItAll.SharedKernel.Error> errors)
        => errors.Count == 0
            ? "The Prompt Gallery did not return a result."
            : string.Join(" ", errors.Select(error => error.Message));

    private Task HandleThreadSearchChangedAsync(string? value)
    {
        threadSearchText = value ?? string.Empty;
        RefreshFilteredSessions();
        return Task.CompletedTask;
    }

    private Task SendMessageAsync()
    {
        if (!selectedAgentId.HasValue)
        {
            SetMessage("Heads up", "warning", "Select a technical agent before sending a prompt.");
            return Task.CompletedTask;
        }

        if (BlocksNewMessage)
        {
            SetMessage(
                "Chat is still active",
                "warning",
                PersistedActiveChatRunState == ActiveAgentChatRunState.AwaitingApproval ||
                workspace?.SelectedRun?.State == ExecutionState.WaitingOnTool
                    ? "Resolve the pending approval before sending another prompt."
                    : "Wait for the current execution to finish before sending another prompt.");
            SynchronizeActiveChatRunState();
            return Task.CompletedTask;
        }

        var prompt = BuildPromptWithAttachments();
        if (string.IsNullOrWhiteSpace(prompt))
        {
            SetMessage("Heads up", "warning", "Enter a prompt before sending it.");
            return Task.CompletedTask;
        }

        var executionAgentId = selectedAgentId.Value;
        var executionSessionId = selectedSessionId;
        var executionHandleId = ActiveChatHandleId;
        var executionAttachmentPaths = draftAttachmentPaths;
        if (!TryBeginChatOperation(executionHandleId))
        {
            SetMessage("Chat is still active", "warning", "Wait for the current execution to finish before sending another prompt.");
            return Task.CompletedTask;
        }

        pendingUserPrompt = draftPrompt;
        var previousDraft = draftPrompt;
        draftPrompt = string.Empty;
        composerKey++;
        runStateText = UpdatingWorkspaceContextRunState;
        runStateTone = "info";
        StateHasChanged();
        trackedChatOperation = RunMessageOperationAsync(
            executionAgentId,
            executionSessionId,
            executionHandleId,
            prompt,
            executionAttachmentPaths,
            previousDraft);
        return Task.CompletedTask;
    }

    private async Task RunMessageOperationAsync(
        Guid executionAgentId,
        Guid? executionSessionId,
        AgentChatHandleId? executionHandleId,
        string prompt,
        IReadOnlyList<string> attachmentPaths,
        string previousDraft)
    {
        var executionCompleted = false;
        try
        {
            var operation = ChatExecutionOrchestrator.StartSendMessage(
                executionAgentId,
                executionSessionId,
                prompt,
                attachmentPaths);
            activeActivityStreamId = operation.StreamId;
            await InvokeAsync(StateHasChanged);
            var result = await operation.Completion;
            executionCompleted = true;
            if (isDisposed || selectedAgentId != executionAgentId)
            {
                return;
            }

            draftAttachmentPaths = [];
            await LoadWorkspaceAsync(executionAgentId, result.ChatSessionId);
            if (isDisposed || selectedAgentId != executionAgentId)
            {
                return;
            }

            SetMessage("Ready", "success", "Prompt sent through the integrated runtime.");
            if (isVoiceModeEnabled && !string.IsNullOrWhiteSpace(result.AssistantMessage.Content))
            {
                await SpeakTextAsync(result.AssistantMessage.Content);
            }
        }
        catch (Exception exception) when (isDisposed)
        {
            LogDetachedOperationFailure(exception, executionAgentId, executionSessionId, executionHandleId, "send");
        }
        catch (Exception exception)
        {
            if (selectedAgentId == executionAgentId)
            {
                if (!executionCompleted)
                {
                    draftPrompt = previousDraft;
                    composerKey++;
                }

                ResolveRunState();
                SetMessage(
                    executionCompleted ? "Refresh needed" : "Attention",
                    executionCompleted ? "warning" : "danger",
                    executionCompleted
                        ? $"The prompt completed, but the latest thread state could not be loaded: {exception.Message}"
                        : exception.Message);
            }
        }
        finally
        {
            pendingUserPrompt = string.Empty;
            isBusy = false;
            if (runStateText == UpdatingWorkspaceContextRunState)
            {
                ResolveRunState();
            }

            await FinishChatOperationAsync(executionHandleId, executionAgentId, executionSessionId, "send");
        }
    }

    private Task HandleApprovalDecisionAsync(bool approved)
        => StartApprovalOperation(approved, autoApprovePendingToolCalls: false);

    private Task ApproveConversationAsync()
        => StartApprovalOperation(approved: true, autoApprovePendingToolCalls: true);

    private Task StartApprovalOperation(bool approved, bool autoApprovePendingToolCalls)
    {
        if (!selectedAgentId.HasValue || !selectedSessionId.HasValue)
        {
            return Task.CompletedTask;
        }

        var executionAgentId = selectedAgentId.Value;
        var executionSessionId = selectedSessionId.Value;
        var executionHandleId = ActiveChatHandleId;
        if (!TryBeginChatOperation(executionHandleId))
        {
            return Task.CompletedTask;
        }

        trackedChatOperation = RunApprovalOperationAsync(
            executionAgentId,
            executionSessionId,
            executionHandleId,
            approved,
            autoApprovePendingToolCalls);
        return Task.CompletedTask;
    }

    private async Task RunApprovalOperationAsync(
        Guid executionAgentId,
        Guid executionSessionId,
        AgentChatHandleId? executionHandleId,
        bool approved,
        bool autoApprovePendingToolCalls)
    {
        await Task.Yield();
        var executionCompleted = false;
        try
        {
            var operation = ChatExecutionOrchestrator.StartApprovalContinuation(
                executionAgentId,
                executionSessionId,
                approved,
                autoApprovePendingToolCalls);
            activeActivityStreamId = operation.StreamId;
            await InvokeAsync(StateHasChanged);
            await operation.Completion;
            executionCompleted = true;
            if (isDisposed ||
                selectedAgentId != executionAgentId ||
                selectedSessionId != executionSessionId)
            {
                return;
            }

            await LoadWorkspaceAsync(executionAgentId, executionSessionId);
            if (isDisposed ||
                selectedAgentId != executionAgentId ||
                selectedSessionId != executionSessionId)
            {
                return;
            }

            SetMessage(
                approved ? "Ready" : "Heads up",
                approved ? "success" : "warning",
                approved
                    ? autoApprovePendingToolCalls
                        ? "Approval resumed the run and enabled remaining approvals for the active execution."
                        : "Approval resumed the run."
                    : "Approval was rejected and the thread was refreshed.");
        }
        catch (Exception exception) when (isDisposed)
        {
            LogDetachedOperationFailure(exception, executionAgentId, executionSessionId, executionHandleId, "approval");
        }
        catch (Exception exception)
        {
            if (selectedAgentId == executionAgentId &&
                selectedSessionId == executionSessionId)
            {
                SetMessage(
                    executionCompleted ? "Refresh needed" : "Attention",
                    executionCompleted ? "warning" : "danger",
                    executionCompleted
                        ? $"The approval completed, but the latest thread state could not be loaded: {exception.Message}"
                        : exception.Message);
            }
        }
        finally
        {
            isBusy = false;
            await FinishChatOperationAsync(executionHandleId, executionAgentId, executionSessionId, "approval");
        }
    }

    private bool TryBeginChatOperation(AgentChatHandleId? handleId)
    {
        if (isBusy || !trackedChatOperation.IsCompleted)
        {
            return false;
        }

        if (handleId.HasValue && !FloatingChatCoordinator.TryBeginOperation(handleId.Value))
        {
            return false;
        }

        isBusy = true;
        return true;
    }

    private async Task FinishChatOperationAsync(
        AgentChatHandleId? handleId,
        Guid agentId,
        Guid? sessionId,
        string operationKind)
    {
        try
        {
            ReconcileActiveChatRunState(handleId);
            if (!isDisposed)
            {
                SynchronizeActiveChatRunState();
                await InvokeAsync(StateHasChanged);
            }
        }
        catch (Exception exception)
        {
            Logger.LogError(
                exception,
                "Detached agent chat operation cleanup failed. Operation={OperationKind} AgentId={AgentId} ChatSessionId={ChatSessionId} HandleId={HandleId} FailureType={FailureType}.",
                operationKind,
                agentId,
                sessionId,
                handleId?.Value,
                exception.GetType().Name);
        }
    }

    private void LogDetachedOperationFailure(
        Exception exception,
        Guid agentId,
        Guid? sessionId,
        AgentChatHandleId? handleId,
        string operationKind)
    {
        Logger.LogWarning(
            exception,
            "Detached agent chat operation finished after its panel was disposed. Operation={OperationKind} AgentId={AgentId} ChatSessionId={ChatSessionId} HandleId={HandleId} FailureType={FailureType}.",
            operationKind,
            agentId,
            sessionId,
            handleId?.Value,
            exception.GetType().Name);
    }

    private async Task StageAttachmentsAsync()
    {
        if (workspace?.SelectedRun is null)
        {
            SetMessage("Heads up", "warning", "Run a prompt first so the thread has execution artifacts to stage.");
            return;
        }

        var detail = await WorkspaceService.GetExecutionRunDetailAsync(workspace.SelectedRun.Id);
        var artifactPaths = detail.Artifacts
            .Select(item => item.RelativePath)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (artifactPaths.Count == 0)
        {
            SetMessage("Heads up", "warning", "The selected run does not have persisted artifacts yet.");
            return;
        }

        draftAttachmentPaths = artifactPaths;
        SetMessage("Ready", "success", $"Staged {artifactPaths.Count} artifact path(s) for the next prompt.");
    }

    private async Task StageUploadedAttachmentFilesAsync(InputFileChangeEventArgs args)
    {
        const int maxFiles = 8;

        isBusy = true;
        try
        {
            var stagedPaths = new List<string>(draftAttachmentPaths);
            foreach (var file in args.GetMultipleFiles(maxFiles))
            {
                await using var stream = file.OpenReadStream(AgentChatAttachmentStagingService.MaxImageAttachmentBytes);
                var staged = await AttachmentStagingService.StageImageAsync(
                    file.Name,
                    file.ContentType,
                    file.Size,
                    stream);
                stagedPaths.Add(staged.RelativePath);
            }

            draftAttachmentPaths = stagedPaths
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            SetMessage("Ready", "success", $"Staged {draftAttachmentPaths.Count} attachment path(s) for the next prompt.");
        }
        catch (Exception exception)
        {
            SetMessage("Attachment failed", "danger", exception.Message);
        }
        finally
        {
            isBusy = false;
        }
    }

    private async Task HandleSessionTitleChangedAsync(string title)
    {
        if (!selectedAgentId.HasValue || !selectedSessionId.HasValue)
        {
            return;
        }

        isBusy = true;
        try
        {
            var session = await WorkspaceService.RenameChatSessionAsync(
                selectedAgentId.Value,
                selectedSessionId.Value,
                title);
            await LoadWorkspaceAsync(selectedAgentId.Value, session.Id);
            SetMessage("Ready", "success", "Thread title updated.");
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

    private async Task LoadWorkspaceAsync(Guid agentId, Guid? preferredSessionId)
    {
        if (selectedAgentId != agentId ||
            selectedSessionId.HasValue &&
            preferredSessionId.HasValue &&
            selectedSessionId != preferredSessionId)
        {
            activeActivityStreamId = null;
        }

        var loadGeneration = Interlocked.Increment(ref workspaceLoadGeneration);
        await PublishAccessStateAsync(AgentChatContextAccessState.Loading);
        if (isDisposed || loadGeneration != Volatile.Read(ref workspaceLoadGeneration))
        {
            return;
        }

        try
        {
            await LoadWorkspaceCoreAsync(agentId, preferredSessionId, loadGeneration);
        }
        catch
        {
            if (!isDisposed && loadGeneration == Volatile.Read(ref workspaceLoadGeneration))
            {
                await PublishAccessStateAsync(AgentChatContextAccessState.Failed);
            }

            throw;
        }
    }

    private async Task LoadWorkspaceCoreAsync(
        Guid agentId,
        Guid? preferredSessionId,
        long loadGeneration)
    {
        var nextAgent = agents.FirstOrDefault(item => item.Id == agentId);
        var nextAgentVoiceAccess = nextAgent is null
            ? new AgentVoiceAccessSettings()
            : AgentVoiceAccessMetadata.Read(nextAgent.ConfigurationJson);
        var selectionChanged = selectedAgentId != agentId;
        var agentChanged = selectedAgentId is { } currentAgentId && currentAgentId != agentId;
        if (agentChanged ||
            !nextAgentVoiceAccess.CanUseVoiceMode && HasVoiceOwnerActivity())
        {
            await ResetVoiceOwnerAsync();
            if (isDisposed || loadGeneration != Volatile.Read(ref workspaceLoadGeneration))
            {
                return;
            }
        }

        var nextWorkspace = await WorkspaceService.GetChatAgentWorkspaceAsync(agentId, preferredSessionId);
        if (isDisposed || loadGeneration != Volatile.Read(ref workspaceLoadGeneration))
        {
            return;
        }

        IReadOnlyList<ExecutionLogEntry> nextExecutionLog;
        IReadOnlyList<AgentRunMetric> nextMetrics;
        if (nextWorkspace.SelectedRun is { } selectedRun)
        {
            var runDetail = await WorkspaceService.GetExecutionRunDetailAsync(selectedRun.Id);
            if (isDisposed || loadGeneration != Volatile.Read(ref workspaceLoadGeneration))
            {
                return;
            }

            nextExecutionLog = runDetail.ExecutionLog;
            nextMetrics = runDetail.Metrics;
        }
        else
        {
            nextExecutionLog = [];
            nextMetrics = [];
        }

        selectedAgentId = agentId;
        selectedAgent = nextAgent;
        workspace = nextWorkspace;
        selectedSessionId = nextWorkspace.SelectedSessionId;
        executionLog = nextExecutionLog;
        metrics = nextMetrics;
        RefreshFilteredSessions();
        ResolveRunState();
        SynchronizeActiveChatRunState();
        if (selectionChanged)
        {
            await SelectedAgentChanged.InvokeAsync(nextAgent);
        }

        if (!isDisposed && loadGeneration == Volatile.Read(ref workspaceLoadGeneration))
        {
            await PublishAccessStateAsync(AgentChatContextAccessState.Ready);
        }
    }

    private async Task PublishAccessStateAsync(AgentChatContextAccessState state)
    {
        if (publishedAccessState == state)
        {
            return;
        }

        publishedAccessState = state;
        await ContextAccessStateChanged.InvokeAsync(state);
    }

    private void RefreshFilteredSessions()
    {
        filteredSessions = workspace?.Sessions
            .Where(MatchesThreadSearch)
            .OrderByDescending(item => item.UpdatedAtUtc)
            .ToArray() ?? [];
    }

    private Task OpenAgentSwitchDialogAsync()
    {
        _ = HandleAgentSwitchDialogAsync();
        return Task.CompletedTask;
    }

    private async Task HandleAgentSwitchDialogAsync()
    {
        try
        {
            await RefreshAgentCatalogAsync();
            var result = await DialogService.OpenAsync<AgentSwitchDialog>(
                "Switch Agent",
                new Dictionary<string, object?>
                {
                    [nameof(AgentSwitchDialog.Agents)] = agents,
                    [nameof(AgentSwitchDialog.SelectedAgentId)] = selectedAgentId,
                    [nameof(AgentSwitchDialog.PrivateAgentIds)] = privateAgentIds,
                    [nameof(AgentSwitchDialog.FavoriteToggled)] =
                        (Func<AgentDefinition, Task<AgentDefinition>>)ToggleAgentFavoriteAsync
                },
                new DialogOptions
                {
                    Eyebrow = "Agent threads",
                    Subtitle = "Choose which technical agent owns the thread list.",
                    Size = ModalSize.Wide,
                    DenseChrome = true,
                    TestId = "agent-switch-dialog-modal",
                    AriaLabel = "Switch chat agent"
                });

            await RefreshAgentCatalogAsync();
            if (result is not Guid agentId || agentId == selectedAgentId)
            {
                return;
            }

            await InvokeAsync(async () =>
            {
                await SelectAgentAsync(agentId);
                StateHasChanged();
            });
        }
        catch (Exception exception)
        {
            await InvokeAsync(() =>
            {
                SetMessage("Attention", "danger", exception.Message);
                StateHasChanged();
            });
        }
    }

    private async Task RefreshAgentCatalogAsync()
    {
        var agentsTask = WorkspaceService.ListAgentsAsync(includeTemplates: false);
        var providersTask = WorkspaceService.ListProvidersAsync();
        agents = await agentsTask;
        providers = await providersTask;
        var privateProviderIds = providers
            .Where(provider => provider.IsPrivateProvider)
            .Select(provider => provider.Id)
            .ToHashSet();
        privateAgentIds = agents
            .Where(agent => agent.ProviderProfileId.HasValue && privateProviderIds.Contains(agent.ProviderProfileId.Value))
            .Select(agent => agent.Id)
            .ToHashSet();
        if (selectedAgentId is { } currentAgentId)
        {
            var refreshedAgent = agents.FirstOrDefault(item => item.Id == currentAgentId);
            var refreshedVoiceAccess = refreshedAgent is null
                ? new AgentVoiceAccessSettings()
                : AgentVoiceAccessMetadata.Read(refreshedAgent.ConfigurationJson);
            if (!refreshedVoiceAccess.CanUseVoiceMode && HasVoiceOwnerActivity())
            {
                await ResetVoiceOwnerAsync();
                if (isDisposed)
                {
                    return;
                }
            }

            selectedAgent = refreshedAgent;
        }
    }

    private bool TryUsePreferredFocusedAgent()
    {
        if (!IsFocusedFloating ||
            PreferredAgent is not { } preferredAgent ||
            PreferredAgentId != preferredAgent.Id ||
            preferredAgent.Status != AgentLifecycleStatus.Active ||
            preferredAgent.IsTemplate)
        {
            return false;
        }

        agents = [preferredAgent];
        if (selectedAgentId == preferredAgent.Id)
        {
            selectedAgent = preferredAgent;
        }

        return true;
    }

    private async Task<AgentDefinition> ToggleAgentFavoriteAsync(AgentDefinition agent)
    {
        var editor = await WorkspaceService.GetAgentEditorAsync(agent.Id);
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

        await WorkspaceService.SaveAgentAsync(editor);
        await RefreshAgentCatalogAsync();
        return agents.FirstOrDefault(item => item.Id == agent.Id)
            ?? throw new InvalidOperationException("Agent was not found after saving favorite state.");
    }

    private Task OpenRuntimeDetailsDialogAsync()
    {
        if (!CanOpenRuntimeDetails)
        {
            SetMessage("Heads up", "warning", "Send a prompt first so runtime evidence can be opened.");
            return Task.CompletedTask;
        }

        _ = DialogService.OpenAsync<AgentRuntimeDetailsDialog>(
            "Runtime details",
            new Dictionary<string, object?>
            {
                [nameof(AgentRuntimeDetailsDialog.Run)] = workspace?.SelectedRun,
                [nameof(AgentRuntimeDetailsDialog.ExecutionLog)] = executionLog,
                [nameof(AgentRuntimeDetailsDialog.Metrics)] = metrics,
                [nameof(AgentRuntimeDetailsDialog.RunStateText)] = runStateText,
                [nameof(AgentRuntimeDetailsDialog.RunStateTone)] = runStateTone
            },
            new DialogOptions
            {
                Eyebrow = "Agent runtime",
                Subtitle = BuildRuntimeDialogSubtitle(),
                Size = ModalSize.Full,
                DenseChrome = true,
                TestId = "agent-runtime-details-dialog",
                AriaLabel = "Agent runtime details",
                Style = "max-height:calc(100vh - 2rem);"
            });

        return Task.CompletedTask;
    }

    private void HandleExecutionUpdated(object? sender, ExecutionLogEntry entry)
    {
        if (isDisposed || !ShouldAcceptExecutionEntry(entry))
        {
            return;
        }

        _ = ObserveExecutionUpdateAsync(entry);
    }

    private async Task ObserveExecutionUpdateAsync(ExecutionLogEntry entry)
    {
        try
        {
            await InvokeAsync(() => ApplyExecutionUpdateAsync(entry));
        }
        catch (Exception exception) when (isDisposed)
        {
            Logger.LogDebug(
                exception,
                "Ignored an agent execution update after its chat panel was disposed. AgentId={AgentId} ChatSessionId={ChatSessionId} ExecutionRunId={ExecutionRunId} State={ExecutionState} FailureType={FailureType}.",
                entry.AgentId,
                entry.ChatSessionId,
                entry.ExecutionRunId,
                entry.State,
                exception.GetType().Name);
        }
        catch (Exception exception)
        {
            Logger.LogWarning(
                exception,
                "Unable to apply an agent execution update to the current chat panel. AgentId={AgentId} ChatSessionId={ChatSessionId} ExecutionRunId={ExecutionRunId} State={ExecutionState} FailureType={FailureType}.",
                entry.AgentId,
                entry.ChatSessionId,
                entry.ExecutionRunId,
                entry.State,
                exception.GetType().Name);
        }
    }

    private async Task ApplyExecutionUpdateAsync(ExecutionLogEntry entry)
    {
        if (isDisposed || !ShouldAcceptExecutionEntry(entry))
        {
            return;
        }

        executionLog = UpsertExecutionLogEntry(executionLog, entry);
        if (workspace?.SelectedRun?.Id == entry.ExecutionRunId)
        {
            runStateText = entry.State.ToString();
            runStateTone = ResolveExecutionTone(entry.State);
            SynchronizeActiveChatRunState(entry.State);
        }

        StateHasChanged();
        if (!ShouldReloadWorkspaceAfterExternalTerminalUpdate(entry))
        {
            return;
        }

        var refreshAgentId = selectedAgentId!.Value;
        var refreshSessionId = selectedSessionId!.Value;
        terminalWorkspaceRefreshRunId = entry.ExecutionRunId;
        try
        {
            await LoadWorkspaceAsync(refreshAgentId, refreshSessionId);
        }
        catch
        {
            if (terminalWorkspaceRefreshRunId == entry.ExecutionRunId)
            {
                terminalWorkspaceRefreshRunId = null;
            }

            throw;
        }

        if (!isDisposed &&
            selectedAgentId == refreshAgentId &&
            selectedSessionId == refreshSessionId)
        {
            StateHasChanged();
        }
    }

    private bool ShouldReloadWorkspaceAfterExternalTerminalUpdate(ExecutionLogEntry entry)
    {
        return entry.State is ExecutionState.Completed or ExecutionState.Failed &&
               entry.ExecutionRunId != Guid.Empty &&
               selectedAgentId.HasValue &&
               selectedSessionId.HasValue &&
               !isBusy &&
               trackedChatOperation.IsCompleted &&
               terminalWorkspaceRefreshRunId != entry.ExecutionRunId;
    }

    private bool ShouldAcceptExecutionEntry(ExecutionLogEntry entry)
    {
        if (selectedAgentId != entry.AgentId)
        {
            return false;
        }

        return !selectedSessionId.HasValue ||
               entry.ChatSessionId == selectedSessionId.Value;
    }

    private static IReadOnlyList<ExecutionLogEntry> UpsertExecutionLogEntry(
        IReadOnlyList<ExecutionLogEntry> entries,
        ExecutionLogEntry entry)
    {
        return entries
            .Where(item => item.Id != entry.Id)
            .Append(entry)
            .OrderByDescending(item => item.CreatedAtUtc)
            .ToList();
    }

    private string BuildPromptWithAttachments()
    {
        if (draftAttachmentPaths.Count == 0)
        {
            return draftPrompt.Trim();
        }

        var attachmentText = string.Join(
            Environment.NewLine,
            draftAttachmentPaths.Select(item => $"- {item}"));

        return $"""
Use these workspace artifacts as input:
{attachmentText}

{draftPrompt.Trim()}
""";
    }

    private bool HasVoiceOwnerActivity()
    {
        lock (voiceOwnerGate)
        {
            return hasBrowserVoiceOwner ||
                   isVoiceModeEnabled ||
                   isVoiceRecording ||
                   isVoiceTranscribing ||
                   isVoiceSpeaking;
        }
    }

    private bool TryBeginVoiceOperation(out VoiceOperation operation)
    {
        lock (voiceOwnerGate)
        {
            if (isDisposed)
            {
                operation = default;
                return false;
            }

            hasBrowserVoiceOwner = true;
            operation = new VoiceOperation(
                voiceOwnerId,
                voiceOwnerGeneration,
                selectedAgentId,
                voiceOperationCancellation.Token);
            return true;
        }
    }

    private bool IsVoiceOperationCurrent(VoiceOperation operation)
    {
        lock (voiceOwnerGate)
        {
            return !isDisposed &&
                   !operation.CancellationToken.IsCancellationRequested &&
                   operation.OwnerId == voiceOwnerId &&
                   operation.Generation == voiceOwnerGeneration &&
                   operation.AgentId == selectedAgentId;
        }
    }

    private async Task ResetVoiceOwnerAsync(bool disableVoiceMode = true)
    {
        string previousOwnerId;
        CancellationTokenSource previousCancellation;
        bool shouldDisposeBrowserOwner;
        lock (voiceOwnerGate)
        {
            if (isDisposed)
            {
                return;
            }

            previousOwnerId = voiceOwnerId;
            previousCancellation = voiceOperationCancellation;
            shouldDisposeBrowserOwner = hasBrowserVoiceOwner;
            voiceOwnerId = Guid.NewGuid().ToString("N");
            voiceOperationCancellation = new CancellationTokenSource();
            voiceOwnerGeneration++;
            hasBrowserVoiceOwner = false;
            isVoiceRecording = false;
            isVoiceTranscribing = false;
            isVoiceSpeaking = false;
            if (disableVoiceMode)
            {
                isVoiceModeEnabled = false;
            }
        }

        previousCancellation.Cancel();
        previousCancellation.Dispose();
        if (shouldDisposeBrowserOwner)
        {
            await DisposeVoiceOwnerInBrowserAsync(previousOwnerId);
        }
    }

    private async Task DisposeVoiceOwnerInBrowserAsync(string ownerId)
    {
        try
        {
            await JsRuntime.InvokeVoidAsync(
                "CanDoItAll.agentFramework.voice.disposeOwner",
                ownerId);
        }
        catch (JSDisconnectedException exception)
        {
            Logger.LogDebug(
                exception,
                "Voice owner cleanup skipped because the browser circuit disconnected. VoiceOwnerId={VoiceOwnerId}.",
                ownerId);
        }
        catch (JSException exception)
        {
            Logger.LogWarning(
                exception,
                "Voice owner cleanup failed in browser interop. VoiceOwnerId={VoiceOwnerId} FailureType={FailureType}.",
                ownerId,
                exception.GetType().Name);
        }
        catch (InvalidOperationException exception)
        {
            Logger.LogDebug(
                exception,
                "Voice owner cleanup was unavailable. VoiceOwnerId={VoiceOwnerId}.",
                ownerId);
        }
    }

    private async Task HandleVoiceModeChangedAsync(bool enabled)
    {
        if (isDisposed)
        {
            return;
        }

        if (!enabled)
        {
            await ResetVoiceOwnerAsync();
            if (!isDisposed)
            {
                SetVoiceStatus("Audio off", "neutral");
            }

            return;
        }

        if (!CanUseSelectedAgentVoiceMode)
        {
            await ResetVoiceOwnerAsync();
            if (isDisposed)
            {
                return;
            }

            SetVoiceStatus("Voice denied", "warning", "This agent does not allow voice mode.");
            return;
        }

        isVoiceModeEnabled = true;
        SetVoiceStatus("Audio on", "primary");
    }

    private async Task ToggleVoiceRecordingAsync()
    {
        if (isDisposed)
        {
            return;
        }

        if (!CanUseSelectedAgentVoiceMode)
        {
            SetVoiceStatus("Voice denied", "warning", "This agent does not allow voice mode.");
            return;
        }

        if (!isVoiceRecording)
        {
            if (!TryBeginVoiceOperation(out var operation))
            {
                return;
            }

            try
            {
                await JsRuntime.InvokeVoidAsync(
                    "CanDoItAll.agentFramework.voice.startRecordingForOwner",
                    operation.CancellationToken,
                    operation.OwnerId);
                if (!IsVoiceOperationCurrent(operation))
                {
                    return;
                }

                isVoiceModeEnabled = true;
                isVoiceRecording = true;
                SetVoiceStatus("Recording", "danger");
            }
            catch (Exception) when (!IsVoiceOperationCurrent(operation))
            {
            }
            catch (Exception exception)
            {
                SetVoiceStatus("Record failed", "danger", exception.Message);
            }

            return;
        }

        await StopRecordingAndSendAsync();
    }

    private async Task StopRecordingAndSendAsync()
    {
        if (isDisposed)
        {
            return;
        }

        isVoiceRecording = false;
        isVoiceTranscribing = true;
        SetVoiceStatus("Transcribing", "info");
        if (!TryBeginVoiceOperation(out var operation))
        {
            return;
        }

        try
        {
            var recording = await JsRuntime.InvokeAsync<BrowserVoiceRecording>(
                "CanDoItAll.agentFramework.voice.stopRecordingForOwner",
                operation.CancellationToken,
                operation.OwnerId);
            var result = await VoiceService.TranscribeAsync(
                recording.ToTranscriptionRequest(),
                operation.CancellationToken);
            if (!IsVoiceOperationCurrent(operation))
            {
                return;
            }

            draftPrompt = result.Text;
            composerKey++;
            SetVoiceStatus("Sending", "info");
            if (!IsVoiceOperationCurrent(operation))
            {
                return;
            }

            await SendMessageAsync();
        }
        catch (Exception) when (!IsVoiceOperationCurrent(operation))
        {
        }
        catch (Exception exception)
        {
            SetVoiceStatus("Voice failed", "danger", exception.Message);
        }
        finally
        {
            if (IsVoiceOperationCurrent(operation))
            {
                isVoiceTranscribing = false;
            }
        }
    }

    private Task SpeakLatestAssistantMessageAsync()
    {
        if (isDisposed)
        {
            return Task.CompletedTask;
        }

        var latestAssistantMessage = workspace?.SelectedSession?.Messages
            .Where(message => message.Role == ChatMessageRole.Assistant)
            .OrderByDescending(message => message.CreatedAtUtc)
            .FirstOrDefault();
        if (latestAssistantMessage is null || string.IsNullOrWhiteSpace(latestAssistantMessage.Content))
        {
            SetVoiceStatus("Nothing to speak", "warning", "No assistant message is available.");
            return Task.CompletedTask;
        }

        return SpeakTextAsync(latestAssistantMessage.Content);
    }

    private async Task SpeakTextAsync(string text)
    {
        if (isDisposed)
        {
            return;
        }

        if (selectedAgent is null)
        {
            SetVoiceStatus("No agent", "warning", "Select an agent before using text-to-speech.");
            return;
        }

        if (!TryBeginVoiceOperation(out var operation))
        {
            return;
        }

        var voiceAccess = SelectedAgentVoiceAccess;
        var suppressIdentifierOmissionNotice = ShouldSuppressIdentifierOmissionNotice();

        isVoiceSpeaking = true;
        SetVoiceStatus("Speaking", "primary");
        try
        {
            await JsRuntime.InvokeVoidAsync(
                "CanDoItAll.agentFramework.voice.clearAudioQueueForOwner",
                operation.CancellationToken,
                operation.OwnerId);
            if (!IsVoiceOperationCurrent(operation))
            {
                return;
            }

            var queuedChunks = 0;
            await foreach (var synthesis in VoiceService.SynthesizeChunksAsync(new AgentVoiceSynthesisRequest(
                               text,
                               voiceAccess,
                               SuppressIdentifierOmissionNotice: suppressIdentifierOmissionNotice),
                               operation.CancellationToken))
            {
                if (!IsVoiceOperationCurrent(operation))
                {
                    return;
                }

                TrackIdentifierOmissionNotice(synthesis);
                queuedChunks++;
                await JsRuntime.InvokeVoidAsync(
                    "CanDoItAll.agentFramework.voice.enqueueAudioForOwner",
                    operation.CancellationToken,
                    operation.OwnerId,
                    Convert.ToBase64String(synthesis.AudioBytes),
                    synthesis.ContentType);
                if (!IsVoiceOperationCurrent(operation))
                {
                    return;
                }

                if (queuedChunks == 1)
                {
                    SetVoiceStatus("Playing", "primary");
                }
            }

            if (IsVoiceOperationCurrent(operation))
            {
                SetVoiceStatus(queuedChunks == 1 ? "Audio ready" : $"Audio ready ({queuedChunks} chunks)", "success");
            }
        }
        catch (Exception) when (!IsVoiceOperationCurrent(operation))
        {
        }
        catch (Exception exception)
        {
            SetVoiceStatus("Speak failed", "danger", exception.Message);
        }
        finally
        {
            if (IsVoiceOperationCurrent(operation))
            {
                isVoiceSpeaking = false;
            }
        }
    }

    private bool ShouldSuppressIdentifierOmissionNotice()
    {
        return selectedSessionId is { } sessionId
            ? sessionsWithVoiceIdentifierOmissionNotice.Contains(sessionId)
            : hasVoiceIdentifierOmissionNoticeWithoutSession;
    }

    private void TrackIdentifierOmissionNotice(AgentVoiceSynthesisResult synthesis)
    {
        if (!synthesis.IdentifierOmissionNoticeIncluded)
        {
            return;
        }

        if (selectedSessionId is { } sessionId)
        {
            sessionsWithVoiceIdentifierOmissionNotice.Add(sessionId);
            return;
        }

        hasVoiceIdentifierOmissionNoticeWithoutSession = true;
    }

    private void SetVoiceStatus(string text, string tone, string? notification = null)
    {
        voiceStatusText = text;
        voiceStatusTone = tone;
        if (!string.IsNullOrWhiteSpace(notification))
        {
            NotificationService.Warning(text, notification);
        }
    }

    private void ResolveRunState()
    {
        if (workspace?.SelectedRun is null)
        {
            runStateText = string.Empty;
            runStateTone = "neutral";
            return;
        }

        var run = workspace.SelectedRun;
        runStateText = run.State.ToString();
        runStateTone = run.State switch
        {
            ExecutionState.Completed => "success",
            ExecutionState.WaitingOnTool => "warning",
            ExecutionState.Failed => "danger",
            _ => "info"
        };
    }

    private void SynchronizeActiveChatRunState(ExecutionState? executionState = null)
    {
        if (!ActiveChatHandleId.HasValue)
        {
            return;
        }

        var effectiveState = executionState ?? workspace?.SelectedRun?.State;
        var activeState = effectiveState switch
        {
            ExecutionState.Preparing or
            ExecutionState.Running or
            ExecutionState.Persisting => ActiveAgentChatRunState.Running,
            ExecutionState.WaitingOnTool => ActiveAgentChatRunState.AwaitingApproval,
            _ => ActiveAgentChatRunState.Idle
        };
        SetActiveChatRunState(activeState);
    }

    private void SetActiveChatRunState(ActiveAgentChatRunState runState)
    {
        if (ActiveChatHandleId is { } handleId)
        {
            FloatingChatCoordinator.SetRunState(handleId, runState);
        }
    }

    private void ReconcileActiveChatRunState(AgentChatHandleId? handleId)
    {
        if (!handleId.HasValue)
        {
            return;
        }

        try
        {
            FloatingChatCoordinator.ReconcileRunStateAfterOperation(handleId.Value);
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private static string ResolveExecutionTone(ExecutionState state)
    {
        return state switch
        {
            ExecutionState.Completed => "success",
            ExecutionState.WaitingOnTool => "warning",
            ExecutionState.Failed => "danger",
            _ => "info"
        };
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

    private static string BuildSessionMeta(ChatSessionSummaryRecord session)
    {
        return session.MessageCount == 0
            ? "Empty thread"
            : $"{session.MessageCount} message(s)";
    }

    private static string FormatThreadUpdatedAt(ChatSessionSummaryRecord session)
        => session.UpdatedAtUtc.LocalDateTime.ToString("dd.MM HH:mm");

    private static string BuildThreadCardPreview(ChatSessionSummaryRecord session)
    {
        var preview = NormalizeInlineText(session.LastMessagePreview);
        const int maxLength = 88;
        return preview.Length <= maxLength
            ? preview
            : $"{preview[..maxLength].TrimEnd()}...";
    }

    private static string BuildThreadTooltipText(ChatSessionSummaryRecord session)
        => NormalizeInlineText(session.LastMessagePreview);

    private static string NormalizeInlineText(string value)
    {
        return string.Join(
            ' ',
            value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    private bool MatchesThreadSearch(ChatSessionSummaryRecord session)
    {
        if (string.IsNullOrWhiteSpace(threadSearchText))
        {
            return true;
        }

        return session.Title.Contains(threadSearchText, StringComparison.OrdinalIgnoreCase) ||
               session.LastMessagePreview.Contains(threadSearchText, StringComparison.OrdinalIgnoreCase) ||
               BuildSessionMeta(session).Contains(threadSearchText, StringComparison.OrdinalIgnoreCase);
    }

    private string ResolveEmptyThreadText()
    {
        if (workspace?.Sessions.Count > 0)
        {
            return "No threads match the current search.";
        }

        return "The selected agent does not have a thread yet.";
    }

    private string BuildRuntimeDialogSubtitle()
    {
        var agentName = selectedAgent?.Name ?? "Selected agent";
        var threadTitle = workspace?.SelectedSession?.Title ?? "No thread selected";
        return $"{agentName} / {threadTitle}";
    }

    private static string ResolveAgentInitials(AgentDefinition agent)
    {
        var name = string.IsNullOrWhiteSpace(agent.Name)
            ? agent.RoleTitle
            : agent.Name;

        var initials = name
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Take(2)
            .Select(static item => char.ToUpperInvariant(item[0]))
            .ToArray();

        return initials.Length == 0
            ? "AI"
            : new string(initials);
    }

    public async ValueTask DisposeAsync()
    {
        string ownerId;
        CancellationTokenSource cancellation;
        bool shouldDisposeBrowserOwner;
        lock (voiceOwnerGate)
        {
            if (isDisposed)
            {
                return;
            }

            isDisposed = true;
            Interlocked.Increment(ref workspaceLoadGeneration);
            ownerId = voiceOwnerId;
            cancellation = voiceOperationCancellation;
            shouldDisposeBrowserOwner = hasBrowserVoiceOwner;
            hasBrowserVoiceOwner = false;
            voiceOwnerGeneration++;
            isVoiceModeEnabled = false;
            isVoiceRecording = false;
            isVoiceTranscribing = false;
            isVoiceSpeaking = false;
        }

        WorkspaceService.ExecutionUpdated -= HandleExecutionUpdated;
        try
        {
            cancellation.Cancel();
            if (shouldDisposeBrowserOwner)
            {
                await DisposeVoiceOwnerInBrowserAsync(ownerId);
            }
        }
        finally
        {
            cancellation.Dispose();
        }
    }

    private readonly record struct VoiceOperation(
        string OwnerId,
        long Generation,
        Guid? AgentId,
        CancellationToken CancellationToken);
}
