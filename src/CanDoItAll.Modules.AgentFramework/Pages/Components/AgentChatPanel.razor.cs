using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Components;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Voice;
using CanDoItAll.Components.BaseLib;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace CanDoItAll.Modules.AgentFramework.Pages.Components;

public partial class AgentChatPanel : IAsyncDisposable
{
    private const string AgentThreadsHelpText =
        "Search and select threads for the active technical agent. Use Switch Agent when you need another agent's thread list.";

    [Parameter]
    public Guid? PreferredAgentId { get; set; }

    [Inject]
    public IAgentFrameworkWorkspaceService WorkspaceService { get; set; } = default!;

    [Inject]
    public IAgentVoiceService VoiceService { get; set; } = default!;

    [Inject]
    public IJSRuntime JsRuntime { get; set; } = default!;

    [Inject]
    public DialogService DialogService { get; set; } = default!;

    [Inject]
    public NotificationService NotificationService { get; set; } = default!;

    private IReadOnlyList<AgentDefinition> agents = [];
    private IReadOnlyCollection<Guid> privateAgentIds = [];
    private ChatAgentWorkspaceSnapshot? workspace;
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
    private string voiceStatusText = string.Empty;
    private string voiceStatusTone = "neutral";
    private readonly HashSet<Guid> sessionsWithVoiceIdentifierOmissionNotice = [];
    private bool hasVoiceIdentifierOmissionNoticeWithoutSession;

    private IReadOnlyList<ChatSessionSummaryRecord> FilteredSessions
        => workspace?.Sessions
            .Where(MatchesThreadSearch)
            .OrderByDescending(item => item.UpdatedAtUtc)
            .ToList() ?? [];

    private bool CanOpenRuntimeDetails
        => workspace?.SelectedRun is not null ||
           executionLog.Count > 0 ||
           metrics.Count > 0;

    private AgentVoiceAccessSettings SelectedAgentVoiceAccess
        => selectedAgent is null
            ? new AgentVoiceAccessSettings()
            : AgentVoiceAccessMetadata.Read(selectedAgent.ConfigurationJson);

    private bool CanUseSelectedAgentVoiceMode
        => SelectedAgentVoiceAccess.CanUseVoiceMode;

    protected override async Task OnInitializedAsync()
    {
        WorkspaceService.ExecutionUpdated += HandleExecutionUpdated;
        await LoadAsync();
    }

    protected override async Task OnParametersSetAsync()
    {
        if (PreferredAgentId.HasValue &&
            PreferredAgentId != selectedAgentId &&
            agents.Any(item => item.Id == PreferredAgentId.Value))
        {
            await SelectAgentAsync(PreferredAgentId.Value);
        }
    }

