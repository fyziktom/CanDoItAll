using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Voice;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Components.Common;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.JSInterop;
using System.Text;

namespace CanDoItAll.Modules.CognitiveMemory.Pages;

public partial class CognitiveMemoryPage
{
    internal async Task StartProbeSessionAsync()
    {
        if (isBusy)
        {
            return;
        }

        if (!TryResolveProbeProjectId(out var projectId))
        {
            return;
        }

        isBusy = true;
        errorMessage = string.Empty;
        probeStatus = "Starting probe session.";

        try
        {
            var session = await CreateProbeSessionAsync(projectId);
            activeProbeSessionId = session.Id;
            lastProbeAskResult = null;
            lastProbeFeedback = null;
            selectedRecallTraceId = null;
            probeStatus = $"Active: {session.Title}";
            NotificationService.Success("Probe session started", session.Title);
            await ReloadSnapshotAsync();
        }
        catch (Exception exception)
        {
            probeStatus = exception.Message;
            errorMessage = exception.Message;
            NotificationService.Error("Probe session failed", exception.Message);
        }
        finally
        {
            isBusy = false;
        }
    }

    internal void ReuseProbeSession(Guid sessionId)
    {
        activeProbeSessionId = sessionId;
        lastProbeAskResult = null;
        lastProbeFeedback = null;
        var session = snapshot?.ProbeSessions.FirstOrDefault(item => item.Id == sessionId);
        if (session is not null)
        {
            probeRecallMode = session.RecallMode;
            probeStatus = $"Reusing: {session.Title}";
        }
    }

    internal async Task AskProbeAsync()
    {
        if (isBusy)
        {
            return;
        }

        if (!TryResolveProbeProjectId(out var projectId))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(probeQuestion))
        {
            probeStatus = "Question is required.";
            NotificationService.Warning("Probe question required", probeStatus);
            return;
        }

        isBusy = true;
        errorMessage = string.Empty;
        probeStatus = "Asking memory.";

