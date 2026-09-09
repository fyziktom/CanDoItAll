using System.Collections.Immutable;
using CanDoItAll.AgentFramework.UI.Chat;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Components;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Voice;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Conversations.Components.Presentation;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace CanDoItAll.Modules.AgentFramework.Pages.Components;

using IProviderRuntimeAdministrationService = CanDoItAll.Modules.AgentFramework.ProviderManagement.IProviderRuntimeAdministrationService;

public partial class AgentChatPanel : IAsyncDisposable {
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
    public IProviderRuntimeAdministrationService ProviderRuntimeAdministrationService { get; set; } = default!;

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
    public ILogger<AgentChatPanel> Logger { get; set; } = default!;

    private AgentChatSession chatSession = default!;
    private IReadOnlyList<AgentDefinition> agents => chatSession.Agents;
    private IReadOnlyList<ProviderProfile> providers => chatSession.Providers;
    private IReadOnlyCollection<Guid> privateAgentIds => chatSession.PrivateAgentIds;
    private ChatAgentWorkspaceSnapshot? workspace => chatSession.Workspace;
    private IReadOnlyList<ExecutionLogEntry> executionLog => chatSession.ExecutionLog;
    private IReadOnlyList<AgentRunMetric> metrics => chatSession.Metrics;
    private AgentDefinition? selectedAgent => chatSession.Agent;
    private Guid? selectedAgentId => chatSession.Agent?.Id;
    private Guid? selectedSessionId => chatSession.Workspace?.SelectedSessionId;
    private long publishedAgentRevision;
    private (Guid? Agent, Guid? Session, AgentDefinition? Preferred, AgentChatPanelDisplayMode Mode)? parameterTarget;
    private long effectOwner;
    private long composerGeneration;
    private CancellationTokenSource? attachmentRead;
    private bool switchDialogOpen;

    private AgentChatNavigationPresentation NavigationPresentation => new() {
        Generation = chatSession.Generation,
        Focused = IsFocusedFloating,
        IsLoading = chatSession.IsLoading,
        IsBusy = isBusy,
        HasAgents = agents.Count > 0,
        Agent = selectedAgent is { } agent ? new(agent.Id, agent.Name, agent.AvatarImageUrl, ResolveAgentInitials(agent)) : null,
        ErrorMessage = workspace is null ? chatSession.ErrorMessage : "",
        Threads = ThreadPresentations.ToImmutableArray()
    };

    private Task HandleNavigationIntentAsync(AgentChatNavigationIntent intent) => !chatSession.IsCurrent(intent.Generation)
        ? Task.CompletedTask : intent.Action switch {
            AgentChatNavigationAction.SwitchAgent => OpenAgentSwitchDialogAsync(),
            AgentChatNavigationAction.Refresh => RefreshAsync(),
            AgentChatNavigationAction.NewThread => CreateThreadAsync(),
            AgentChatNavigationAction.SelectThread when intent.SessionId is { } id => SelectSessionAsync(id),
            _ => Task.CompletedTask
        };

    private string draftPrompt = string.Empty;
    private string pendingUserPrompt = string.Empty;
    private IReadOnlyList<string> draftAttachmentPaths = [];
    private bool isBusy;
    private int composerKey;
    private string runStateText = string.Empty;
    private string runStateTone = "neutral";
    private bool isVoiceModeEnabled;
    private bool isVoiceRecording;
    private bool isVoiceTranscribing;
    private bool isVoiceSpeaking;
    private bool isDisposed;
    private AgentChatContextAccessState? publishedAccessState;
    private string voiceStatusText = string.Empty;
    private string voiceStatusTone = "neutral";
    private Task trackedChatOperation = Task.CompletedTask;
    private AgentExecutionActivityStreamId? activeActivityStreamId;
    private Guid? terminalWorkspaceRefreshRunId;
    private readonly HashSet<Guid> sessionsWithVoiceIdentifierOmissionNotice = [];
    private bool hasVoiceIdentifierOmissionNoticeWithoutSession;

    private IReadOnlyList<ConversationThreadPresentation> ThreadPresentations
        => workspace?.Sessions
            .Select(session => AgentThreadPresentationMapper.Map(session, selectedSessionId))
            .ToArray() ?? [];

    private bool CanOpenRuntimeDetails
        => workspace?.SelectedRun is not null ||
           executionLog.Count > 0 ||
           metrics.Count > 0;

    private bool IsChatInteractionBusy
        => isBusy || chatSession.IsLoading ||
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

    protected override void OnInitialized() {
        chatSession = new(WorkspaceService, ProviderRuntimeAdministrationService, Logger);
        WorkspaceService.ExecutionUpdated += HandleExecutionUpdated;
    }

    protected override async Task OnParametersSetAsync() {
        var requested = (PreferredAgentId, PreferredSessionId, PreferredAgent, DisplayMode);
        if (isDisposed || parameterTarget == requested) {
            return;
        }
        var previous = parameterTarget;
        parameterTarget = requested;
        if (previous.HasValue && chatSession.MatchesAccepted(PreferredAgentId, PreferredSessionId)
            && previous.Value.Preferred == PreferredAgent && previous.Value.Mode == DisplayMode) {
            return;
        }
        await LoadTargetAsync(PreferredAgentId ?? (IsFocusedFloating ? null : selectedAgentId), PreferredSessionId,
            refreshCatalog: true, throwOnFailure: false, usePreferredAgent: true);
    }

    private Task LoadAsync() => LoadTargetAsync(PreferredAgentId ?? selectedAgentId,
        PreferredSessionId ?? selectedSessionId, refreshCatalog: true, throwOnFailure: false);

    private async Task PublishAgentObservationAsync(long generation) {
        if (!chatSession.IsCurrent(generation) || publishedAgentRevision == chatSession.AgentRevision) {
            return;
        }
        publishedAgentRevision = chatSession.AgentRevision;
        await SelectedAgentChanged.InvokeAsync(selectedAgent);
    }

    private void ResetTargetEffects() {
        effectOwner++;
        composerGeneration++;
        attachmentRead?.Cancel();
        attachmentRead = null;
        trackedChatOperation = Task.CompletedTask;
        pendingUserPrompt = "";
        draftPrompt = "";
        draftAttachmentPaths = [];
        composerKey++;
        isBusy = false;
        activeActivityStreamId = null;
        terminalWorkspaceRefreshRunId = null;
        switchDialogOpen = false;
        runStateText = "";
        runStateTone = "neutral";
    }

    private async Task RefreshAsync() {
        await LoadAsync();
    }

    private Task SelectAgentAsync(Guid agentId)
        => LoadTargetAsync(agentId, null, refreshCatalog: false, throwOnFailure: false);