    private async Task LoadAsync()
    {
        agents = await WorkspaceService.ListAgentsAsync(includeTemplates: false);
        if (agents.Count == 0)
        {
            workspace = null;
            selectedAgent = null;
            selectedAgentId = null;
            selectedSessionId = null;
            executionLog = [];
            metrics = [];
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

    private Task HandleThreadSearchChangedAsync(string? value)
    {
        threadSearchText = value ?? string.Empty;
        return Task.CompletedTask;
    }

    private async Task SendMessageAsync()
    {
        if (!selectedAgentId.HasValue)
        {
            SetMessage("Heads up", "warning", "Select a technical agent before sending a prompt.");
            return;
        }

        var prompt = BuildPromptWithAttachments();
        if (string.IsNullOrWhiteSpace(prompt))
        {
            SetMessage("Heads up", "warning", "Enter a prompt before sending it.");
            return;
        }

        isBusy = true;
        pendingUserPrompt = draftPrompt;
        var previousDraft = draftPrompt;
        draftPrompt = string.Empty;
        composerKey++;
        try
        {
            var result = await WorkspaceService.SendMessageAsync(
                selectedAgentId.Value,
                selectedSessionId,
                prompt);
            draftAttachmentPaths = [];
            await LoadWorkspaceAsync(selectedAgentId.Value, result.ChatSessionId);
            SetMessage("Ready", "success", "Prompt sent through the integrated runtime.");
            if (isVoiceModeEnabled && !string.IsNullOrWhiteSpace(result.AssistantMessage.Content))
            {
                await SpeakTextAsync(result.AssistantMessage.Content);
            }
        }
        catch (Exception exception)
        {
            draftPrompt = previousDraft;
            composerKey++;
            SetMessage("Attention", "danger", exception.Message);
        }
        finally
        {
            pendingUserPrompt = string.Empty;
            isBusy = false;
        }
    }

    private async Task HandleApprovalDecisionAsync(bool approved)
    {
        await ContinueApprovalAsync(approved, autoApprovePendingToolCalls: false);
    }

    private async Task ApproveConversationAsync()
    {
        await ContinueApprovalAsync(approved: true, autoApprovePendingToolCalls: true);
    }

    private async Task ContinueApprovalAsync(bool approved, bool autoApprovePendingToolCalls)
    {
        if (!selectedAgentId.HasValue || !selectedSessionId.HasValue)
        {
            return;
        }

        isBusy = true;
        try
        {
            await WorkspaceService.RespondToPendingApprovalsAsync(
                selectedAgentId.Value,
                selectedSessionId.Value,
                approved,
                autoApprovePendingToolCalls);
            await LoadWorkspaceAsync(selectedAgentId.Value, selectedSessionId.Value);
            SetMessage(
                approved ? "Ready" : "Heads up",
                approved ? "success" : "warning",
                approved
                    ? autoApprovePendingToolCalls
                        ? "Approval resumed the run and enabled remaining approvals for the active execution."
                        : "Approval resumed the run."
                    : "Approval was rejected and the thread was refreshed.");
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
        selectedAgentId = agentId;
        selectedAgent = agents.FirstOrDefault(item => item.Id == agentId);
        workspace = await WorkspaceService.GetChatAgentWorkspaceAsync(agentId, preferredSessionId);
        selectedSessionId = workspace.SelectedSessionId;
        if (!CanUseSelectedAgentVoiceMode)
        {
            isVoiceModeEnabled = false;
            isVoiceRecording = false;
        }

        var runtimeSnapshot = await WorkspaceService.GetChatRuntimeSnapshotAsync(agentId, workspace.SelectedSessionId);
        executionLog = runtimeSnapshot.ExecutionLog;
        metrics = runtimeSnapshot.Metrics;
        ResolveRunState();
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
        var privateProviderIds = (await providersTask)
            .Where(provider => provider.IsPrivateProvider)
            .Select(provider => provider.Id)
            .ToHashSet();
        privateAgentIds = agents
            .Where(agent => agent.ProviderProfileId.HasValue && privateProviderIds.Contains(agent.ProviderProfileId.Value))
            .Select(agent => agent.Id)
            .ToHashSet();
        if (selectedAgentId is { } currentAgentId)
        {
            selectedAgent = agents.FirstOrDefault(item => item.Id == currentAgentId);
        }
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
        if (!ShouldAcceptExecutionEntry(entry))
        {
            return;
        }

        _ = InvokeAsync(() =>
        {
            if (!ShouldAcceptExecutionEntry(entry))
            {
                return;
            }

            executionLog = UpsertExecutionLogEntry(executionLog, entry);
            if (workspace?.SelectedRun?.Id == entry.ExecutionRunId)
            {
                runStateText = entry.State.ToString();
                runStateTone = ResolveExecutionTone(entry.State);
            }

            StateHasChanged();
        });
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

    private async Task HandleVoiceModeChangedAsync(bool enabled)
    {
        if (enabled && !CanUseSelectedAgentVoiceMode)
        {
            SetVoiceStatus("Voice denied", "warning", "This agent does not allow voice mode.");
            return;
        }

        isVoiceModeEnabled = enabled;
        SetVoiceStatus(enabled ? "Audio on" : "Audio off", enabled ? "primary" : "neutral");
        await Task.CompletedTask;
    }

    private async Task ToggleVoiceRecordingAsync()
    {
        if (!CanUseSelectedAgentVoiceMode)
        {
            SetVoiceStatus("Voice denied", "warning", "This agent does not allow voice mode.");
            return;
        }

        if (!isVoiceRecording)
        {
            try
            {
                await JsRuntime.InvokeVoidAsync("CanDoItAll.agentFramework.voice.startRecording");
                isVoiceModeEnabled = true;
                isVoiceRecording = true;
                SetVoiceStatus("Recording", "danger");
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
        isVoiceRecording = false;
        isVoiceTranscribing = true;
        SetVoiceStatus("Transcribing", "info");
        try
        {
            var recording = await JsRuntime.InvokeAsync<BrowserVoiceRecording>(
                "CanDoItAll.agentFramework.voice.stopRecording");
            var result = await VoiceService.TranscribeAsync(recording.ToTranscriptionRequest());

            draftPrompt = result.Text;
            composerKey++;
            SetVoiceStatus("Sending", "info");
            await SendMessageAsync();
        }
        catch (Exception exception)
        {
            SetVoiceStatus("Voice failed", "danger", exception.Message);
        }
        finally
        {
            isVoiceTranscribing = false;
        }
    }

    private Task SpeakLatestAssistantMessageAsync()
    {
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
        if (selectedAgent is null)
        {
            SetVoiceStatus("No agent", "warning", "Select an agent before using text-to-speech.");
            return;
        }

        isVoiceSpeaking = true;
        SetVoiceStatus("Speaking", "primary");
        try
        {
            await JsRuntime.InvokeVoidAsync("CanDoItAll.agentFramework.voice.clearAudioQueue");
            var queuedChunks = 0;
            await foreach (var synthesis in VoiceService.SynthesizeChunksAsync(new AgentVoiceSynthesisRequest(
                               text,
                               SelectedAgentVoiceAccess,
                               SuppressIdentifierOmissionNotice: ShouldSuppressIdentifierOmissionNotice())))
            {
                TrackIdentifierOmissionNotice(synthesis);
                queuedChunks++;
                await JsRuntime.InvokeVoidAsync(
                    "CanDoItAll.agentFramework.voice.enqueueAudio",
                    Convert.ToBase64String(synthesis.AudioBytes),
                    synthesis.ContentType);
                if (queuedChunks == 1)
                {
                    SetVoiceStatus("Playing", "primary");
                }
            }

            SetVoiceStatus(queuedChunks == 1 ? "Audio ready" : $"Audio ready ({queuedChunks} chunks)", "success");
        }
        catch (Exception exception)
        {
            SetVoiceStatus("Speak failed", "danger", exception.Message);
        }
        finally
        {
            isVoiceSpeaking = false;
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

    public ValueTask DisposeAsync()
    {
        WorkspaceService.ExecutionUpdated -= HandleExecutionUpdated;
        return ValueTask.CompletedTask;
    }
}
