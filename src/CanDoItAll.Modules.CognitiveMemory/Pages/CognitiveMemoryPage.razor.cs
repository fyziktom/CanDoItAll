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
    internal const string OperatorActorId = "cognitive-memory-operator-ui";

    [Inject]
    public ICognitiveMemoryReviewUiService ReviewUiService { get; set; } = default!;

    [Inject]
    public ICognitiveMemoryProbeService ProbeService { get; set; } = default!;

    [Inject]
    public ICognitiveMemoryAutomationSettingsService AutomationSettingsService { get; set; } = default!;

    [Inject]
    public ICognitiveMemorySourceIngestionService SourceIngestionService { get; set; } = default!;

    [Inject]
    public ICognitiveMemoryScheduledAutomationRunner ScheduledAutomationRunner { get; set; } = default!;

    [Inject]
    public ICognitiveMemoryProjectionRebuildService ProjectionRebuildService { get; set; } = default!;

    [Inject]
    public ICognitiveMemoryExternalSourceIngestionService ExternalSourceIngestionService { get; set; } = default!;

    [Inject]
    public IAgentFrameworkWorkspaceService AgentWorkspaceService { get; set; } = default!;

    [Inject]
    public IAgentVoiceService VoiceService { get; set; } = default!;

    [Inject]
    public IJSRuntime JsRuntime { get; set; } = default!;

    [Inject]
    public NotificationService NotificationService { get; set; } = default!;

    [SupplyParameterFromQuery]
    public Guid? ProjectId { get; set; }

    internal CognitiveMemoryReviewUiSnapshot? snapshot;
    internal Guid? selectedMemoryRecordId;
    internal Guid? selectedReviewItemId;
    internal Guid? selectedRecallTraceId;
    internal string reviewNotes = "Decision recorded from Cognitive Memory operator UI.";
    internal string errorMessage = string.Empty;
    internal int activeTabIndex;
    internal bool isLoading = true;
    internal bool isBusy;
    internal int uiRevision;
    internal CognitiveMemoryAutomationScheduleMode automationScheduleMode = CognitiveMemoryAutomationScheduleMode.ManualOnly;
    internal string nightlyLocalTime = "02:00";
    internal int idleMinutes = 30;
    internal string scheduledLocalTimesText = string.Empty;
    internal bool autoIngestProjectStructure = true;
    internal bool autoIngestProcessRuntime = true;
    internal bool autoConsolidateAfterIngestion = true;
    internal CognitiveMemoryModelAccessMode modelAccessMode = CognitiveMemoryModelAccessMode.AnyEnabledProvider;
    internal string defaultProviderProfileIdText = string.Empty;
    internal string defaultAgentIdText = string.Empty;
    internal List<CognitiveMemoryProviderSelection> modelProviderOptions = [];
    internal IReadOnlyList<AgentDefinition> modelAgentOptions = [];
    internal string modelAccessStatus = "Provider policy not loaded.";
    internal string manualSourceScopeText = string.Empty;
    internal int manualSourceTake = 250;
    internal string manualIngestionStatus = "Ready.";
    internal int manualIngestionProgress;
    internal string automationRunStatus = "Ready.";
    internal int automationRunProgress;
    internal string projectionRebuildStatus = "Ready.";
    internal int projectionRebuildProgress;
    internal string externalSourceUrl = string.Empty;
    internal string externalSourceStatus = "Ready.";
    internal int externalSourceProgress;
    internal CognitiveMemoryExternalSourceIngestResult? lastExternalSourceResult;
    internal Guid? activeProbeSessionId;
    internal CognitiveMemoryProbeAskResult? lastProbeAskResult;
    internal CognitiveMemoryProbeFeedbackRecord? lastProbeFeedback;
    internal string probeSessionTitle = "Project memory dialogue";
    internal string probeQuestion = "What are the project phases, investments, team growth, and main risks?";
    internal string probeFeedbackNotes = string.Empty;
    internal string probeCorrectionText = string.Empty;
    internal string probeStatus = "Ready.";
    internal CognitiveMemoryRecallMode probeRecallMode = CognitiveMemoryRecallMode.DeepSourceGrounded;
    internal CognitiveMemoryRecallIntentKind probeIntent = CognitiveMemoryRecallIntentKind.SourceLookup;
    internal CognitiveMemoryProbeFeedbackAction probeFeedbackAction = CognitiveMemoryProbeFeedbackAction.MarkCorrect;
    internal CognitiveMemoryRiskLevel probeFeedbackRiskLevel = CognitiveMemoryRiskLevel.Medium;
    internal bool probeCreateRegressionTest;
    internal bool probeRequestHumanReview;
    internal bool probeVoiceModeEnabled;
    internal bool probeVoiceRecording;
    internal bool probeVoiceTranscribing;
    internal bool probeVoiceSpeaking;
    internal bool probeVoiceAwaitingConfirmation;
    internal CognitiveMemoryProbeVoiceCaptureTarget probeVoiceCaptureTarget;
    internal string probeVoiceStatus = "Audio ready.";
    internal string probeVoiceStatusTone = "neutral";
    internal string pendingVoiceCorrectionText = string.Empty;
    internal readonly HashSet<Guid> probeSessionsWithVoiceIdentifierOmissionNotice = [];
    internal bool hasProbeVoiceIdentifierOmissionNoticeWithoutSession;

    internal CognitiveMemoryReviewQueueItem? SelectedReviewItem
        => snapshot?.ReviewItems.FirstOrDefault(item => item.Id.Value == selectedReviewItemId);

    internal CognitiveMemoryExplorerItem? SelectedMemoryRecord
        => snapshot?.MemoryRecords.FirstOrDefault(record => record.Id.Value == selectedMemoryRecordId);

    internal CognitiveMemoryRecallTraceView? SelectedRecallTrace
        => snapshot?.RecallTraces.FirstOrDefault(trace => trace.Id == selectedRecallTraceId);

    internal CognitiveMemoryProbeSessionView? ActiveProbeSessionView
        => activeProbeSessionId is { } sessionId
            ? snapshot?.ProbeSessions.FirstOrDefault(session => session.Id == sessionId)
            : null;

    internal bool HasActiveProbeSession
        => activeProbeSessionId.HasValue;

    internal bool CanStartProbe
        => !isBusy && ProjectId is { } projectId && projectId != Guid.Empty;

    internal bool CanAskProbe
        => !isBusy &&
           ProjectId is { } projectId &&
           projectId != Guid.Empty &&
           !string.IsNullOrWhiteSpace(probeQuestion);

    internal bool CanSendProbeFeedback
        => !isBusy && lastProbeAskResult?.Turn is not null;

    internal bool CanUseProbeVoice
        => !isBusy && !probeVoiceTranscribing && !probeVoiceSpeaking;

    internal string ProjectScopePlaceholder
        => ProjectId?.ToString("D") ?? "Optional process scope id";

    internal string ProbeProjectScopeText
        => ProjectId is { } projectId && projectId != Guid.Empty
            ? projectId.ToString("D")
            : "No project selected";

    internal string ActiveProbeSessionTitle
        => ActiveProbeSessionView?.Title ??
           (activeProbeSessionId is { } sessionId ? $"Session {FormatShortId(sessionId)}" : "No active session");

    internal string ActiveProbeSessionMeta
        => ActiveProbeSessionView is { } session
            ? $"{FormatLabel(session.Status)} / {FormatLabel(session.RecallMode)} / {session.TurnCount} turn(s)"
            : activeProbeSessionId is null
                ? "Start or reuse a session before asking."
                : "Session was created in this workbench and will appear after refresh.";

    internal string ReviewQueueTone
        => snapshot?.Summary.HighRiskReviewCount > 0
            ? "danger"
            : snapshot?.Summary.PendingReviewCount > 0
                ? "warning"
                : "success";

    internal string HealthBadgeText
    {
        get
        {
            if (snapshot is null)
            {
                return "0";
            }

            return (snapshot.Summary.ConsolidationIssueCount + snapshot.Summary.ProjectionIssueCount).ToString();
        }
    }

    internal string RegulationBadgeText
    {
        get
        {
            if (snapshot is null)
            {
                return "0";
            }

            return (snapshot.Summary.SelfRegulationActionCount +
                    snapshot.Summary.AnswerGateInterventionCount +
                    snapshot.Summary.ProfessorReviewCount +
                    snapshot.Summary.LearningProposalCount).ToString();
        }
    }

    internal string ScaleBadgeText
    {
        get
        {
            if (snapshot is null)
            {
                return "0";
            }

            return (snapshot.Summary.CrossProjectReviewCount + snapshot.Summary.DistributedIssueCount).ToString();
        }
    }

    protected override async Task OnInitializedAsync()
    {
        await LoadAutomationSettingsAsync();
        await RefreshAsync();
    }


    internal async Task RefreshAsync()
    {
        if (isBusy)
        {
            return;
        }

        isBusy = true;
        isLoading = true;
        errorMessage = string.Empty;

        try
        {
            snapshot = await LoadSnapshotAsync();
            selectedMemoryRecordId = ResolveSelectedMemoryRecordId(snapshot, selectedMemoryRecordId);
            selectedReviewItemId = ResolveSelectedReviewItemId(snapshot, selectedReviewItemId);
            selectedRecallTraceId = ResolveSelectedRecallTraceId(snapshot, selectedRecallTraceId);
            selectedAggregateCandidateId = ResolveSelectedAggregateCandidateId(snapshot, selectedAggregateCandidateId);
            BumpUiRevision();
        }
        catch (Exception exception)
        {
            errorMessage = exception.Message;
            NotificationService.Error("Memory review refresh failed", exception.Message);
        }
        finally
        {
            isLoading = false;
            isBusy = false;
        }
    }

    internal void SelectReviewItem(Guid reviewItemId)
    {
        selectedReviewItemId = reviewItemId;
        BumpUiRevision();
    }

    internal void SelectMemoryRecord(Guid memoryRecordId)
    {
        selectedMemoryRecordId = memoryRecordId;
        BumpUiRevision();
    }

    internal void SelectRecallTrace(Guid recallTraceId)
    {
        selectedRecallTraceId = recallTraceId;
        BumpUiRevision();
    }

    internal async Task DecideReviewAsync(CognitiveMemoryReviewDecisionKind decisionKind)
    {
        var item = SelectedReviewItem;
        if (item is null || isBusy)
        {
            return;
        }

        isBusy = true;
        errorMessage = string.Empty;

        try
        {
            var updated = await ReviewUiService.DecideReviewItemAsync(new CognitiveMemoryReviewDecisionRequest(
                item.Id,
                decisionKind,
                OperatorActorId,
                reviewNotes,
                item.ConcurrencyToken));
            NotificationService.Success("Review decision recorded", $"{FormatLabel(updated.Status)}: {updated.SubjectTitle}");
            selectedReviewItemId = updated.Id.Value;
            await LoadAfterDecisionAsync();
        }
        catch (Exception exception)
        {
            errorMessage = exception.Message;
            NotificationService.Error("Review decision failed", exception.Message);
        }
        finally
        {
            isBusy = false;
        }
    }

    internal async Task LoadAfterDecisionAsync()
    {
        await ReloadSnapshotAsync();
    }

    internal async Task ReloadSnapshotAsync()
    {
        snapshot = await LoadSnapshotAsync();
        selectedMemoryRecordId = ResolveSelectedMemoryRecordId(snapshot, selectedMemoryRecordId);
        selectedReviewItemId = ResolveSelectedReviewItemId(snapshot, selectedReviewItemId);
        selectedRecallTraceId = ResolveSelectedRecallTraceId(snapshot, selectedRecallTraceId);
        selectedAggregateCandidateId = ResolveSelectedAggregateCandidateId(snapshot, selectedAggregateCandidateId);
        BumpUiRevision();
    }

    internal void BumpUiRevision()
    {
        unchecked
        {
            uiRevision++;
        }
    }
}
