using CanDoItAll.AgentFramework.Voice;
using CanDoItAll.Components.Common;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace CanDoItAll.Modules.CognitiveMemory.Pages;

public partial class CognitiveMemoryPage
{
    private const int MaximumCuratorSpeechCharacters = 1600;

    [Inject]
    public ICognitiveMemoryCuratorConversationService CuratorConversationService { get; set; } = default!;

    internal Guid? activeCuratorSessionId;
    internal CognitiveMemoryCuratorRuntimeMode curatorRuntimeMode = CognitiveMemoryCuratorRuntimeMode.DirectLlm;
    internal CognitiveMemoryCuratorConversationDepth curatorConversationDepth = CognitiveMemoryCuratorConversationDepth.Medium;
    internal CognitiveMemoryCuratorRuntimeMode? activeCuratorSessionMode;
    internal CognitiveMemoryCuratorConversationDepth? activeCuratorSessionDepth;
    internal string activeCuratorSessionTitle = string.Empty;
    internal string curatorSessionTitle = "Cognitive Memory curator";
    internal string curatorMessage = string.Empty;
    internal string curatorStatus = "Ready.";
    internal string curatorVoiceStatus = "Audio ready.";
    internal string curatorVoiceStatusTone = "neutral";
    internal bool curatorVoiceModeEnabled;
    internal bool curatorVoiceRecording;
    internal bool curatorVoiceTranscribing;
    internal bool curatorVoiceSpeaking;
    internal CognitiveMemoryCuratorSendResult? lastCuratorSendResult;
    internal readonly List<CognitiveMemoryCuratorTurnViewModel> curatorTurns = [];
    internal readonly HashSet<Guid> curatorSessionsWithVoiceIdentifierOmissionNotice = [];
    internal bool hasCuratorVoiceIdentifierOmissionNoticeWithoutSession;

    internal bool HasActiveCuratorSession
        => activeCuratorSessionId.HasValue &&
           activeCuratorSessionMode == curatorRuntimeMode &&
           activeCuratorSessionDepth == curatorConversationDepth;

    internal bool CanStartCurator
        => !isBusy && ProjectId is { } projectId && projectId != Guid.Empty;

    internal bool CanSendCurator
        => !isBusy &&
           ProjectId is { } projectId &&
           projectId != Guid.Empty &&
           !string.IsNullOrWhiteSpace(curatorMessage);

    internal bool CanUseCuratorVoice
        => !isBusy && !curatorVoiceTranscribing && !curatorVoiceSpeaking;

    internal string CuratorProjectScopeText
        => ProjectId is { } projectId && projectId != Guid.Empty
            ? projectId.ToString("D")
            : "No project selected";

    internal string ActiveCuratorSessionTitle
        => activeCuratorSessionId is { } sessionId
            ? FirstNonEmpty(activeCuratorSessionTitle, $"Session {FormatShortId(sessionId)}")
            : "No active session";

    internal string ActiveCuratorSessionMeta
        => activeCuratorSessionId is null
            ? "Start a curator session before talking."
            : activeCuratorSessionMode != curatorRuntimeMode
                ? "Runtime mode changed; next turn starts a new session."
                : activeCuratorSessionDepth != curatorConversationDepth
                    ? "Response length changed; next turn starts a new session."
                    : $"{FormatLabel(curatorRuntimeMode)} / {FormatLabel(curatorConversationDepth)} / {curatorTurns.Count} turn(s)";

    internal async Task StartCuratorSessionAsync()
    {
        if (isBusy)
        {
            return;
        }

        if (!TryResolveCuratorProjectId(out var projectId))
        {
            return;
        }

        isBusy = true;
        errorMessage = string.Empty;
        curatorStatus = "Starting curator session.";

        try
        {
            var session = await CreateCuratorSessionAsync(projectId);
            ActivateCuratorSession(session, clearTranscript: true);
            curatorStatus = $"Active: {session.Title}";
            NotificationService.Success("Curator session started", session.Title);
        }
        catch (Exception exception)
        {
            curatorStatus = exception.Message;
            errorMessage = exception.Message;
            NotificationService.Error("Curator session failed", exception.Message);
        }
        finally
        {
            isBusy = false;
        }
    }

