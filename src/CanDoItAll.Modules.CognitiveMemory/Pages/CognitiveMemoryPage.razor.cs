using CanDoItAll.AgentFramework.Core;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Components.Common;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Rendering;
using System.Text;

namespace CanDoItAll.Modules.CognitiveMemory.Pages;

public partial class CognitiveMemoryPage
{
    private const string OperatorActorId = "cognitive-memory-operator-ui";

    [Inject]
    public ICognitiveMemoryReviewUiService ReviewUiService { get; set; } = default!;

    [Inject]
    public ICognitiveMemoryAutomationSettingsService AutomationSettingsService { get; set; } = default!;

    [Inject]
    public ICognitiveMemorySourceIngestionService SourceIngestionService { get; set; } = default!;

    [Inject]
    public ICognitiveMemoryExternalSourceIngestionService ExternalSourceIngestionService { get; set; } = default!;

    [Inject]
    public NotificationService NotificationService { get; set; } = default!;

    [SupplyParameterFromQuery]
    public Guid? ProjectId { get; set; }

    private CognitiveMemoryReviewUiSnapshot? snapshot;
    private Guid? selectedMemoryRecordId;
    private Guid? selectedReviewItemId;
    private Guid? selectedRecallTraceId;
    private string reviewNotes = "Decision recorded from Cognitive Memory operator UI.";
    private string errorMessage = string.Empty;
    private int activeTabIndex;
    private bool isLoading = true;
    private bool isBusy;
    private CognitiveMemoryAutomationScheduleMode automationScheduleMode = CognitiveMemoryAutomationScheduleMode.ManualOnly;
    private string nightlyLocalTime = "02:00";
    private int idleMinutes = 30;
    private string scheduledLocalTimesText = string.Empty;
    private bool autoIngestProjectStructure = true;
    private bool autoIngestProcessRuntime = true;
    private bool autoConsolidateAfterIngestion = true;
    private string manualSourceScopeText = string.Empty;
    private int manualSourceTake = 250;
    private string manualIngestionStatus = "Ready.";
    private int manualIngestionProgress;
    private string externalSourceUrl = string.Empty;
    private string externalSourceStatus = "Ready.";
    private int externalSourceProgress;
    private CognitiveMemoryExternalSourceIngestResult? lastExternalSourceResult;

    private CognitiveMemoryReviewQueueItem? SelectedReviewItem
        => snapshot?.ReviewItems.FirstOrDefault(item => item.Id.Value == selectedReviewItemId);

    private CognitiveMemoryExplorerItem? SelectedMemoryRecord
        => snapshot?.MemoryRecords.FirstOrDefault(record => record.Id.Value == selectedMemoryRecordId);

    private CognitiveMemoryRecallTraceView? SelectedRecallTrace
        => snapshot?.RecallTraces.FirstOrDefault(trace => trace.Id == selectedRecallTraceId);

    private string ProjectScopePlaceholder
        => ProjectId?.ToString("D") ?? "Optional process scope id";

    private string ReviewQueueTone
        => snapshot?.Summary.HighRiskReviewCount > 0
            ? "danger"
            : snapshot?.Summary.PendingReviewCount > 0
                ? "warning"
                : "success";

    private string HealthBadgeText
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

    private string RegulationBadgeText
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

    private string ScaleBadgeText
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

    private async Task LoadAutomationSettingsAsync()
    {
        try
        {
            var settings = await AutomationSettingsService.GetAsync(CancellationToken.None);
            automationScheduleMode = settings.ScheduleMode;
            nightlyLocalTime = settings.NightlyLocalTime;
            idleMinutes = settings.IdleMinutes;
            scheduledLocalTimesText = string.Join(Environment.NewLine, settings.ScheduledLocalTimes);
            autoIngestProjectStructure = settings.AutoIngestProjectStructure;
            autoIngestProcessRuntime = settings.AutoIngestProcessRuntime;
            autoConsolidateAfterIngestion = settings.AutoConsolidateAfterIngestion;
        }
        catch (Exception exception)
        {
            errorMessage = exception.Message;
            NotificationService.Error("Memory settings failed", exception.Message);
        }
    }

    private async Task RefreshAsync()
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
            snapshot = await ReviewUiService.GetSnapshotAsync(
                new CognitiveMemoryReviewUiQuery(ProjectId),
                CancellationToken.None);
            selectedMemoryRecordId = ResolveSelectedMemoryRecordId(snapshot, selectedMemoryRecordId);
            selectedReviewItemId = ResolveSelectedReviewItemId(snapshot, selectedReviewItemId);
            selectedRecallTraceId = ResolveSelectedRecallTraceId(snapshot, selectedRecallTraceId);
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

    private void SelectReviewItem(Guid reviewItemId)
    {
        selectedReviewItemId = reviewItemId;
    }

    private void SelectMemoryRecord(Guid memoryRecordId)
    {
        selectedMemoryRecordId = memoryRecordId;
    }

    private void SelectRecallTrace(Guid recallTraceId)
    {
        selectedRecallTraceId = recallTraceId;
    }

    private async Task DecideReviewAsync(CognitiveMemoryReviewDecisionKind decisionKind)
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

    private async Task LoadAfterDecisionAsync()
    {
        await ReloadSnapshotAsync();
    }

    private async Task ReloadSnapshotAsync()
    {
        snapshot = await ReviewUiService.GetSnapshotAsync(
            new CognitiveMemoryReviewUiQuery(ProjectId),
            CancellationToken.None);
        selectedMemoryRecordId = ResolveSelectedMemoryRecordId(snapshot, selectedMemoryRecordId);
        selectedReviewItemId = ResolveSelectedReviewItemId(snapshot, selectedReviewItemId);
        selectedRecallTraceId = ResolveSelectedRecallTraceId(snapshot, selectedRecallTraceId);
    }