    private Task SelectSessionAsync(Guid sessionId) => selectedAgentId is { } agentId
        ? LoadTargetAsync(agentId, sessionId, refreshCatalog: false, throwOnFailure: false) : Task.CompletedTask;

    private async Task CreateThreadAsync() {
        if (selectedAgentId is not { } agentId || isBusy) {
            return;
        }
        var generation = chatSession.Generation;
        var owner = ++effectOwner;
        var handle = ActiveChatHandleId;
        isBusy = true;
        try {
            if (handle.HasValue) {
                await FloatingChatCoordinator.StartNewChatAsync(agentId);
                return;
            }
            var created = await WorkspaceService.GetOrCreateChatSessionAsync(agentId);
            if (!chatSession.IsCurrent(generation) || owner != effectOwner) {
                return;
            }
            await LoadTargetAsync(agentId, created.Id, refreshCatalog: false, throwOnFailure: true);
            if (chatSession.IsCurrent(generation + 1) && chatSession.MatchesAccepted(agentId, created.Id)) {
                SetMessage("Ready", "success", "New thread created.");
            }
        } catch (Exception exception) {
            LogOperationFailure(exception, agentId, null, handle, "create thread", false);
            if (owner == effectOwner && !isDisposed) {
                SetMessage("Attention", "danger", "The thread could not be created. Refresh before trying again.");
            }
        } finally {
            if (owner == effectOwner) {
                isBusy = false;
            }
        }
    }

    private Task HandleDraftPromptChangedAsync(string value) {
        draftPrompt = value;
        composerGeneration++;
        return Task.CompletedTask;
    }

    private Task InsertPromptGalleryContentAsync(string content, long generation) {
        if (!chatSession.IsCurrent(generation)) {
            return Task.CompletedTask;
        }
        ArgumentException.ThrowIfNullOrWhiteSpace(content);
        composerGeneration++;
        var normalizedContent = content.Trim();
        draftPrompt = string.IsNullOrWhiteSpace(draftPrompt)
            ? normalizedContent
            : $"{draftPrompt.TrimEnd()}{Environment.NewLine}{Environment.NewLine}{normalizedContent}";
        composerKey++;
        return Task.CompletedTask;
    }