    internal Task SendCuratorMessageFromUiAsync()
        => SendCuratorMessageAsync(speakResponse: curatorVoiceModeEnabled);

    internal async Task SendCuratorMessageAsync(bool speakResponse)
    {
        if (isBusy)
        {
            return;
        }

        if (!TryResolveCuratorProjectId(out var projectId))
        {
            return;
        }

        var message = curatorMessage.Trim();
        if (string.IsNullOrWhiteSpace(message))
        {
            curatorStatus = "Message is required.";
            NotificationService.Warning("Curator message required", curatorStatus);
            return;
        }

        isBusy = true;
        errorMessage = string.Empty;
        curatorStatus = "Asking curator.";

        try
        {
            if (activeCuratorSessionId is null ||
                activeCuratorSessionMode != curatorRuntimeMode ||
                activeCuratorSessionDepth != curatorConversationDepth)
            {
                var session = await CreateCuratorSessionAsync(projectId);
                ActivateCuratorSession(session, clearTranscript: true);
            }

            var sessionId = activeCuratorSessionId
                ?? throw new InvalidOperationException("Curator session was not initialized.");
            var result = await CuratorConversationService.SendAsync(new CognitiveMemoryCuratorSendRequest(
                sessionId,
                message,
                Intent: CognitiveMemoryRecallIntentKind.Implementation,
                ConversationDepth: curatorConversationDepth));
            lastCuratorSendResult = result;
            activeCuratorSessionId = result.Session.Id;
            activeCuratorSessionMode = result.Session.RuntimeMode;
            activeCuratorSessionDepth = result.Turn.ConversationDepth;
            activeCuratorSessionTitle = result.Session.Title;
            selectedRecallTraceId = result.RecallTraceId;
            curatorTurns.Add(CognitiveMemoryCuratorTurnViewModel.FromResult(result));
            curatorMessage = string.Empty;
            var completedStatus = CreateCuratorStatus(result);
            await ReloadSnapshotAsync();
            curatorStatus = completedStatus;
            NotificationService.Success("Curator answered", curatorStatus);

            if (speakResponse)
            {
                await SpeakCuratorTextAsync(BuildCuratorResponseSpeech(result));
            }
        }
        catch (Exception exception)
        {
            curatorStatus = exception.Message;
            errorMessage = exception.Message;
            NotificationService.Error("Curator conversation failed", exception.Message);
        }
        finally
        {
            isBusy = false;
        }
    }

    internal Task ToggleCuratorVoiceModeAsync()
    {
        curatorVoiceModeEnabled = !curatorVoiceModeEnabled;
        SetCuratorVoiceStatus(curatorVoiceModeEnabled ? "Audio on" : "Audio off", curatorVoiceModeEnabled ? "primary" : "neutral");
        return Task.CompletedTask;
    }

    internal async Task ToggleCuratorRecordingAsync()
    {
        if (!curatorVoiceRecording)
        {
            try
            {
                await JsRuntime.InvokeVoidAsync("CanDoItAll.agentFramework.voice.startRecording");
                curatorVoiceModeEnabled = true;
                curatorVoiceRecording = true;
                SetCuratorVoiceStatus("Recording", "danger");
            }
            catch (Exception exception)
            {
                SetCuratorVoiceStatus("Record failed", "danger");
                NotificationService.Error("Curator voice failed", exception.Message);
            }

            return;
        }

        await StopCuratorRecordingAsync();
    }

    internal async Task StopCuratorRecordingAsync()
    {
        curatorVoiceRecording = false;
        curatorVoiceTranscribing = true;
        SetCuratorVoiceStatus("Transcribing", "info");

        try
        {
            var recording = await JsRuntime.InvokeAsync<BrowserVoiceRecording>(
                "CanDoItAll.agentFramework.voice.stopRecording");
            var transcription = await VoiceService.TranscribeAsync(recording.ToTranscriptionRequest());
            await HandleCuratorVoiceTranscriptAsync(transcription.Text);
        }
        catch (Exception exception)
        {
            SetCuratorVoiceStatus("Voice failed", "danger");
            NotificationService.Error("Curator voice failed", exception.Message);
        }
        finally
        {
            curatorVoiceTranscribing = false;
        }
    }