    private async Task SaveAutomationSettingsAsync()
    {
        if (isBusy)
        {
            return;
        }

        isBusy = true;
        errorMessage = string.Empty;

        try
        {
            var settings = await AutomationSettingsService.SaveAsync(new CognitiveMemoryAutomationSettingsUpdate(
                automationScheduleMode,
                nightlyLocalTime,
                idleMinutes,
                ParseScheduledLocalTimes(),
                autoIngestProjectStructure,
                autoIngestProcessRuntime,
                autoConsolidateAfterIngestion,
                OperatorActorId));
            scheduledLocalTimesText = string.Join(Environment.NewLine, settings.ScheduledLocalTimes);
            NotificationService.Success("Memory settings saved", FormatLabel(settings.ScheduleMode));
        }
        catch (Exception exception)
        {
            errorMessage = exception.Message;
            NotificationService.Error("Memory settings failed", exception.Message);
        }
        finally
        {
            isBusy = false;
        }
    }

    private async Task IngestProjectStructureAsync()
    {
        await RunManualSourceIngestionAsync(MemorySourceKind.WorkbenchProjectStructure);
    }

    private async Task IngestProcessesAsync()
    {
        await RunManualSourceIngestionAsync(MemorySourceKind.ProcessRuntime);
    }

    private async Task RunManualSourceIngestionAsync(MemorySourceKind sourceKind)
    {
        if (isBusy)
        {
            return;
        }

        isBusy = true;
        errorMessage = string.Empty;
        manualIngestionProgress = 15;
        manualIngestionStatus = $"Starting {FormatLabel(sourceKind).ToLowerInvariant()} ingestion.";
        await InvokeAsync(StateHasChanged);
        await Task.Yield();

        try
        {
            var scopeId = ResolveManualScopeId(sourceKind);
            var result = await SourceIngestionService.IngestAsync(new CognitiveMemorySourceIngestionRequest(
                sourceKind,
                scopeId,
                new CognitiveMemoryIdempotencyKey($"ui:{sourceKind}:{Guid.NewGuid():N}"),
                Take: manualSourceTake,
                ProjectId: ProjectId ?? (sourceKind == MemorySourceKind.WorkbenchProjectStructure ? scopeId : null)));

            manualIngestionProgress = 100;
            manualIngestionStatus = $"{FormatLabel(result.Status)}: {result.CreatedSourceItemCount} created, {result.UpdatedSourceItemCount} updated, {result.CreatedEvidenceAnchorCount} anchors.";
            NotificationService.Success("Memory ingestion finished", manualIngestionStatus);
            await ReloadSnapshotAsync();
        }
        catch (Exception exception)
        {
            manualIngestionProgress = 100;
            manualIngestionStatus = exception.Message;
            errorMessage = exception.Message;
            NotificationService.Error("Memory ingestion failed", exception.Message);
        }
        finally
        {
            isBusy = false;
        }
    }

    private async Task UploadExternalSourceAsync(InputFileChangeEventArgs args)
    {
        if (isBusy)
        {
            return;
        }

        var file = args.File;
        isBusy = true;
        errorMessage = string.Empty;
        externalSourceProgress = 15;
        externalSourceStatus = $"Uploading {file.Name}.";
        await InvokeAsync(StateHasChanged);
        await Task.Yield();

        try
        {
            await using var stream = file.OpenReadStream(10 * 1024 * 1024);
            lastExternalSourceResult = await ExternalSourceIngestionService.IngestFileAsync(
                ProjectId,
                file.Name,
                file.ContentType,
                stream,
                file.Size,
                OperatorActorId);

            ApplyExternalSourceResult(lastExternalSourceResult);
            NotificationService.Success("External source ingested", externalSourceStatus);
            await ReloadSnapshotAsync();
        }
        catch (Exception exception)
        {
            externalSourceProgress = 100;
            externalSourceStatus = exception.Message;
            errorMessage = exception.Message;
            NotificationService.Error("External source failed", exception.Message);
        }
        finally
        {
            isBusy = false;
        }
    }