    private Task SendMessageAsync() {
        if (!selectedAgentId.HasValue) {
            SetMessage("Heads up", "warning", "Select a technical agent before sending a prompt.");
            return Task.CompletedTask;
        }

        if (BlocksNewMessage) {
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
        if (string.IsNullOrWhiteSpace(prompt)) {
            SetMessage("Heads up", "warning", "Enter a prompt before sending it.");
            return Task.CompletedTask;
        }

        var executionAgentId = selectedAgentId.Value;
        var executionSessionId = selectedSessionId;
        var executionWorkspaceGeneration = chatSession.Generation;
        var executionHandleId = ActiveChatHandleId;
        var executionAttachmentPaths = draftAttachmentPaths.ToImmutableArray();
        if (!TryBeginChatOperation(executionHandleId)) {
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
            executionWorkspaceGeneration,
            executionHandleId,
            prompt,
            executionAttachmentPaths,
            previousDraft,
            effectOwner);
        return Task.CompletedTask;
    }

    private async Task RunMessageOperationAsync(
        Guid executionAgentId,
        Guid? executionSessionId,
        long executionWorkspaceGeneration,
        AgentChatHandleId? executionHandleId,
        string prompt,
        IReadOnlyList<string> attachmentPaths,
        string previousDraft,
        long owner) {
        var executionCompleted = false;
        var continuationWorkspaceGeneration = executionWorkspaceGeneration;
        try {
            var operation = ChatExecutionOrchestrator.StartSendMessage(
                new AgentChatSendRequest(
                    executionAgentId,
                    executionSessionId,
                    prompt) {
                    AttachmentPaths = attachmentPaths,
                    ConversationHandleId = executionHandleId
                });
            activeActivityStreamId = operation.StreamId;
            await InvokeAsync(StateHasChanged);
            var result = await operation.Completion;
            executionCompleted = true;
            if (!IsOperationWorkspaceCurrent(
                    executionAgentId,
                    executionSessionId,
                    executionWorkspaceGeneration)) {
                LogDetachedCompletion(executionAgentId, result.ChatSessionId, executionHandleId, "send");
                return;
            }

            draftAttachmentPaths = [];
            continuationWorkspaceGeneration = unchecked(executionWorkspaceGeneration + 1);
            await LoadWorkspaceAsync(executionAgentId, result.ChatSessionId);
            if (chatSession.Generation != continuationWorkspaceGeneration ||
                selectedAgentId != executionAgentId ||
                selectedSessionId != result.ChatSessionId) {
                return;
            }

            SetMessage("Ready", "success", "Prompt sent through the integrated runtime.");
            if (isVoiceModeEnabled && !string.IsNullOrWhiteSpace(result.AssistantMessage.Content)) {
                await SpeakTextAsync(result.AssistantMessage.Content);
            }
        } catch (AgentChatRunFailedException exception) when (isDisposed) {
            LogDetachedRunFailure(
                exception,
                executionHandleId,
                "send");
        } catch (Exception exception) when (isDisposed) {
            LogDetachedOperationFailure(exception, executionAgentId, executionSessionId, executionHandleId, "send");
        } catch (AgentChatRunFailedException exception) {
            if (exception.AgentId == executionAgentId &&
                (!executionSessionId.HasValue ||
                 exception.ChatSessionId == executionSessionId) &&
                IsOperationWorkspaceCurrent(
                    executionAgentId,
                    executionSessionId,
                    executionWorkspaceGeneration)) {
                var reloadGeneration = chatSession.Generation;
                var workspaceReloaded = await TryReloadFailedWorkspaceAsync(exception);
                if (WasFailedRunReloadSuperseded(reloadGeneration) ||
                    selectedAgentId != exception.AgentId ||
                    selectedSessionId != exception.ChatSessionId) {
                    LogInactiveSelectionRunFailure(
                        exception,
                        executionHandleId,
                        "send");
                    return;
                }

                SetMessage(
                    "Attention",
                    "danger",
                    BuildFailedRunMessage(
                        exception.SanitizedDisplayMessage,
                        workspaceReloaded));
                return;
            }

            LogInactiveSelectionRunFailure(
                exception,
                executionHandleId,
                "send");
        } catch (Exception exception) {
            LogOperationFailure(
                exception,
                executionAgentId,
                executionSessionId,
                executionHandleId,
                "send",
                executionCompleted);
            if (IsOperationWorkspaceCurrent(
                    executionAgentId,
                    executionSessionId,
                    continuationWorkspaceGeneration)) {
                if (!executionCompleted) {
                    draftPrompt = previousDraft;
                    composerKey++;
                }

                ResolveRunState();
                SetMessage(
                    executionCompleted ? "Refresh needed" : "Attention",
                    executionCompleted ? "warning" : "danger",
                    executionCompleted
                        ? "The prompt completed, but the latest thread state could not be loaded. Reload this thread to see the persisted result."
                        : "The prompt could not be completed. Retry, or inspect runtime details for the persisted failure.");
            }
        } finally {
            if (owner == effectOwner && !isDisposed) {
                pendingUserPrompt = string.Empty;
                isBusy = false;
                if (runStateText == UpdatingWorkspaceContextRunState) {
                    ResolveRunState();
                }
            }

            await FinishChatOperationAsync(executionHandleId, executionAgentId, executionSessionId, "send", owner);
        }
    }

    private Task HandleApprovalDecisionsAsync(IReadOnlyList<PendingToolApprovalDecision> decisions)
        => StartApprovalOperation(decisions, autoApprovePendingToolCalls: false);

    private Task ApproveConversationAsync()
        => StartApprovalOperation(decisions: null, autoApprovePendingToolCalls: true);

    private Task StartApprovalOperation(
        IReadOnlyList<PendingToolApprovalDecision>? decisions,
        bool autoApprovePendingToolCalls) {
        if (!selectedAgentId.HasValue || !selectedSessionId.HasValue) {
            return Task.CompletedTask;
        }

        var executionAgentId = selectedAgentId.Value;
        var executionSessionId = selectedSessionId.Value;
        var executionWorkspaceGeneration = chatSession.Generation;
        var executionHandleId = ActiveChatHandleId;
        var capturedDecisions = decisions?.ToImmutableArray() ?? workspace?.SelectedRun?.PendingApprovals
            .Select(item => new PendingToolApprovalDecision(item.ApprovalId, true)).ToImmutableArray() ?? [];
        if (capturedDecisions.IsEmpty) {
            return Task.CompletedTask;
        }
        if (!TryBeginChatOperation(executionHandleId)) {
            return Task.CompletedTask;
        }

        trackedChatOperation = RunApprovalOperationAsync(
            executionAgentId,
            executionSessionId,
            executionWorkspaceGeneration,
            executionHandleId,
            capturedDecisions,
            autoApprovePendingToolCalls,
            effectOwner);
        return Task.CompletedTask;
    }

    private async Task RunApprovalOperationAsync(
        Guid executionAgentId,
        Guid executionSessionId,
        long executionWorkspaceGeneration,
        AgentChatHandleId? executionHandleId,
        IReadOnlyList<PendingToolApprovalDecision> decisions,
        bool autoApprovePendingToolCalls,
        long owner) {
        await Task.Yield();
        var executionCompleted = false;
        var continuationWorkspaceGeneration = executionWorkspaceGeneration;
        var approved = true;
        try {
            var effectiveDecisions = decisions;
            if (effectiveDecisions.Count == 0) {
                return;
            }

            approved = effectiveDecisions.All(item => item.Approved);
            var operation = ChatExecutionOrchestrator.StartApprovalContinuation(
                executionAgentId,
                executionSessionId,
                effectiveDecisions,
                autoApprovePendingToolCalls);
            if (owner == effectOwner && !isDisposed) {
                activeActivityStreamId = operation.StreamId;
                await InvokeAsync(StateHasChanged);
            }
            await operation.Completion;
            executionCompleted = true;
            if (!IsOperationWorkspaceCurrent(
                    executionAgentId,
                    executionSessionId,
                    executionWorkspaceGeneration)) {
                LogDetachedCompletion(executionAgentId, executionSessionId, executionHandleId, "approval");
                return;
            }

            continuationWorkspaceGeneration = unchecked(executionWorkspaceGeneration + 1);
            await LoadWorkspaceAsync(executionAgentId, executionSessionId);
            if (chatSession.Generation != continuationWorkspaceGeneration ||
                selectedAgentId != executionAgentId ||
                selectedSessionId != executionSessionId) {
                return;
            }

            SetMessage(
                approved ? "Ready" : "Heads up",
                approved ? "success" : "warning",
                approved
                    ? autoApprovePendingToolCalls
                        ? "Approval resumed the run and enabled remaining approvals for the active execution."
                        : "Approval resumed the run."
                    : "The decisions were submitted; rejected proposals will not execute and the thread was refreshed.");
        } catch (AgentChatRunFailedException exception) when (isDisposed) {
            LogDetachedRunFailure(
                exception,
                executionHandleId,
                "approval");
        } catch (Exception exception) when (isDisposed) {
            LogDetachedOperationFailure(exception, executionAgentId, executionSessionId, executionHandleId, "approval");
        } catch (AgentChatRunFailedException exception) {
            if (exception.AgentId == executionAgentId &&
                exception.ChatSessionId == executionSessionId &&
                IsOperationWorkspaceCurrent(
                    executionAgentId,
                    executionSessionId,
                    executionWorkspaceGeneration)) {
                var reloadGeneration = chatSession.Generation;
                var workspaceReloaded = await TryReloadFailedWorkspaceAsync(exception);
                if (WasFailedRunReloadSuperseded(reloadGeneration) ||
                    selectedAgentId != exception.AgentId ||
                    selectedSessionId != exception.ChatSessionId) {
                    LogInactiveSelectionRunFailure(
                        exception,
                        executionHandleId,
                        "approval");
                    return;
                }

                SetMessage(
                    "Attention",
                    "danger",
                    BuildFailedRunMessage(
                        exception.SanitizedDisplayMessage,
                        workspaceReloaded));
                return;
            }

            LogInactiveSelectionRunFailure(
                exception,
                executionHandleId,
                "approval");
        } catch (Exception exception) {
            LogOperationFailure(
                exception,
                executionAgentId,
                executionSessionId,
                executionHandleId,
                "approval",
                executionCompleted);
            if (IsOperationWorkspaceCurrent(
                    executionAgentId,
                    executionSessionId,
                    continuationWorkspaceGeneration)) {
                SetMessage(
                    executionCompleted ? "Refresh needed" : "Attention",
                    executionCompleted ? "warning" : "danger",
                    executionCompleted
                        ? "The approval completed, but the latest thread state could not be loaded. Reload this thread to see the persisted result."
                        : "The approval could not be completed. Retry, or inspect runtime details for the persisted failure.");
            }
        } finally {
            if (owner == effectOwner && !isDisposed) {
                isBusy = false;
            }
            await FinishChatOperationAsync(executionHandleId, executionAgentId, executionSessionId, "approval", owner);
        }
    }

    private bool TryBeginChatOperation(AgentChatHandleId? handleId) {
        if (isBusy || !trackedChatOperation.IsCompleted) {
            return false;
        }

        if (handleId.HasValue && !FloatingChatCoordinator.TryBeginOperation(handleId.Value)) {
            return false;
        }

        isBusy = true;
        effectOwner++;
        return true;
    }

    private bool IsOperationWorkspaceCurrent(
        Guid agentId,
        Guid? sessionId,
        long workspaceGeneration) {
        return !isDisposed &&
               workspaceGeneration == chatSession.Generation &&
               selectedAgentId == agentId &&
               selectedSessionId == sessionId;
    }

    private async Task FinishChatOperationAsync(
        AgentChatHandleId? handleId,
        Guid agentId,
        Guid? sessionId,
        string operationKind,
        long owner) {
        try {
            ReconcileActiveChatRunState(handleId);
            if (!isDisposed && owner == effectOwner) {
                SynchronizeActiveChatRunState();
                await InvokeAsync(StateHasChanged);
            }
        } catch (Exception exception) {
            Logger.LogError(
                "Detached agent chat operation cleanup failed. Operation={OperationKind} AgentId={AgentId} ChatSessionId={ChatSessionId} HandleId={HandleId} FailureType={FailureType}.",
                operationKind,
                agentId,
                sessionId,
                handleId?.Value,
                exception.GetType().Name);
        }
    }

    private void LogDetachedCompletion(Guid agentId, Guid? sessionId, AgentChatHandleId? handleId, string operationKind)
        => Logger.LogInformation("Agent chat execution completed outside its original target. Operation={OperationKind} AgentId={AgentId} ChatSessionId={ChatSessionId} HandleId={HandleId}",
            operationKind, agentId, sessionId, handleId?.Value);

    private void LogDetachedOperationFailure(
        Exception exception,
        Guid agentId,
        Guid? sessionId,
        AgentChatHandleId? handleId,
        string operationKind) {
        Logger.LogWarning(
            "Detached agent chat operation finished after its panel was disposed. Operation={OperationKind} AgentId={AgentId} ChatSessionId={ChatSessionId} HandleId={HandleId} FailureType={FailureType}.",
            operationKind,
            agentId,
            sessionId,
            handleId?.Value,
            exception.GetType().Name);
    }

    private void LogOperationFailure(
        Exception exception,
        Guid agentId,
        Guid? sessionId,
        AgentChatHandleId? handleId,
        string operationKind,
        bool executionCompleted) {
        Logger.LogWarning(
            "Agent chat operation failed. Operation={OperationKind} AgentId={AgentId} ChatSessionId={ChatSessionId} HandleId={HandleId} ExecutionCompleted={ExecutionCompleted} FailureType={FailureType}.",
            operationKind,
            agentId,
            sessionId,
            handleId?.Value,
            executionCompleted,
            exception.GetType().Name);
    }

    private void LogDetachedRunFailure(
        AgentChatRunFailedException exception,
        AgentChatHandleId? handleId,
        string operationKind) {
        Logger.LogWarning(
            "Detached agent chat run failed after its panel was disposed. Operation={OperationKind} AgentId={AgentId} ChatSessionId={ChatSessionId} ExecutionRunId={ExecutionRunId} HandleId={HandleId} FailureCategory={FailureCategory}.",
            operationKind,
            exception.AgentId,
            exception.ChatSessionId,
            exception.ExecutionRunId,
            handleId?.Value,
            exception.FailureCategory);
    }

    private void LogInactiveSelectionRunFailure(
        AgentChatRunFailedException exception,
        AgentChatHandleId? handleId,
        string operationKind) {
        Logger.LogWarning(
            "Agent chat run failure did not match the active panel selection. Operation={OperationKind} AgentId={AgentId} ChatSessionId={ChatSessionId} ExecutionRunId={ExecutionRunId} SelectedAgentId={SelectedAgentId} SelectedChatSessionId={SelectedChatSessionId} HandleId={HandleId} FailureCategory={FailureCategory}.",
            operationKind,
            exception.AgentId,
            exception.ChatSessionId,
            exception.ExecutionRunId,
            selectedAgentId,
            selectedSessionId,
            handleId?.Value,
            exception.FailureCategory);
    }

    private async Task<bool> TryReloadFailedWorkspaceAsync(
        AgentChatRunFailedException exception) {
        try {
            await LoadWorkspaceAsync(
                exception.AgentId,
                exception.ChatSessionId,
                exception.ExecutionRunId);
            return !isDisposed &&
                   selectedAgentId == exception.AgentId &&
                   selectedSessionId == exception.ChatSessionId &&
                   workspace?.SelectedRun?.Id == exception.ExecutionRunId;
        } catch (Exception reloadException) {
            Logger.LogWarning(
                "Unable to reload the persisted failed agent run. AgentId={AgentId} ChatSessionId={ChatSessionId} ExecutionRunId={ExecutionRunId} FailureCategory={FailureCategory} ReloadFailureType={ReloadFailureType}.",
                exception.AgentId,
                exception.ChatSessionId,
                exception.ExecutionRunId,
                exception.FailureCategory,
                reloadException.GetType().Name);
            return false;
        }
    }

    private bool WasFailedRunReloadSuperseded(long generationBeforeReload)
        => chatSession.Generation != unchecked(generationBeforeReload + 1);

    private static string BuildFailedRunMessage(
        string sanitizedDisplayMessage,
        bool workspaceReloaded) {
        return workspaceReloaded
            ? sanitizedDisplayMessage
            : $"{sanitizedDisplayMessage} The failed run was persisted, but runtime details could not be refreshed. Reload this workspace to see the persisted failure.";
    }

    private async Task StageAttachmentsAsync() {
        if (workspace?.SelectedRun is not { } run) {
            SetMessage("Heads up", "warning", "Run a prompt first so the thread has execution artifacts to stage.");
            return;
        }
        var generation = chatSession.Generation;
        var composer = composerGeneration;
        attachmentRead?.Cancel();
        using var request = CancellationTokenSource.CreateLinkedTokenSource(chatSession.TargetCancellation);
        attachmentRead = request;
        try {
            var detail = await WorkspaceService.GetExecutionRunDetailAsync(run.Id, request.Token);
            if (!chatSession.IsCurrent(generation) || composer != composerGeneration || !ReferenceEquals(attachmentRead, request)) {
                return;
            }
            if (detail.Run.Id != run.Id || detail.Run.AgentId != selectedAgentId || detail.Run.ChatSessionId != selectedSessionId) {
                SetMessage("Attachment failed", "danger", "The artifacts do not belong to this thread.");
                return;
            }
            draftAttachmentPaths = detail.Artifacts.Select(item => item.RelativePath).Where(IsRelativeAttachment)
                .Distinct(StringComparer.OrdinalIgnoreCase).ToImmutableArray();
            SetMessage("Ready", draftAttachmentPaths.Count == 0 ? "warning" : "success", draftAttachmentPaths.Count == 0
                ? "The selected run does not have persisted artifacts yet." : $"Staged {draftAttachmentPaths.Count} artifact path(s) for the next prompt.");
        } catch (Exception exception) {
            if (chatSession.IsCurrent(generation) && !request.IsCancellationRequested) {
                LogOperationFailure(exception, run.AgentId, run.ChatSessionId, ActiveChatHandleId, "read artifacts", false);
                SetMessage("Attachment failed", "danger", "The thread artifacts could not be loaded.");
            }
        } finally {
            if (ReferenceEquals(attachmentRead, request)) {
                attachmentRead = null;
            }
        }
    }

    private async Task StageUploadedAttachmentFilesAsync(InputFileChangeEventArgs args) {
        if (selectedAgentId is not { } agentId || isBusy) {
            return;
        }
        var generation = chatSession.Generation;
        var composer = composerGeneration;
        var sessionId = selectedSessionId;
        var owner = ++effectOwner;
        using var request = CancellationTokenSource.CreateLinkedTokenSource(chatSession.TargetCancellation);
        isBusy = true;
        try {
            var stagedPaths = new List<string>(draftAttachmentPaths);
            foreach (var file in args.GetMultipleFiles(8)) {
                await using var stream = file.OpenReadStream(AgentChatAttachmentStagingService.MaxImageAttachmentBytes, request.Token);
                var staged = await AttachmentStagingService.StageImageAsync(file.Name, file.ContentType, file.Size, stream, request.Token);
                if (!chatSession.IsCurrent(generation) || composer != composerGeneration || request.IsCancellationRequested) {
                    return;
                }
                if (!IsRelativeAttachment(staged.RelativePath)) {
                    throw new InvalidOperationException("Attachment staging returned a non-relative path.");
                }
                stagedPaths.Add(staged.RelativePath);
            }
            draftAttachmentPaths = stagedPaths.Distinct(StringComparer.OrdinalIgnoreCase).ToImmutableArray();
            SetMessage("Ready", "success", $"Staged {draftAttachmentPaths.Count} attachment path(s) for the next prompt.");
        } catch (Exception exception) {
            if (chatSession.IsCurrent(generation) && !request.IsCancellationRequested) {
                LogOperationFailure(exception, agentId, sessionId, ActiveChatHandleId, "upload attachment", false);
                SetMessage("Attachment failed", "danger", "The image could not be staged. Check its type and size before trying again.");
            }
        } finally {
            if (owner == effectOwner) {
                isBusy = false;
            }
        }
    }

    private static bool IsRelativeAttachment(string value) => !string.IsNullOrWhiteSpace(value)
        && !value.StartsWith('/') && !value.StartsWith('\\') && !value.Contains(':');

    private async Task HandleSessionTitleChangedAsync(string title) {
        if (selectedAgentId is not { } agentId || selectedSessionId is not { } sessionId || isBusy) {
            return;
        }
        var generation = chatSession.Generation;
        var owner = ++effectOwner;
        var capturedTitle = title.Trim();
        isBusy = true;
        try {
            await WorkspaceService.RenameChatSessionAsync(agentId, sessionId, capturedTitle);
            if (!chatSession.IsCurrent(generation) || owner != effectOwner) {
                return;
            }
            await LoadWorkspaceAsync(agentId, sessionId);
            if (owner == effectOwner && selectedAgentId == agentId && selectedSessionId == sessionId) {
                SetMessage("Ready", "success", "Thread title updated.");
            }
        } catch (Exception exception) {
            LogOperationFailure(exception, agentId, sessionId, ActiveChatHandleId, "rename thread", false);
            if (owner == effectOwner && !isDisposed) {
                SetMessage("Attention", "danger", "The thread title could not be refreshed. Reload before trying again.");
            }
        } finally {
            if (owner == effectOwner) {
                isBusy = false;
            }
        }
    }

    private Task LoadWorkspaceAsync(Guid agentId, Guid? preferredSessionId, Guid? preferredExecutionRunId = null)
        => LoadTargetAsync(agentId, preferredSessionId, refreshCatalog: false, throwOnFailure: true,
            runId: preferredExecutionRunId, preserveEffects: true);

    private async Task LoadTargetAsync(Guid? agentId, Guid? sessionId, bool refreshCatalog, bool throwOnFailure,
        Guid? runId = null, bool preserveEffects = false, bool usePreferredAgent = false) {
        if (isDisposed) {
            return;
        }
        var changed = chatSession.DesiredAgentId != agentId || chatSession.DesiredSessionId != sessionId;
        var load = chatSession.LoadAsync(agentId, sessionId, IsFocusedFloating, usePreferredAgent ? PreferredAgent : null, refreshCatalog, runId);
        var generation = chatSession.Generation;
        if (changed && !preserveEffects) {
            ResetTargetEffects();
            await ResetVoiceOwnerAsync();
        }
        if (chatSession.IsCurrent(generation)) {
            await PublishAccessStateAsync(AgentChatContextAccessState.Loading);
        }
        var accepted = await load;
        if (!chatSession.IsCurrent(generation)) {
            return;
        }
        if (!CanUseSelectedAgentVoiceMode && HasVoiceOwnerActivity()) {
            await ResetVoiceOwnerAsync();
            if (!chatSession.IsCurrent(generation)) {
                return;
            }
        }
        await PublishAgentObservationAsync(generation);
        if (!chatSession.IsCurrent(generation)) {
            return;
        }
        if (accepted) {
            ResolveRunState();
            SynchronizeActiveChatRunState();
            await PublishAccessStateAsync(AgentChatContextAccessState.Ready);
        } else {
            await PublishAccessStateAsync(AgentChatContextAccessState.Failed);
            if (throwOnFailure && chatSession.IsCurrent(generation)) {
                throw new InvalidOperationException("The requested agent workspace could not be refreshed.");
            }
        }
    }

    private async Task PublishAccessStateAsync(AgentChatContextAccessState state) {
        if (publishedAccessState == state) {
            return;
        }

        publishedAccessState = state;
        await ContextAccessStateChanged.InvokeAsync(state);
    }

    private Task OpenAgentSwitchDialogAsync() {
        _ = HandleAgentSwitchDialogAsync();
        return Task.CompletedTask;
    }

    private async Task HandleAgentSwitchDialogAsync() {
        if (isDisposed || switchDialogOpen) {
            return;
        }
        var generation = chatSession.Generation;
        switchDialogOpen = true;
        try {
            await RefreshAgentCatalogAsync();
            if (!chatSession.IsCurrent(generation)) {
                return;
            }
            var result = await DialogService.OpenAsync<AgentSwitchDialog>(
                "Switch Agent",
                new Dictionary<string, object?> {
                    [nameof(AgentSwitchDialog.Agents)] = agents,
                    [nameof(AgentSwitchDialog.SelectedAgentId)] = selectedAgentId,
                    [nameof(AgentSwitchDialog.PrivateAgentIds)] = privateAgentIds,
                    [nameof(AgentSwitchDialog.FavoriteToggled)] =
                        (Func<AgentDefinition, Task<AgentDefinition>>)(agent => ToggleAgentFavoriteAsync(agent, generation))
                },
                new DialogOptions {
                    Eyebrow = "Agent threads",
                    Subtitle = "Choose which technical agent owns the thread list.",
                    Size = ModalSize.Wide,
                    DenseChrome = true,
                    TestId = "agent-switch-dialog-modal",
                    AriaLabel = "Switch chat agent"
                }, chatSession.TargetCancellation);

            if (!chatSession.IsCurrent(generation)) {
                return;
            }
            await RefreshAgentCatalogAsync();
            if (!chatSession.IsCurrent(generation) || result is not Guid agentId || agentId == selectedAgentId) {
                return;
            }

            await InvokeAsync(async () => {
                await SelectAgentAsync(agentId);
                StateHasChanged();
            });
        } catch (Exception exception) {
            if (chatSession.IsCurrent(generation)) {
                Logger.LogWarning("Agent switch failed. FailureType={FailureType}", exception.GetType().Name);
                await InvokeAsync(() => {
                    if (chatSession.IsCurrent(generation)) {
                        SetMessage("Attention", "danger", "The agent selection could not be updated.");
                        StateHasChanged();
                    }
                });
            }
        } finally {
            if (chatSession.IsCurrent(generation)) {
                switchDialogOpen = false;
            }
        }
    }

    private async Task RefreshAgentCatalogAsync() {
        var generation = chatSession.Generation;
        if (!await chatSession.RefreshCatalogAsync(generation) || !chatSession.IsCurrent(generation)) {
            return;
        }
        if (!CanUseSelectedAgentVoiceMode && HasVoiceOwnerActivity()) {
            await ResetVoiceOwnerAsync();
        }
        await PublishAgentObservationAsync(generation);
    }

    private async Task<AgentDefinition> ToggleAgentFavoriteAsync(AgentDefinition agent, long generation) {
        try {
            return await ToggleAgentFavoriteCoreAsync(agent, generation);
        } catch (Exception exception) {
            Logger.LogWarning("Agent favorite update failed. AgentId={AgentId} FailureType={FailureType}", agent.Id, exception.GetType().Name);
            throw new InvalidOperationException("The agent favorite could not be updated. Refresh before trying again.");
        }
    }

    private async Task<AgentDefinition> ToggleAgentFavoriteCoreAsync(AgentDefinition agent, long generation) {
        if (!chatSession.IsCurrent(generation)) {
            return agent;
        }
        var token = chatSession.TargetCancellation;
        var editor = await WorkspaceService.GetAgentEditorAsync(agent.Id, token);
        if (!chatSession.IsCurrent(generation)) {
            return agent;
        }
        if (editor.Id is null) {
            throw new InvalidOperationException("Agent was not found.");
        }

        if (editor.Tags.Any(AgentSpecialTags.IsFavorite)) {
            editor.Tags = editor.Tags
                .Where(item => !AgentSpecialTags.IsFavorite(item))
                .ToList();
        } else {
            editor.Tags = editor.Tags
                .Append(AgentSpecialTags.Favorite)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        await WorkspaceService.SaveAgentAsync(editor);
        if (!chatSession.IsCurrent(generation)) {
            return agent;
        }
        await RefreshAgentCatalogAsync();
        if (!chatSession.IsCurrent(generation)) {
            return agent;
        }
        return agents.FirstOrDefault(item => item.Id == agent.Id)
            ?? throw new InvalidOperationException("Agent was not found after saving favorite state.");
    }

    private Task OpenRuntimeDetailsDialogAsync() {
        if (!CanOpenRuntimeDetails) {
            SetMessage("Heads up", "warning", "Send a prompt first so runtime evidence can be opened.");
            return Task.CompletedTask;
        }

        _ = DialogService.OpenAsync<AgentRuntimeDetailsDialog>(
            "Runtime details",
            new Dictionary<string, object?> {
                [nameof(AgentRuntimeDetailsDialog.Run)] = workspace?.SelectedRun,
                [nameof(AgentRuntimeDetailsDialog.ExecutionLog)] = executionLog,
                [nameof(AgentRuntimeDetailsDialog.Metrics)] = metrics,
                [nameof(AgentRuntimeDetailsDialog.RunStateText)] = runStateText,
                [nameof(AgentRuntimeDetailsDialog.RunStateTone)] = runStateTone
            },
            new DialogOptions {
                Eyebrow = "Agent runtime",
                Subtitle = BuildRuntimeDialogSubtitle(),
                Size = ModalSize.Full,
                DenseChrome = true,
                TestId = "agent-runtime-details-dialog",
                AriaLabel = "Agent runtime details",
                Style = "max-height:calc(100vh - 2rem);"
            }, chatSession.TargetCancellation);

        return Task.CompletedTask;
    }

    private void HandleExecutionUpdated(object? sender, ExecutionLogEntry entry) {
        if (isDisposed || !ShouldAcceptExecutionEntry(entry)) {
            return;
        }

        _ = ObserveExecutionUpdateAsync(entry, chatSession.Generation);
    }

    private async Task ObserveExecutionUpdateAsync(ExecutionLogEntry entry, long generation) {
        try {
            await InvokeAsync(() => ApplyExecutionUpdateAsync(entry, generation));
        } catch (Exception exception) when (isDisposed) {
            Logger.LogDebug(
                exception,
                "Ignored an agent execution update after its chat panel was disposed. AgentId={AgentId} ChatSessionId={ChatSessionId} ExecutionRunId={ExecutionRunId} State={ExecutionState} FailureType={FailureType}.",
                entry.AgentId,
                entry.ChatSessionId,
                entry.ExecutionRunId,
                entry.State,
                exception.GetType().Name);
        } catch (Exception exception) {
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

    private async Task ApplyExecutionUpdateAsync(ExecutionLogEntry entry, long generation) {
        if (isDisposed || !ShouldAcceptExecutionEntry(entry)) {
            return;
        }

        if (!chatSession.TryObserveExecution(entry, generation)) {
            return;
        }
        if (workspace?.SelectedRun?.Id == entry.ExecutionRunId) {
            runStateText = entry.State.ToString();
            runStateTone = ResolveExecutionTone(entry.State);
            SynchronizeActiveChatRunState(entry.State);
        }

        StateHasChanged();
        if (!ShouldReloadWorkspaceAfterExternalTerminalUpdate(entry)) {
            return;
        }

        var refreshAgentId = selectedAgentId!.Value;
        var refreshSessionId = selectedSessionId!.Value;
        terminalWorkspaceRefreshRunId = entry.ExecutionRunId;
        try {
            await LoadWorkspaceAsync(refreshAgentId, refreshSessionId);
        } catch {
            if (terminalWorkspaceRefreshRunId == entry.ExecutionRunId) {
                terminalWorkspaceRefreshRunId = null;
            }

            throw;
        }

        if (!isDisposed &&
            selectedAgentId == refreshAgentId &&
            selectedSessionId == refreshSessionId) {
            StateHasChanged();
        }
    }

    private bool ShouldReloadWorkspaceAfterExternalTerminalUpdate(ExecutionLogEntry entry) {
        return entry.State is ExecutionState.Completed or ExecutionState.Failed &&
               entry.ExecutionRunId != Guid.Empty &&
               selectedAgentId.HasValue &&
               selectedSessionId.HasValue &&
               !isBusy &&
               trackedChatOperation.IsCompleted &&
               terminalWorkspaceRefreshRunId != entry.ExecutionRunId;
    }

    private bool ShouldAcceptExecutionEntry(ExecutionLogEntry entry) {
        if (selectedAgentId != entry.AgentId) {
            return false;
        }

        return !selectedSessionId.HasValue ||
               entry.ChatSessionId == selectedSessionId.Value;
    }

    private string BuildPromptWithAttachments() {
        if (draftAttachmentPaths.Count == 0) {
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

    private bool HasVoiceOwnerActivity() {
        lock (voiceOwnerGate) {
            return hasBrowserVoiceOwner ||
                   isVoiceModeEnabled ||
                   isVoiceRecording ||
                   isVoiceTranscribing ||
                   isVoiceSpeaking;
        }
    }

    private bool TryBeginVoiceOperation(out VoiceOperation operation) {
        lock (voiceOwnerGate) {
            if (isDisposed) {
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

    private bool IsVoiceOperationCurrent(VoiceOperation operation) {
        lock (voiceOwnerGate) {
            return !isDisposed &&
                   !operation.CancellationToken.IsCancellationRequested &&
                   operation.OwnerId == voiceOwnerId &&
                   operation.Generation == voiceOwnerGeneration &&
                   operation.AgentId == selectedAgentId;
        }
    }

    private async Task ResetVoiceOwnerAsync(bool disableVoiceMode = true) {
        string previousOwnerId;
        CancellationTokenSource previousCancellation;
        bool shouldDisposeBrowserOwner;
        lock (voiceOwnerGate) {
            if (isDisposed) {
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
            if (disableVoiceMode) {
                isVoiceModeEnabled = false;
            }
        }

        previousCancellation.Cancel();
        previousCancellation.Dispose();
        if (shouldDisposeBrowserOwner) {
            await DisposeVoiceOwnerInBrowserAsync(previousOwnerId);
        }
    }

    private async Task DisposeVoiceOwnerInBrowserAsync(string ownerId) {
        try {
            await JsRuntime.InvokeVoidAsync(
                "CanDoItAll.agentFramework.voice.disposeOwner",
                ownerId);
        } catch (JSDisconnectedException exception) {
            Logger.LogDebug(
                exception,
                "Voice owner cleanup skipped because the browser circuit disconnected. VoiceOwnerId={VoiceOwnerId}.",
                ownerId);
        } catch (JSException exception) {
            Logger.LogWarning(
                exception,
                "Voice owner cleanup failed in browser interop. VoiceOwnerId={VoiceOwnerId} FailureType={FailureType}.",
                ownerId,
                exception.GetType().Name);
        } catch (InvalidOperationException exception) {
            Logger.LogDebug(
                exception,
                "Voice owner cleanup was unavailable. VoiceOwnerId={VoiceOwnerId}.",
                ownerId);
        }
    }

    private async Task HandleVoiceModeChangedAsync(bool enabled) {
        if (isDisposed) {
            return;
        }

        if (!enabled) {
            await ResetVoiceOwnerAsync();
            if (!isDisposed) {
                SetVoiceStatus("Audio off", "neutral");
            }

            return;
        }

        if (!CanUseSelectedAgentVoiceMode) {
            await ResetVoiceOwnerAsync();
            if (isDisposed) {
                return;
            }

            SetVoiceStatus("Voice denied", "warning", "This agent does not allow voice mode.");
            return;
        }

        isVoiceModeEnabled = true;
        SetVoiceStatus("Audio on", "primary");
    }

    private async Task ToggleVoiceRecordingAsync() {
        if (isDisposed) {
            return;
        }

        if (!CanUseSelectedAgentVoiceMode) {
            SetVoiceStatus("Voice denied", "warning", "This agent does not allow voice mode.");
            return;
        }

        if (!isVoiceRecording) {
            if (!TryBeginVoiceOperation(out var operation)) {
                return;
            }

            try {
                await JsRuntime.InvokeVoidAsync(
                    "CanDoItAll.agentFramework.voice.startRecordingForOwner",
                    operation.CancellationToken,
                    operation.OwnerId);
                if (!IsVoiceOperationCurrent(operation)) {
                    return;
                }

                isVoiceModeEnabled = true;
                isVoiceRecording = true;
                SetVoiceStatus("Recording", "danger");
            } catch (Exception) when (!IsVoiceOperationCurrent(operation)) {
            } catch (Exception exception) {
                LogVoiceFailure(exception, "record");
                SetVoiceStatus("Record failed", "danger", "Browser recording could not be started.");
            }

            return;
        }

        await StopRecordingAndSendAsync();
    }

    private async Task StopRecordingAndSendAsync() {
        if (isDisposed) {
            return;
        }

        isVoiceRecording = false;
        isVoiceTranscribing = true;
        SetVoiceStatus("Transcribing", "info");
        if (!TryBeginVoiceOperation(out var operation)) {
            return;
        }

        try {
            var recording = await JsRuntime.InvokeAsync<BrowserVoiceRecording>(
                "CanDoItAll.agentFramework.voice.stopRecordingForOwner",
                operation.CancellationToken,
                operation.OwnerId);
            var result = await VoiceService.TranscribeAsync(
                recording.ToTranscriptionRequest(),
                operation.CancellationToken);
            if (!IsVoiceOperationCurrent(operation)) {
                return;
            }

            draftPrompt = result.Text;
            composerKey++;
            SetVoiceStatus("Sending", "info");
            if (!IsVoiceOperationCurrent(operation)) {
                return;
            }

            await SendMessageAsync();
        } catch (Exception) when (!IsVoiceOperationCurrent(operation)) {
        } catch (Exception exception) {
            LogVoiceFailure(exception, "transcribe");
            SetVoiceStatus("Voice failed", "danger", "The recording could not be transcribed.");
        } finally {
            if (IsVoiceOperationCurrent(operation)) {
                isVoiceTranscribing = false;
            }
        }
    }

    private Task SpeakLatestAssistantMessageAsync() {
        if (isDisposed) {
            return Task.CompletedTask;
        }

        var latestAssistantMessage = workspace?.SelectedSession?.Messages
            .Where(message => message.Role == ChatMessageRole.Assistant)
            .OrderByDescending(message => message.CreatedAtUtc)
            .FirstOrDefault();
        if (latestAssistantMessage is null || string.IsNullOrWhiteSpace(latestAssistantMessage.Content)) {
            SetVoiceStatus("Nothing to speak", "warning", "No assistant message is available.");
            return Task.CompletedTask;
        }

        return SpeakTextAsync(latestAssistantMessage.Content);
    }

    private async Task SpeakTextAsync(string text) {
        if (isDisposed) {
            return;
        }

        if (selectedAgent is null) {
            SetVoiceStatus("No agent", "warning", "Select an agent before using text-to-speech.");
            return;
        }

        if (!TryBeginVoiceOperation(out var operation)) {
            return;
        }

        var voiceAccess = SelectedAgentVoiceAccess;
        var suppressIdentifierOmissionNotice = ShouldSuppressIdentifierOmissionNotice();

        isVoiceSpeaking = true;
        SetVoiceStatus("Speaking", "primary");
        try {
            await JsRuntime.InvokeVoidAsync(
                "CanDoItAll.agentFramework.voice.clearAudioQueueForOwner",
                operation.CancellationToken,
                operation.OwnerId);
            if (!IsVoiceOperationCurrent(operation)) {
                return;
            }

            var queuedChunks = 0;
            await foreach (var synthesis in VoiceService.SynthesizeChunksAsync(new AgentVoiceSynthesisRequest(
                               text,
                               voiceAccess,
                               SuppressIdentifierOmissionNotice: suppressIdentifierOmissionNotice),
                               operation.CancellationToken)) {
                if (!IsVoiceOperationCurrent(operation)) {
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
                if (!IsVoiceOperationCurrent(operation)) {
                    return;
                }

                if (queuedChunks == 1) {
                    SetVoiceStatus("Playing", "primary");
                }
            }

            if (IsVoiceOperationCurrent(operation)) {
                SetVoiceStatus(queuedChunks == 1 ? "Audio ready" : $"Audio ready ({queuedChunks} chunks)", "success");
            }
        } catch (Exception) when (!IsVoiceOperationCurrent(operation)) {
        } catch (Exception exception) {
            LogVoiceFailure(exception, "speak");
            SetVoiceStatus("Speak failed", "danger", "The response audio could not be played.");
        } finally {
            if (IsVoiceOperationCurrent(operation)) {
                isVoiceSpeaking = false;
            }
        }
    }

    private bool ShouldSuppressIdentifierOmissionNotice() {
        return selectedSessionId is { } sessionId
            ? sessionsWithVoiceIdentifierOmissionNotice.Contains(sessionId)
            : hasVoiceIdentifierOmissionNoticeWithoutSession;
    }

    private void TrackIdentifierOmissionNotice(AgentVoiceSynthesisResult synthesis) {
        if (!synthesis.IdentifierOmissionNoticeIncluded) {
            return;
        }

        if (selectedSessionId is { } sessionId) {
            sessionsWithVoiceIdentifierOmissionNotice.Add(sessionId);
            return;
        }

        hasVoiceIdentifierOmissionNoticeWithoutSession = true;
    }

    private void LogVoiceFailure(Exception exception, string operation)
        => Logger.LogWarning("Agent chat voice {Operation} failed. AgentId={AgentId} FailureType={FailureType}", operation, selectedAgentId, exception.GetType().Name);

    private void SetVoiceStatus(string text, string tone, string? notification = null) {
        voiceStatusText = text;
        voiceStatusTone = tone;
        if (!string.IsNullOrWhiteSpace(notification)) {
            NotificationService.Warning(text, notification);
        }
    }

    private void ResolveRunState() {
        if (workspace?.SelectedRun is null) {
            runStateText = string.Empty;
            runStateTone = "neutral";
            return;
        }

        var run = workspace.SelectedRun;
        runStateText = run.State.ToString();
        runStateTone = run.State switch {
            ExecutionState.Completed => "success",
            ExecutionState.WaitingOnTool => "warning",
            ExecutionState.Failed => "danger",
            _ => "info"
        };
    }

    private void SynchronizeActiveChatRunState(ExecutionState? executionState = null) {
        if (!ActiveChatHandleId.HasValue) {
            return;
        }

        var effectiveState = executionState ?? workspace?.SelectedRun?.State;
        var activeState = effectiveState switch {
            ExecutionState.Preparing or
            ExecutionState.Running or
            ExecutionState.Persisting => ActiveAgentChatRunState.Running,
            ExecutionState.WaitingOnTool => ActiveAgentChatRunState.AwaitingApproval,
            _ => ActiveAgentChatRunState.Idle
        };
        SetActiveChatRunState(activeState);
    }

    private void SetActiveChatRunState(ActiveAgentChatRunState runState) {
        if (ActiveChatHandleId is { } handleId) {
            FloatingChatCoordinator.SetRunState(handleId, runState);
        }
    }

    private void ReconcileActiveChatRunState(AgentChatHandleId? handleId) {
        if (!handleId.HasValue) {
            return;
        }

        try {
            FloatingChatCoordinator.ReconcileRunStateAfterOperation(handleId.Value);
        } catch (ObjectDisposedException) {
        }
    }

    private static string ResolveExecutionTone(ExecutionState state) {
        return state switch {
            ExecutionState.Completed => "success",
            ExecutionState.WaitingOnTool => "warning",
            ExecutionState.Failed => "danger",
            _ => "info"
        };
    }

    private void SetMessage(string label, string tone, string value) {
        switch (tone) {
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

    private string BuildRuntimeDialogSubtitle() {
        var agentName = selectedAgent?.Name ?? "Selected agent";
        var threadTitle = workspace?.SelectedSession?.Title ?? "No thread selected";
        return $"{agentName} / {threadTitle}";
    }

    private static string ResolveAgentInitials(AgentDefinition agent) {
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

    public async ValueTask DisposeAsync() {
        string ownerId;
        CancellationTokenSource cancellation;
        bool shouldDisposeBrowserOwner;
        lock (voiceOwnerGate) {
            if (isDisposed) {
                return;
            }

            isDisposed = true;
            chatSession.Dispose();
            effectOwner++;
            attachmentRead?.Cancel();
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
        try {
            cancellation.Cancel();
            if (shouldDisposeBrowserOwner) {
                await DisposeVoiceOwnerInBrowserAsync(ownerId);
            }
        } finally {
            cancellation.Dispose();
        }
    }

    private readonly record struct VoiceOperation(
        string OwnerId,
        long Generation,
        Guid? AgentId,
        CancellationToken CancellationToken);
}