    internal async Task HandleCuratorVoiceTranscriptAsync(string transcript)
    {
        curatorMessage = transcript.Trim();
        if (string.IsNullOrWhiteSpace(curatorMessage))
        {
            SetCuratorVoiceStatus("No speech", "warning");
            return;
        }

        SetCuratorVoiceStatus("Sending", "info");
        await SendCuratorMessageAsync(speakResponse: true);
    }

    internal async Task SpeakCuratorTextAsync(string text)
    {
        curatorVoiceSpeaking = true;
        try
        {
            await JsRuntime.InvokeVoidAsync("CanDoItAll.agentFramework.voice.clearAudioQueue");
            var queuedChunks = 0;
            await foreach (var synthesis in VoiceService.SynthesizeChunksAsync(new AgentVoiceSynthesisRequest(
                               text,
                               SuppressIdentifierOmissionNotice: ShouldSuppressCuratorIdentifierOmissionNotice())))
            {
                TrackCuratorIdentifierOmissionNotice(synthesis);
                queuedChunks++;
                await JsRuntime.InvokeVoidAsync(
                    "CanDoItAll.agentFramework.voice.enqueueAudio",
                    Convert.ToBase64String(synthesis.AudioBytes),
                    synthesis.ContentType);
                if (queuedChunks == 1)
                {
                    SetCuratorVoiceStatus("Playing", "primary");
                }
            }

            SetCuratorVoiceStatus(queuedChunks == 1 ? "Audio ready" : $"Audio ready ({queuedChunks} chunks)", "success");
        }
        catch (Exception exception)
        {
            SetCuratorVoiceStatus("Speak failed", "danger");
            NotificationService.Error("Curator voice failed", exception.Message);
        }
        finally
        {
            curatorVoiceSpeaking = false;
        }
    }

    internal bool ShouldSuppressCuratorIdentifierOmissionNotice()
    {
        return activeCuratorSessionId is { } sessionId
            ? curatorSessionsWithVoiceIdentifierOmissionNotice.Contains(sessionId)
            : hasCuratorVoiceIdentifierOmissionNoticeWithoutSession;
    }

    internal void TrackCuratorIdentifierOmissionNotice(AgentVoiceSynthesisResult synthesis)
    {
        if (!synthesis.IdentifierOmissionNoticeIncluded)
        {
            return;
        }

        if (activeCuratorSessionId is { } sessionId)
        {
            curatorSessionsWithVoiceIdentifierOmissionNotice.Add(sessionId);
            return;
        }

        hasCuratorVoiceIdentifierOmissionNoticeWithoutSession = true;
    }

    internal void SetCuratorVoiceStatus(string text, string tone)
    {
        curatorVoiceStatus = text;
        curatorVoiceStatusTone = tone;
    }

    internal async Task<CognitiveMemoryCuratorSessionRecord> CreateCuratorSessionAsync(Guid projectId)
        => await CuratorConversationService.StartAsync(new CognitiveMemoryCuratorSessionStartRequest(
            projectId,
            string.IsNullOrWhiteSpace(curatorSessionTitle) ? "Cognitive Memory curator" : curatorSessionTitle.Trim(),
            CreateCuratorPolicyContext(projectId),
            curatorRuntimeMode,
            curatorConversationDepth));

    internal void ActivateCuratorSession(CognitiveMemoryCuratorSessionRecord session, bool clearTranscript)
    {
        activeCuratorSessionId = session.Id;
        activeCuratorSessionMode = session.RuntimeMode;
        activeCuratorSessionDepth = session.ConversationDepth;
        activeCuratorSessionTitle = session.Title;
        lastCuratorSendResult = null;
        selectedRecallTraceId = null;
        if (clearTranscript)
        {
            curatorTurns.Clear();
        }
    }