    private async Task IngestExternalLinkAsync()
    {
        if (isBusy)
        {
            return;
        }

        if (!Uri.TryCreate(externalSourceUrl, UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("http" or "https"))
        {
            externalSourceStatus = "Enter an absolute HTTP or HTTPS URL.";
            return;
        }

        isBusy = true;
        errorMessage = string.Empty;
        externalSourceProgress = 15;
        externalSourceStatus = $"Fetching {uri.Host}.";
        await InvokeAsync(StateHasChanged);
        await Task.Yield();

        try
        {
            lastExternalSourceResult = await ExternalSourceIngestionService.IngestWebsiteAsync(
                ProjectId,
                uri,
                OperatorActorId);

            ApplyExternalSourceResult(lastExternalSourceResult);
            NotificationService.Success("Website ingested", externalSourceStatus);
            await ReloadSnapshotAsync();
        }
        catch (Exception exception)
        {
            externalSourceProgress = 100;
            externalSourceStatus = exception.Message;
            errorMessage = exception.Message;
            NotificationService.Error("Website ingestion failed", exception.Message);
        }
        finally
        {
            isBusy = false;
        }
    }

    private IReadOnlyList<string> ParseScheduledLocalTimes()
    {
        if (string.IsNullOrWhiteSpace(scheduledLocalTimesText))
        {
            return [];
        }

        return scheduledLocalTimesText
            .Split(['\r', '\n', ',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
    }

    private Guid ResolveManualScopeId(MemorySourceKind sourceKind)
    {
        if (string.IsNullOrWhiteSpace(manualSourceScopeText))
        {
            if (ProjectId.HasValue)
            {
                return ProjectId.Value;
            }

            if (sourceKind == MemorySourceKind.ProcessRuntime)
            {
                return Guid.Empty;
            }

            throw new InvalidOperationException("Project structure ingestion requires a project id or scope id.");
        }

        return Guid.TryParse(manualSourceScopeText, out var scopeId)
            ? scopeId
            : throw new InvalidOperationException("Scope id must be a GUID.");
    }

    private void ApplyExternalSourceResult(CognitiveMemoryExternalSourceIngestResult result)
    {
        externalSourceProgress = result.ProgressPercent;
        externalSourceStatus = result.FailureMessage is null
            ? $"{FormatLabel(result.Status)}: {result.StatusMessage}"
            : result.FailureMessage;
    }

    private static Guid? ResolveSelectedMemoryRecordId(
        CognitiveMemoryReviewUiSnapshot snapshot,
        Guid? preferredId)
    {
        if (preferredId.HasValue &&
            snapshot.MemoryRecords.Any(record => record.Id.Value == preferredId.Value))
        {
            return preferredId.Value;
        }

        return snapshot.MemoryRecords.FirstOrDefault()?.Id.Value;
    }

    private static Guid? ResolveSelectedReviewItemId(
        CognitiveMemoryReviewUiSnapshot snapshot,
        Guid? preferredId)
    {
        if (preferredId.HasValue &&
            snapshot.ReviewItems.Any(item => item.Id.Value == preferredId.Value))
        {
            return preferredId.Value;
        }

        return snapshot.ReviewItems.FirstOrDefault()?.Id.Value;
    }

    private static Guid? ResolveSelectedRecallTraceId(
        CognitiveMemoryReviewUiSnapshot snapshot,
        Guid? preferredId)
    {
        if (preferredId.HasValue &&
            snapshot.RecallTraces.Any(trace => trace.Id == preferredId.Value))
        {
            return preferredId.Value;
        }

        return snapshot.RecallTraces.FirstOrDefault()?.Id;
    }

    private static string SummaryValue(int? value)
        => value?.ToString() ?? "-";

    private static string FormatDate(DateTimeOffset value)
        => value.ToLocalTime().ToString("MMM d, HH:mm");

    private static string FormatShortId(Guid value)
        => value.ToString("N")[..8];

    private static string ScoreText(double? value)
        => value.HasValue ? value.Value.ToString("0.00") : "n/a";

    private static string FormatLabel<TValue>(TValue value)
        where TValue : struct, Enum
    {
        var text = value.ToString();
        var builder = new StringBuilder(text.Length + 8);

        for (var index = 0; index < text.Length; index++)
        {
            var character = text[index];
            if (index > 0 &&
                char.IsUpper(character) &&
                (char.IsLower(text[index - 1]) ||
                 char.IsDigit(text[index - 1]) ||
                 index + 1 < text.Length && char.IsLower(text[index + 1])))
            {
                builder.Append(' ');
            }

            builder.Append(character);
        }

        return builder.ToString();
    }

    private static int VisibleSourceEvidenceCount(CognitiveMemoryExplorerItem record)
        => Math.Max(record.SourceEvidenceCount, record.SourceLinks.Count);

    private static string ReviewTone(
        CognitiveMemoryReviewStatus status,
        CognitiveMemoryRiskLevel riskLevel)
        => status switch
        {
            CognitiveMemoryReviewStatus.Approved => "success",
            CognitiveMemoryReviewStatus.Rejected => "danger",
            CognitiveMemoryReviewStatus.NeedsChanges => "warning",
            CognitiveMemoryReviewStatus.Deferred => "secondary",
            _ when riskLevel == CognitiveMemoryRiskLevel.High => "danger",
            _ => "warning"
        };

    private static string RecordTone(
        CognitiveMemoryValidationState validationState,
        CognitiveMemoryRiskLevel riskLevel)
        => validationState switch
        {
            CognitiveMemoryValidationState.Approved or CognitiveMemoryValidationState.HumanReviewed => "success",
            CognitiveMemoryValidationState.Rejected => "danger",
            CognitiveMemoryValidationState.NeedsHumanReview => "warning",
            _ when riskLevel == CognitiveMemoryRiskLevel.High => "danger",
            _ => "secondary"
        };

    private static string RiskTone(CognitiveMemoryRiskLevel riskLevel)
        => riskLevel switch
        {
            CognitiveMemoryRiskLevel.High => "danger",
            CognitiveMemoryRiskLevel.Medium => "warning",
            _ => "success"
        };

    private static string RunTone(CognitiveMemoryRunStatus status)
        => status switch
        {
            CognitiveMemoryRunStatus.Succeeded => "success",
            CognitiveMemoryRunStatus.Failed => "danger",
            CognitiveMemoryRunStatus.Blocked => "warning",
            CognitiveMemoryRunStatus.Running => "info",
            CognitiveMemoryRunStatus.Cancelled => "neutral",
            _ => "secondary"
        };

    private static string ProjectionTone(CognitiveMemoryProjectionStatus status, bool rebuildRequired)
        => status == CognitiveMemoryProjectionStatus.Failed
            ? "danger"
            : rebuildRequired || status == CognitiveMemoryProjectionStatus.RebuildRequired
                ? "warning"
                : status == CognitiveMemoryProjectionStatus.Projected
                    ? "success"
                    : "secondary";

    private static string ReplayTone(CognitiveMemoryReplayJobState state)
        => state switch
        {
            CognitiveMemoryReplayJobState.Completed => "success",
            CognitiveMemoryReplayJobState.Failed => "danger",
            CognitiveMemoryReplayJobState.NeedsReview => "warning",
            CognitiveMemoryReplayJobState.Running => "info",
            _ => "secondary"
        };

    private static string ProcedureTone(CognitiveMemoryProcedureSkillMaturity maturity, CognitiveMemoryRiskLevel riskLevel)
        => riskLevel == CognitiveMemoryRiskLevel.High
            ? "danger"
            : maturity switch
            {
                CognitiveMemoryProcedureSkillMaturity.Automatable or CognitiveMemoryProcedureSkillMaturity.Validated => "success",
                CognitiveMemoryProcedureSkillMaturity.Reviewed => "info",
                CognitiveMemoryProcedureSkillMaturity.Draft or CognitiveMemoryProcedureSkillMaturity.Observed => "warning",
                _ => "secondary"
            };

    private static string ProbeTone(CognitiveMemoryProbeSessionStatus status)
        => status switch
        {
            CognitiveMemoryProbeSessionStatus.Active => "info",
            CognitiveMemoryProbeSessionStatus.Closed => "success",
            CognitiveMemoryProbeSessionStatus.Abandoned => "warning",
            _ => "secondary"
        };

    private static string SelfRegulationTone(CognitiveMemorySelfRegulationStateKind state)
        => state switch
        {
            CognitiveMemorySelfRegulationStateKind.Calibrated => "success",
            CognitiveMemorySelfRegulationStateKind.ProfessorReviewNeeded or
            CognitiveMemorySelfRegulationStateKind.HighRiskUnverified or
            CognitiveMemorySelfRegulationStateKind.AccessLimited => "danger",
            CognitiveMemorySelfRegulationStateKind.Overconfident or
            CognitiveMemorySelfRegulationStateKind.SourcePoor or
            CognitiveMemorySelfRegulationStateKind.Fragmented => "warning",
            CognitiveMemorySelfRegulationStateKind.Underconfident or
            CognitiveMemorySelfRegulationStateKind.Exploratory => "info",
            _ => "secondary"
        };

    private static string AnswerGateTone(CognitiveMemoryAnswerGateDecisionKind decisionKind)
        => decisionKind switch
        {
            CognitiveMemoryAnswerGateDecisionKind.Answer => "success",
            CognitiveMemoryAnswerGateDecisionKind.Warn => "warning",
            CognitiveMemoryAnswerGateDecisionKind.Abstain or
            CognitiveMemoryAnswerGateDecisionKind.Review or
            CognitiveMemoryAnswerGateDecisionKind.ProfessorReview => "danger",
            CognitiveMemoryAnswerGateDecisionKind.Clarify or
            CognitiveMemoryAnswerGateDecisionKind.SourceAudit or
            CognitiveMemoryAnswerGateDecisionKind.Probe or
            CognitiveMemoryAnswerGateDecisionKind.LearningRequest => "info",
            _ => "secondary"
        };

    private static string ProfessorReviewTone(CognitiveMemoryProfessorReviewStatus status)
        => status switch
        {
            CognitiveMemoryProfessorReviewStatus.Completed or CognitiveMemoryProfessorReviewStatus.Routed => "success",
            CognitiveMemoryProfessorReviewStatus.RejectedByPolicy => "danger",
            CognitiveMemoryProfessorReviewStatus.Requested => "warning",
            _ => "secondary"
        };

    private static string LearningProposalTone(CognitiveMemoryLearningProposalStatus status)
        => status switch
        {
            CognitiveMemoryLearningProposalStatus.Approved or CognitiveMemoryLearningProposalStatus.Completed => "success",
            CognitiveMemoryLearningProposalStatus.Rejected or CognitiveMemoryLearningProposalStatus.Snoozed => "secondary",
            CognitiveMemoryLearningProposalStatus.PendingApproval => "warning",
            _ => "info"
        };

    private static string CrossProjectTone(CognitiveMemoryCrossProjectPromotionStatus status)
        => status switch
        {
            CognitiveMemoryCrossProjectPromotionStatus.Approved => "success",
            CognitiveMemoryCrossProjectPromotionStatus.Rejected or CognitiveMemoryCrossProjectPromotionStatus.Demoted => "danger",
            CognitiveMemoryCrossProjectPromotionStatus.PendingReview => "warning",
            _ => "secondary"
        };

    private static string DistributedJobTone(CognitiveMemoryDistributedJobState state)
        => state switch
        {
            CognitiveMemoryDistributedJobState.Completed => "success",
            CognitiveMemoryDistributedJobState.Rejected or CognitiveMemoryDistributedJobState.Expired => "danger",
            CognitiveMemoryDistributedJobState.Leased => "info",
            CognitiveMemoryDistributedJobState.Queued => "warning",
            _ => "secondary"
        };

    private static void RenderFact(
        RenderTreeBuilder builder,
        int sequence,
        string label,
        string value)
    {
        builder.OpenElement(sequence, "div");
        builder.AddAttribute(sequence + 1, "class", "cognitive-memory-fact");
        builder.OpenElement(sequence + 2, "span");
        builder.AddContent(sequence + 3, label);
        builder.CloseElement();
        builder.OpenElement(sequence + 4, "strong");
        builder.AddContent(sequence + 5, value);
        builder.CloseElement();
        builder.CloseElement();
    }

    private void RenderDecisionButton(
        RenderTreeBuilder builder,
        int sequence,
        string text,
        string icon,
        ButtonStyle style,
        CognitiveMemoryReviewDecisionKind decisionKind,
        string testId)
    {
        builder.OpenComponent<Button>(sequence);
        builder.AddAttribute(sequence + 1, nameof(Button.Text), text);
        builder.AddAttribute(sequence + 2, nameof(Button.Icon), icon);
        builder.AddAttribute(sequence + 3, nameof(Button.ButtonStyle), style);
        builder.AddAttribute(sequence + 4, nameof(Button.Size), ButtonSize.Small);
        builder.AddAttribute(sequence + 5, nameof(Button.Disabled), isBusy || SelectedReviewItem is null);
        builder.AddAttribute(sequence + 6, nameof(Button.Click), EventCallback.Factory.Create(this, () => DecideReviewAsync(decisionKind)));
        builder.AddAttribute(sequence + 7, "data-testid", testId);
        builder.CloseComponent();
    }

    private static void RenderTraceCollections(
        RenderTreeBuilder builder,
        int sequence,
        CognitiveMemoryRecallTraceView trace)
    {
        builder.OpenElement(sequence, "div");
        builder.AddAttribute(sequence + 1, "class", "cognitive-memory-section");
        builder.OpenElement(sequence + 2, "h3");
        builder.AddContent(sequence + 3, "Selected candidates");
        builder.CloseElement();
        if (trace.Candidates.Count == 0)
        {
            RenderEmptyLine(builder, sequence + 4, "No candidate rows were persisted for this trace.");
        }
        else
        {
            RenderCandidateRows(builder, sequence + 5, trace.Candidates);
        }

        builder.CloseElement();
        builder.OpenElement(sequence + 100, "div");
        builder.AddAttribute(sequence + 101, "class", "cognitive-memory-section");
        builder.OpenElement(sequence + 102, "h3");
        builder.AddContent(sequence + 103, "Source references");
        builder.CloseElement();
        if (trace.SourceReferences.Count == 0)
        {
            RenderEmptyLine(builder, sequence + 104, "No source references were selected for this trace.");
        }
        else
        {
            RenderSourceRows(builder, sequence + 105, trace.SourceReferences);
        }

        builder.CloseElement();
    }

    private static void RenderCandidateRows(
        RenderTreeBuilder builder,
        int sequence,
        IReadOnlyList<CognitiveMemoryRecallCandidateView> candidates)
    {
        builder.OpenElement(sequence, "div");
        builder.AddAttribute(sequence + 1, "class", "cognitive-memory-list");
        var itemSequence = sequence + 2;
        foreach (var candidate in candidates)
        {
            builder.OpenElement(itemSequence++, "div");
            builder.AddAttribute(itemSequence++, "class", "cognitive-memory-row");
            builder.OpenElement(itemSequence++, "strong");
            builder.AddContent(itemSequence++, candidate.Title);
            builder.CloseElement();
            builder.OpenElement(itemSequence++, "span");
            builder.AddContent(itemSequence++, $"{FormatLabel(candidate.DecisionKind)} / {FormatLabel(candidate.PrimaryChannelKind)} / score {ScoreText(candidate.DisplayRankProjection)}");
            builder.CloseElement();
            if (!string.IsNullOrWhiteSpace(candidate.Reason))
            {
                builder.OpenElement(itemSequence++, "small");
                builder.AddContent(itemSequence++, candidate.Reason);
                builder.CloseElement();
            }

            builder.CloseElement();
        }

        builder.CloseElement();
    }

    private static void RenderSourceRows(
        RenderTreeBuilder builder,
        int sequence,
        IReadOnlyList<CognitiveMemoryRecallSourceReferenceView> sourceReferences)
    {
        builder.OpenElement(sequence, "div");
        builder.AddAttribute(sequence + 1, "class", "cognitive-memory-list");
        var itemSequence = sequence + 2;
        foreach (var sourceRef in sourceReferences)
        {
            builder.OpenElement(itemSequence++, "div");
            builder.AddAttribute(itemSequence++, "class", "cognitive-memory-row");
            builder.OpenElement(itemSequence++, "strong");
            builder.AddContent(itemSequence++, string.IsNullOrWhiteSpace(sourceRef.SourceSystem) ? "Source" : sourceRef.SourceSystem);
            builder.CloseElement();
            builder.OpenElement(itemSequence++, "span");
            builder.AddContent(itemSequence++, FirstNonEmpty(sourceRef.Summary, sourceRef.Locator, "No source summary."));
            builder.CloseElement();
            builder.OpenElement(itemSequence++, "small");
            builder.AddContent(itemSequence++, $"{FormatLabel(sourceRef.AccessLevel)} / {FormatLabel(sourceRef.RedactionState)} / {(sourceRef.IncludedInContext ? "included" : FormatLabel(sourceRef.ExclusionReasonKind))}");
            builder.CloseElement();
            builder.CloseElement();
        }

        builder.CloseElement();
    }

    private static void RenderMemorySourceRows(
        RenderTreeBuilder builder,
        int sequence,
        IReadOnlyList<CognitiveMemorySourceLinkView> sourceLinks)
    {
        builder.OpenElement(sequence, "div");
        builder.AddAttribute(sequence + 1, "class", "cognitive-memory-list");
        var itemSequence = sequence + 2;
        foreach (var sourceLink in sourceLinks)
        {
            builder.OpenElement(itemSequence++, "div");
            builder.AddAttribute(itemSequence++, "class", "cognitive-memory-row");
            builder.OpenElement(itemSequence++, "strong");
            builder.AddContent(itemSequence++, FormatLabel(sourceLink.EvidenceRole));
            builder.CloseElement();
            builder.OpenElement(itemSequence++, "span");
            builder.AddContent(itemSequence++, FirstNonEmpty(sourceLink.Summary, sourceLink.Locator, FormatShortId(sourceLink.SourceItemId)));
            builder.CloseElement();
            if (!string.IsNullOrWhiteSpace(sourceLink.Locator))
            {
                builder.OpenElement(itemSequence++, "small");
                builder.AddContent(itemSequence++, sourceLink.Locator);
                builder.CloseElement();
            }

            builder.CloseElement();
        }

        builder.CloseElement();
    }

    private void RenderProjectionRows(RenderTreeBuilder builder, int sequence)
    {
        if (snapshot is null)
        {
            return;
        }

        builder.OpenComponent<Stack>(sequence);
        builder.AddAttribute(sequence + 1, nameof(Stack.GapScale), LayoutGap.Small);
        builder.AddAttribute(sequence + 2, nameof(Stack.Class), "mt-4");
        builder.AddAttribute(sequence + 3, nameof(Stack.ChildContent), (RenderFragment)(contentBuilder =>
        {
            var itemSequence = 0;
            foreach (var projection in snapshot.ProjectionHealth)
            {
                contentBuilder.OpenElement(itemSequence++, "div");
                contentBuilder.AddAttribute(itemSequence++, "class", "cognitive-memory-row");
                contentBuilder.OpenElement(itemSequence++, "strong");
                contentBuilder.AddContent(itemSequence++, $"{FormatLabel(projection.ProjectionKind)} / {projection.TargetProvider}");
                contentBuilder.CloseElement();
                contentBuilder.OpenComponent<StatusBadge>(itemSequence++);
                contentBuilder.AddAttribute(itemSequence++, nameof(StatusBadge.Tone), ProjectionTone(projection.Status, projection.RebuildRequired));
                contentBuilder.AddAttribute(itemSequence++, nameof(StatusBadge.Text), projection.RebuildRequired ? "Rebuild required" : FormatLabel(projection.Status));
                contentBuilder.CloseComponent();
                if (!string.IsNullOrWhiteSpace(projection.FailureMessage))
                {
                    contentBuilder.OpenElement(itemSequence++, "small");
                    contentBuilder.AddContent(itemSequence++, projection.FailureMessage);
                    contentBuilder.CloseElement();
                }

                contentBuilder.CloseElement();
            }
        }));
        builder.CloseComponent();
    }

    private void RenderConsolidationRows(RenderTreeBuilder builder, int sequence)
    {
        if (snapshot is null)
        {
            return;
        }

        builder.OpenComponent<Stack>(sequence);
        builder.AddAttribute(sequence + 1, nameof(Stack.GapScale), LayoutGap.Small);
        builder.AddAttribute(sequence + 2, nameof(Stack.Class), "mt-4");
        builder.AddAttribute(sequence + 3, nameof(Stack.ChildContent), (RenderFragment)(contentBuilder =>
        {
            var itemSequence = 0;
            foreach (var run in snapshot.ConsolidationRuns)
            {
                contentBuilder.OpenElement(itemSequence++, "div");
                contentBuilder.AddAttribute(itemSequence++, "class", "cognitive-memory-row");
                contentBuilder.OpenElement(itemSequence++, "strong");
                contentBuilder.AddContent(itemSequence++, $"{FormatLabel(run.Mode)} / {FormatShortId(run.Id)}");
                contentBuilder.CloseElement();
                contentBuilder.OpenComponent<StatusBadge>(itemSequence++);
                contentBuilder.AddAttribute(itemSequence++, nameof(StatusBadge.Tone), RunTone(run.Status));
                contentBuilder.AddAttribute(itemSequence++, nameof(StatusBadge.Text), FormatLabel(run.Status));
                contentBuilder.CloseComponent();
                contentBuilder.OpenElement(itemSequence++, "span");
                contentBuilder.AddContent(itemSequence++, $"{run.SourceItemsScanned} source item(s), {run.CandidatesCreated} candidate(s), {run.ReviewItemsCreated} review item(s), {run.ProjectionInvalidations} projection invalidation(s)");
                contentBuilder.CloseElement();
                if (!string.IsNullOrWhiteSpace(run.FailureMessage))
                {
                    contentBuilder.OpenElement(itemSequence++, "small");
                    contentBuilder.AddContent(itemSequence++, run.FailureMessage);
                    contentBuilder.CloseElement();
                }

                contentBuilder.CloseElement();
            }
        }));
        builder.CloseComponent();
    }

    private void RenderReplayRows(RenderTreeBuilder builder, int sequence)
    {
        if (snapshot is null)
        {
            return;
        }

        builder.OpenComponent<Stack>(sequence);
        builder.AddAttribute(sequence + 1, nameof(Stack.GapScale), LayoutGap.Small);
        builder.AddAttribute(sequence + 2, nameof(Stack.Class), "mt-4");
        builder.AddAttribute(sequence + 3, nameof(Stack.ChildContent), (RenderFragment)(contentBuilder =>
        {
            var itemSequence = 0;
            foreach (var job in snapshot.ReplayJobs)
            {
                contentBuilder.OpenElement(itemSequence++, "div");
                contentBuilder.AddAttribute(itemSequence++, "class", "cognitive-memory-row");
                contentBuilder.OpenElement(itemSequence++, "strong");
                contentBuilder.AddContent(itemSequence++, $"{FormatLabel(job.JobKind)} / priority {job.QueuePriority}");
                contentBuilder.CloseElement();
                contentBuilder.OpenComponent<StatusBadge>(itemSequence++);
                contentBuilder.AddAttribute(itemSequence++, nameof(StatusBadge.Tone), ReplayTone(job.State));
                contentBuilder.AddAttribute(itemSequence++, nameof(StatusBadge.Text), FormatLabel(job.State));
                contentBuilder.CloseComponent();
                contentBuilder.OpenElement(itemSequence++, "span");
                contentBuilder.AddContent(itemSequence++, FirstNonEmpty(job.Reason, job.FailureMessage, "Replay job has no reason text."));
                contentBuilder.CloseElement();
                contentBuilder.CloseElement();
            }
        }));
        builder.CloseComponent();
    }

    private void RenderProcedureRows(RenderTreeBuilder builder, int sequence)
    {
        if (snapshot is null)
        {
            return;
        }

        builder.OpenComponent<Stack>(sequence);
        builder.AddAttribute(sequence + 1, nameof(Stack.GapScale), LayoutGap.Small);
        builder.AddAttribute(sequence + 2, nameof(Stack.Class), "mt-4");
        builder.AddAttribute(sequence + 3, nameof(Stack.ChildContent), (RenderFragment)(contentBuilder =>
        {
            var itemSequence = 0;
            foreach (var skill in snapshot.ProcedureSkills)
            {
                contentBuilder.OpenElement(itemSequence++, "div");
                contentBuilder.AddAttribute(itemSequence++, "class", "cognitive-memory-row");
                contentBuilder.OpenElement(itemSequence++, "strong");
                contentBuilder.AddContent(itemSequence++, skill.Title);
                contentBuilder.CloseElement();
                contentBuilder.OpenComponent<StatusBadge>(itemSequence++);
                contentBuilder.AddAttribute(itemSequence++, nameof(StatusBadge.Tone), ProcedureTone(skill.Maturity, skill.RiskLevel));
                contentBuilder.AddAttribute(itemSequence++, nameof(StatusBadge.Text), $"{FormatLabel(skill.Maturity)} / {FormatLabel(skill.RiskLevel)}");
                contentBuilder.CloseComponent();
                contentBuilder.OpenElement(itemSequence++, "span");
                contentBuilder.AddContent(itemSequence++, $"{skill.StepCount} step(s), {skill.FailureModeCount} failure mode(s), {skill.ValidationEvidenceCount} evidence link(s), maturity {ScoreText(skill.DisplayMaturityScore)}");
                contentBuilder.CloseElement();
                contentBuilder.CloseElement();
            }
        }));
        builder.CloseComponent();
    }

    private void RenderProbeRows(RenderTreeBuilder builder, int sequence)
    {
        if (snapshot is null)
        {
            return;
        }

        RenderRows(
            builder,
            sequence,
            snapshot.ProbeSessions,
            (contentBuilder, session, itemSequence) =>
            {
                RenderRowStart(contentBuilder, ref itemSequence, session.Title);
                RenderStatus(contentBuilder, ref itemSequence, ProbeTone(session.Status), FormatLabel(session.Status));
                RenderRowText(contentBuilder, ref itemSequence, $"{FormatLabel(session.RecallMode)} / {session.TurnCount} turn(s) / updated {FormatDate(session.UpdatedAtUtc)}");
                contentBuilder.CloseElement();
            });
    }

    private void RenderSelfRegulationRows(RenderTreeBuilder builder, int sequence)
    {
        if (snapshot is null)
        {
            return;
        }

        RenderRows(
            builder,
            sequence,
            snapshot.SelfRegulationAssessments,
            (contentBuilder, assessment, itemSequence) =>
            {
                RenderRowStart(contentBuilder, ref itemSequence, $"{FirstNonEmpty(assessment.DomainKey, "domain")} / {FirstNonEmpty(assessment.TaskTypeKey, "task")}");
                RenderStatus(contentBuilder, ref itemSequence, SelfRegulationTone(assessment.State), FormatLabel(assessment.State));
                RenderRowText(contentBuilder, ref itemSequence, $"{FormatLabel(assessment.AssessmentBucket)} / score {ScoreText(assessment.DisplayAssessmentScore)}");
                contentBuilder.CloseElement();
            });
    }

    private void RenderAnswerGateRows(RenderTreeBuilder builder, int sequence)
    {
        if (snapshot is null)
        {
            return;
        }

        RenderRows(
            builder,
            sequence,
            snapshot.AnswerGateDecisions,
            (contentBuilder, decision, itemSequence) =>
            {
                RenderRowStart(contentBuilder, ref itemSequence, $"{FormatLabel(decision.DecisionKind)} / {FormatShortId(decision.Id)}");
                RenderStatus(contentBuilder, ref itemSequence, AnswerGateTone(decision.DecisionKind), FormatLabel(decision.DecisionBucket));
                RenderRowText(contentBuilder, ref itemSequence, FirstNonEmpty(decision.Reason, $"Confidence {ScoreText(decision.DisplayConfidenceProjection)}"));
                contentBuilder.CloseElement();
            });
    }

    private void RenderProfessorReviewRows(RenderTreeBuilder builder, int sequence)
    {
        if (snapshot is null)
        {
            return;
        }

        RenderRows(
            builder,
            sequence,
            snapshot.ProfessorReviews,
            (contentBuilder, review, itemSequence) =>
            {
                RenderRowStart(contentBuilder, ref itemSequence, FirstNonEmpty(review.InputSummary, FormatLabel(review.ReviewMode)));
                RenderStatus(contentBuilder, ref itemSequence, ProfessorReviewTone(review.Status), FormatLabel(review.Status));
                RenderRowText(contentBuilder, ref itemSequence, $"{FormatLabel(review.ReviewMode)} / requested by {review.RequestedByActorId}");
                if (!string.IsNullOrWhiteSpace(review.MissingEvidence))
                {
                    RenderRowSmall(contentBuilder, ref itemSequence, review.MissingEvidence);
                }

                contentBuilder.CloseElement();
            });
    }

    private void RenderLearningProposalRows(RenderTreeBuilder builder, int sequence)
    {
        if (snapshot is null)
        {
            return;
        }

        RenderRows(
            builder,
            sequence,
            snapshot.LearningProposals,
            (contentBuilder, proposal, itemSequence) =>
            {
                RenderRowStart(contentBuilder, ref itemSequence, proposal.Title);
                RenderStatus(contentBuilder, ref itemSequence, LearningProposalTone(proposal.Status), FormatLabel(proposal.Status));
                RenderRowText(contentBuilder, ref itemSequence, $"{FormatLabel(proposal.NeedBucket)} / priority {ScoreText(proposal.DisplayPriorityProjection)}");
                contentBuilder.CloseElement();
            });
    }

    private void RenderCrossProjectRows(RenderTreeBuilder builder, int sequence)
    {
        if (snapshot is null)
        {
            return;
        }

        RenderRows(
            builder,
            sequence,
            snapshot.CrossProjectPromotions,
            (contentBuilder, candidate, itemSequence) =>
            {
                RenderRowStart(contentBuilder, ref itemSequence, $"Memory {FormatShortId(candidate.SourceMemoryRecordId)}");
                RenderStatus(contentBuilder, ref itemSequence, CrossProjectTone(candidate.Status), $"{FormatLabel(candidate.Status)} / {FormatLabel(candidate.PromotionBucket)}");
                RenderRowText(contentBuilder, ref itemSequence, FirstNonEmpty(candidate.Reason, "Cross-project promotion requires review."));
                contentBuilder.CloseElement();
            });
    }

    private void RenderDistributedRows(RenderTreeBuilder builder, int sequence)
    {
        if (snapshot is null)
        {
            return;
        }

        RenderRows(
            builder,
            sequence,
            snapshot.DistributedJobs,
            (contentBuilder, job, itemSequence) =>
            {
                RenderRowStart(contentBuilder, ref itemSequence, $"{FormatLabel(job.JobKind)} / {FirstNonEmpty(job.SourceScopeKey, FormatShortId(job.Id))}");
                RenderStatus(contentBuilder, ref itemSequence, DistributedJobTone(job.State), FormatLabel(job.State));
                RenderRowText(contentBuilder, ref itemSequence, string.IsNullOrWhiteSpace(job.LeasedWorkerId)
                    ? $"Created {FormatDate(job.CreatedAtUtc)}"
                    : $"Worker {job.LeasedWorkerId} / updated {FormatDate(job.UpdatedAtUtc)}");
                contentBuilder.CloseElement();
            });
    }

    private static void RenderRows<TItem>(
        RenderTreeBuilder builder,
        int sequence,
        IReadOnlyList<TItem> items,
        Action<RenderTreeBuilder, TItem, int> renderItem)
    {
        builder.OpenComponent<Stack>(sequence);
        builder.AddAttribute(sequence + 1, nameof(Stack.GapScale), LayoutGap.Small);
        builder.AddAttribute(sequence + 2, nameof(Stack.Class), "mt-4");
        builder.AddAttribute(sequence + 3, nameof(Stack.ChildContent), (RenderFragment)(contentBuilder =>
        {
            var itemSequence = 0;
            foreach (var item in items)
            {
                renderItem(contentBuilder, item, itemSequence);
                itemSequence += 20;
            }
        }));
        builder.CloseComponent();
    }

    private static void RenderRowStart(
        RenderTreeBuilder builder,
        ref int sequence,
        string title)
    {
        builder.OpenElement(sequence++, "div");
        builder.AddAttribute(sequence++, "class", "cognitive-memory-row");
        builder.OpenElement(sequence++, "strong");
        builder.AddContent(sequence++, title);
        builder.CloseElement();
    }

    private static void RenderStatus(
        RenderTreeBuilder builder,
        ref int sequence,
        string tone,
        string text)
    {
        builder.OpenComponent<StatusBadge>(sequence++);
        builder.AddAttribute(sequence++, nameof(StatusBadge.Tone), tone);
        builder.AddAttribute(sequence++, nameof(StatusBadge.Text), text);
        builder.CloseComponent();
    }

    private static void RenderRowText(
        RenderTreeBuilder builder,
        ref int sequence,
        string text)
    {
        builder.OpenElement(sequence++, "span");
        builder.AddContent(sequence++, text);
        builder.CloseElement();
    }

    private static void RenderRowSmall(
        RenderTreeBuilder builder,
        ref int sequence,
        string text)
    {
        builder.OpenElement(sequence++, "small");
        builder.AddContent(sequence++, text);
        builder.CloseElement();
    }

    private static void RenderEmptyLine(RenderTreeBuilder builder, int sequence, string text)
    {
        builder.OpenElement(sequence, "p");
        builder.AddAttribute(sequence + 1, "class", "cognitive-memory-muted");
        builder.AddContent(sequence + 2, text);
        builder.CloseElement();
    }

    private static string FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;

    private static string TruncateListText(string value, int maxLength)
    {
        var normalized = string.Join(" ", value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        return normalized.Length <= maxLength
            ? normalized
            : $"{normalized[..Math.Max(0, maxLength - 1)]}...";
    }
}