        try
        {
            if (activeProbeSessionId is null)
            {
                var session = await CreateProbeSessionAsync(projectId);
                activeProbeSessionId = session.Id;
            }

            var result = await ProbeService.AskAsync(new CognitiveMemoryProbeAskRequest(
                activeProbeSessionId.Value,
                probeQuestion,
                probeIntent,
                CreateProbeRecallBudget(),
                CreateProbeMetadata()));
            lastProbeAskResult = result;
            lastProbeFeedback = null;
            selectedRecallTraceId = result.RecallResult.TraceId;
            activeProbeSessionId = result.Session.Id;
            probeStatus = $"Answered turn {result.Turn.Sequence}: {result.RecallResult.ContextPack.SourceRefs.Count(item => item.IncludedInContext)} included source ref(s).";
            NotificationService.Success("Probe answered", $"Trace {FormatShortId(result.RecallResult.TraceId)}");
            await ReloadSnapshotAsync();
        }
        catch (Exception exception)
        {
            probeStatus = exception.Message;
            errorMessage = exception.Message;
            NotificationService.Error("Probe ask failed", exception.Message);
        }
        finally
        {
            isBusy = false;
        }
    }

    internal async Task SubmitProbeFeedbackAsync()
    {
        if (isBusy || lastProbeAskResult?.Turn is not { } turn)
        {
            return;
        }

        isBusy = true;
        errorMessage = string.Empty;
        probeStatus = "Recording feedback.";

        try
        {
            var feedback = await ProbeService.RecordFeedbackAsync(new CognitiveMemoryProbeFeedbackRequest(
                turn.Id,
                probeFeedbackAction,
                probeFeedbackNotes,
                probeCorrectionText,
                probeFeedbackRiskLevel,
                probeCreateRegressionTest,
                probeRequestHumanReview,
                ResolveProbeCalibrationOutcome(probeFeedbackAction)));
            lastProbeFeedback = feedback;
            if (feedback.ReviewItemId is { } reviewItemId)
            {
                selectedReviewItemId = reviewItemId;
            }

            probeStatus = feedback.ReviewItemId is null
                ? $"Feedback saved: {FormatShortId(feedback.Id)}"
                : $"Feedback saved: {FormatShortId(feedback.Id)} / review {FormatShortId(feedback.ReviewItemId.Value)}";
            NotificationService.Success("Probe feedback saved", probeStatus);
            await ReloadSnapshotAsync();
        }
        catch (Exception exception)
        {
            probeStatus = exception.Message;
            errorMessage = exception.Message;
            NotificationService.Error("Probe feedback failed", exception.Message);
        }
        finally
        {
            isBusy = false;
        }
    }

    internal Task ToggleProbeVoiceModeAsync()
    {
        probeVoiceModeEnabled = !probeVoiceModeEnabled;
        SetProbeVoiceStatus(probeVoiceModeEnabled ? "Audio on" : "Audio off", probeVoiceModeEnabled ? "primary" : "neutral");
        return Task.CompletedTask;
    }

    internal Task ToggleProbeQuestionRecordingAsync()
        => ToggleProbeRecordingAsync(CognitiveMemoryProbeVoiceCaptureTarget.Question);

    internal Task ToggleProbeCorrectionRecordingAsync()
        => ToggleProbeRecordingAsync(CognitiveMemoryProbeVoiceCaptureTarget.Correction);

    internal Task ToggleProbeConfirmationRecordingAsync()
        => ToggleProbeRecordingAsync(CognitiveMemoryProbeVoiceCaptureTarget.Confirmation);

    internal async Task ToggleProbeRecordingAsync(CognitiveMemoryProbeVoiceCaptureTarget target)
    {
        if (!probeVoiceRecording)
        {
            try
            {
                await JsRuntime.InvokeVoidAsync("CanDoItAll.agentFramework.voice.startRecording");
                probeVoiceModeEnabled = true;
                probeVoiceRecording = true;
                probeVoiceCaptureTarget = target;
                SetProbeVoiceStatus("Recording", "danger");
            }
            catch (Exception exception)
            {
                SetProbeVoiceStatus("Record failed", "danger");
                NotificationService.Error("Probe voice failed", exception.Message);
            }

            return;
        }

        await StopProbeRecordingAsync();
    }

    internal async Task StopProbeRecordingAsync()
    {
        probeVoiceRecording = false;
        probeVoiceTranscribing = true;
        SetProbeVoiceStatus("Transcribing", "info");

        try
        {
            var recording = await JsRuntime.InvokeAsync<BrowserVoiceRecording>(
                "CanDoItAll.agentFramework.voice.stopRecording");
            var transcription = await VoiceService.TranscribeAsync(recording.ToTranscriptionRequest());

            await HandleProbeVoiceTranscriptAsync(transcription.Text);
        }
        catch (Exception exception)
        {
            SetProbeVoiceStatus("Voice failed", "danger");
            NotificationService.Error("Probe voice failed", exception.Message);
        }
        finally
        {
            probeVoiceTranscribing = false;
        }
    }

    internal async Task HandleProbeVoiceTranscriptAsync(string transcript)
    {
        switch (probeVoiceCaptureTarget)
        {
            case CognitiveMemoryProbeVoiceCaptureTarget.Question:
                probeQuestion = transcript;
                SetProbeVoiceStatus("Asking memory", "info");
                await AskProbeAsync();
                if (lastProbeAskResult is not null)
                {
                    await SpeakProbeTextAsync(BuildProbeAnswerSpeech(lastProbeAskResult));
                }
                break;
            case CognitiveMemoryProbeVoiceCaptureTarget.Correction:
                await PrepareVoiceCorrectionAsync(transcript);
                break;
            case CognitiveMemoryProbeVoiceCaptureTarget.Confirmation:
                await HandleVoiceCorrectionConfirmationAsync(transcript);
                break;
        }
    }

    internal async Task PrepareVoiceCorrectionAsync(string transcript)
    {
        if (lastProbeAskResult?.Turn is null)
        {
            SetProbeVoiceStatus("Ask first", "warning");
            await SpeakProbeTextAsync("Ask memory a probe question first, then record the correction that should be reviewed for storage.");
            return;
        }

        pendingVoiceCorrectionText = transcript.Trim();
        probeCorrectionText = pendingVoiceCorrectionText;
        probeFeedbackNotes = "Voice correction prepared from Cognitive Memory probe dialogue.";
        probeFeedbackAction = CognitiveMemoryProbeFeedbackAction.AddCorrection;
        probeFeedbackRiskLevel = CognitiveMemoryRiskLevel.Medium;
        probeRequestHumanReview = true;
        probeCreateRegressionTest = true;
        probeVoiceAwaitingConfirmation = true;
        SetProbeVoiceStatus("Confirm storage", "warning");

        await SpeakProbeTextAsync(BuildVoiceCorrectionInterpretation(pendingVoiceCorrectionText));
    }

    internal async Task HandleVoiceCorrectionConfirmationAsync(string transcript)
    {
        var intent = AgentVoiceConfirmationClassifier.Classify(transcript);
        if (intent == AgentVoiceConfirmationIntent.Affirm)
        {
            if (!probeVoiceAwaitingConfirmation || string.IsNullOrWhiteSpace(pendingVoiceCorrectionText))
            {
                SetProbeVoiceStatus("Nothing pending", "warning");
                await SpeakProbeTextAsync("There is no pending memory correction to store.");
                return;
            }

            SetProbeVoiceStatus("Saving feedback", "info");
            await SubmitProbeFeedbackAsync();
            probeVoiceAwaitingConfirmation = false;
            pendingVoiceCorrectionText = string.Empty;
            await SpeakProbeTextAsync("The correction feedback was saved for review-gated memory processing.");
            return;
        }

        if (intent == AgentVoiceConfirmationIntent.Reject)
        {
            probeVoiceAwaitingConfirmation = false;
            pendingVoiceCorrectionText = string.Empty;
            probeCorrectionText = string.Empty;
            SetProbeVoiceStatus("Cancelled", "neutral");
            await SpeakProbeTextAsync("I cancelled the pending correction. Nothing was stored.");
            return;
        }

        SetProbeVoiceStatus("Clarify", "warning");
        await SpeakProbeTextAsync("I could not tell whether you approved storing this. Say yes, okay, store it, or cancel.");
    }

    internal async Task SpeakProbeTextAsync(string text)
    {
        probeVoiceSpeaking = true;
        try
        {
            await JsRuntime.InvokeVoidAsync("CanDoItAll.agentFramework.voice.clearAudioQueue");
            var queuedChunks = 0;
            await foreach (var synthesis in VoiceService.SynthesizeChunksAsync(new AgentVoiceSynthesisRequest(
                               text,
                               SuppressIdentifierOmissionNotice: ShouldSuppressProbeIdentifierOmissionNotice())))
            {
                TrackProbeIdentifierOmissionNotice(synthesis);
                queuedChunks++;
                await JsRuntime.InvokeVoidAsync(
                    "CanDoItAll.agentFramework.voice.enqueueAudio",
                    Convert.ToBase64String(synthesis.AudioBytes),
                    synthesis.ContentType);
                if (queuedChunks == 1)
                {
                    SetProbeVoiceStatus("Playing", "primary");
                }
            }

            SetProbeVoiceStatus(queuedChunks == 1 ? "Audio ready" : $"Audio ready ({queuedChunks} chunks)", "success");
        }
        catch (Exception exception)
        {
            SetProbeVoiceStatus("Speak failed", "danger");
            NotificationService.Error("Probe voice failed", exception.Message);
        }
        finally
        {
            probeVoiceSpeaking = false;
        }
    }

    internal bool ShouldSuppressProbeIdentifierOmissionNotice()
    {
        return activeProbeSessionId is { } sessionId
            ? probeSessionsWithVoiceIdentifierOmissionNotice.Contains(sessionId)
            : hasProbeVoiceIdentifierOmissionNoticeWithoutSession;
    }

    internal void TrackProbeIdentifierOmissionNotice(AgentVoiceSynthesisResult synthesis)
    {
        if (!synthesis.IdentifierOmissionNoticeIncluded)
        {
            return;
        }

        if (activeProbeSessionId is { } sessionId)
        {
            probeSessionsWithVoiceIdentifierOmissionNotice.Add(sessionId);
            return;
        }

        hasProbeVoiceIdentifierOmissionNoticeWithoutSession = true;
    }

    internal static string BuildProbeAnswerSpeech(CognitiveMemoryProbeAskResult result)
    {
        var includedSourceCount = result.RecallResult.ContextPack.SourceRefs.Count(item => item.IncludedInContext);
        return $"Memory answered the probe. It used {includedSourceCount} included source references. Review the visible answer evidence before deciding whether correction is needed.";
    }

    internal static string BuildVoiceCorrectionInterpretation(string correctionText)
    {
        var normalizedCorrection = string.Join(
            ' ',
            correctionText.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        const int maxSpeechCharacters = 900;
        if (normalizedCorrection.Length > maxSpeechCharacters)
        {
            normalizedCorrection = normalizedCorrection[..maxSpeechCharacters].TrimEnd();
        }

        return $"Wait a little while I process this. I understood this as a correction to the last probe answer: {normalizedCorrection}. I will store it as review-gated probe feedback, create a regression test request, and ask for human review before canonical memory changes. Say yes, okay, or store it to confirm. Say cancel to discard it.";
    }

    internal void SetProbeVoiceStatus(string text, string tone)
    {
        probeVoiceStatus = text;
        probeVoiceStatusTone = tone;
    }

    internal async Task<CognitiveMemoryProbeSessionRecord> CreateProbeSessionAsync(Guid projectId)
        => await ProbeService.StartAsync(new CognitiveMemoryProbeStartRequest(
            projectId,
            string.IsNullOrWhiteSpace(probeSessionTitle) ? "Project memory dialogue" : probeSessionTitle.Trim(),
            CreateProbePolicyContext(projectId, CognitiveMemoryRiskLevel.Low),
            probeRecallMode));

    internal bool TryResolveProbeProjectId(out Guid projectId)
    {
        if (ProjectId is { } resolvedProjectId && resolvedProjectId != Guid.Empty)
        {
            projectId = resolvedProjectId;
            return true;
        }

        projectId = Guid.Empty;
        probeStatus = "Open the page with a projectId query parameter before probing.";
        NotificationService.Warning("Project scope required", probeStatus);
        return false;
    }

    internal static CognitiveMemoryPolicyContext CreateProbePolicyContext(
        Guid projectId,
        CognitiveMemoryRiskLevel riskLevel)
        => new(
            projectId,
            OperatorActorId,
            CognitiveMemoryAccessLevel.Project,
            new CognitiveMemoryPolicyProfileId("cognitive-memory-probe-ui"),
            riskLevel,
            AllowRestrictedContent: false);

    internal static CognitiveMemoryRecallBudget CreateProbeRecallBudget()
        => new(
            coarseCandidateLimit: 160,
            graphExpansionDepth: 3,
            vectorResultLimit: 48,
            focusLimit: 48,
            detailItemLimit: 48,
            contextCharacterBudget: 96_000,
            maxSourceBytes: 768_000);

    internal static IReadOnlyDictionary<string, string> CreateProbeMetadata()
        => new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["surface"] = "dialogue-workbench",
            ["actor"] = OperatorActorId
        };

    internal static CognitiveMemoryCalibrationOutcomeKind ResolveProbeCalibrationOutcome(
        CognitiveMemoryProbeFeedbackAction action)
        => action switch
        {
            CognitiveMemoryProbeFeedbackAction.MarkCorrect => CognitiveMemoryCalibrationOutcomeKind.CorrectHighConfidence,
            CognitiveMemoryProbeFeedbackAction.MarkIncorrect => CognitiveMemoryCalibrationOutcomeKind.IncorrectHighConfidence,
            CognitiveMemoryProbeFeedbackAction.WrongScope => CognitiveMemoryCalibrationOutcomeKind.WrongScope,
            CognitiveMemoryProbeFeedbackAction.NeedsSource => CognitiveMemoryCalibrationOutcomeKind.SourceInsufficient,
            CognitiveMemoryProbeFeedbackAction.AddCorrection => CognitiveMemoryCalibrationOutcomeKind.IncorrectHighConfidence,
            _ => CognitiveMemoryCalibrationOutcomeKind.Unknown
        };
}