    internal bool TryResolveCuratorProjectId(out Guid projectId)
    {
        if (ProjectId is { } resolvedProjectId && resolvedProjectId != Guid.Empty)
        {
            projectId = resolvedProjectId;
            return true;
        }

        projectId = Guid.Empty;
        curatorStatus = "Open the page with a projectId query parameter before curator chat.";
        NotificationService.Warning("Project scope required", curatorStatus);
        return false;
    }

    internal static CognitiveMemoryPolicyContext CreateCuratorPolicyContext(Guid projectId)
        => new(
            projectId,
            OperatorActorId,
            CognitiveMemoryAccessLevel.Project,
            new CognitiveMemoryPolicyProfileId("cognitive-memory-curator-ui"),
            CognitiveMemoryRiskLevel.Medium,
            AllowRestrictedContent: false);

    internal static string CreateCuratorStatus(CognitiveMemoryCuratorSendResult result)
    {
        var captureText = result.CapturedImprovements.Count == 1
            ? "1 trusted capture"
            : $"{result.CapturedImprovements.Count} trusted captures";
        var includedText = result.IncludedMemoryRecordIds.Count == 1
            ? "1 included memory"
            : $"{result.IncludedMemoryRecordIds.Count} included memories";
        return $"Turn {result.Turn.Sequence}: {captureText}, {includedText}.";
    }

    internal static string BuildCuratorResponseSpeech(CognitiveMemoryCuratorSendResult result)
    {
        var response = NormalizeCuratorSpeech(result.ResponseText);
        if (response.Length > MaximumCuratorSpeechCharacters)
        {
            response = response[..MaximumCuratorSpeechCharacters].TrimEnd();
        }

        if (result.CapturedImprovements.Count == 0)
        {
            return response;
        }

        var captureText = result.CapturedImprovements.Count == 1
            ? "I captured one trusted memory improvement."
            : $"I captured {result.CapturedImprovements.Count} trusted memory improvements.";
        return $"{response} {captureText}";
    }

    internal static string NormalizeCuratorSpeech(string text)
        => string.Join(
            ' ',
            text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

    internal static string CuratorCaptureTone(CognitiveMemoryCuratorCaptureStatus status)
        => status switch
        {
            CognitiveMemoryCuratorCaptureStatus.Applied => "success",
            _ => "warning"
        };

    internal sealed record CognitiveMemoryCuratorTurnViewModel(
        int Sequence,
        string UserMessage,
        string CuratorResponse,
        CognitiveMemoryCuratorRuntimeMode RuntimeMode,
        CognitiveMemoryCuratorConversationDepth ConversationDepth,
        Guid? RecallTraceId,
        Guid? ContextPackId,
        int IncludedMemoryRecordCount,
        IReadOnlyList<CognitiveMemoryCuratorCaptureViewModel> Captures,
        IReadOnlyList<string> Warnings,
        DateTimeOffset CreatedAtUtc)
    {
        public static CognitiveMemoryCuratorTurnViewModel FromResult(CognitiveMemoryCuratorSendResult result)
            => new(
                result.Turn.Sequence,
                result.Turn.UserMessage,
                result.ResponseText,
                result.RuntimeMode,
                result.Turn.ConversationDepth,
                result.RecallTraceId,
                result.ContextPackId,
                result.IncludedMemoryRecordIds.Count,
                result.CapturedImprovements.Select(CognitiveMemoryCuratorCaptureViewModel.FromRecord).ToArray(),
                result.Warnings,
                result.Turn.CreatedAtUtc);
    }

    internal sealed record CognitiveMemoryCuratorCaptureViewModel(
        CognitiveMemoryCuratorCaptureKind CaptureKind,
        CognitiveMemoryCuratorCaptureStatus Status,
        string Summary,
        Guid? AppliedMemoryRecordId,
        double ConfidenceScore,
        double PriorityScore)
    {
        public static CognitiveMemoryCuratorCaptureViewModel FromRecord(CognitiveMemoryCuratorCapturedImprovementRecord record)
            => new(
                record.CaptureKind,
                record.Status,
                record.Summary,
                record.AppliedMemoryRecordId,
                record.ConfidenceScore,
                record.PriorityScore);
    }
}
