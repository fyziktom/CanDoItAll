using CanDoItAll.Components.BaseLib;
using CanDoItAll.Components.Common;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using System.Text;

namespace CanDoItAll.Modules.CognitiveMemory.Pages;

public partial class CognitiveMemoryPage
{
    private const string OperatorActorId = "cognitive-memory-operator-ui";

    [Inject]
    public ICognitiveMemoryReviewUiService ReviewUiService { get; set; } = default!;

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

    private CognitiveMemoryReviewQueueItem? SelectedReviewItem
        => snapshot?.ReviewItems.FirstOrDefault(item => item.Id.Value == selectedReviewItemId);

    private CognitiveMemoryExplorerItem? SelectedMemoryRecord
        => snapshot?.MemoryRecords.FirstOrDefault(record => record.Id.Value == selectedMemoryRecordId);

    private CognitiveMemoryRecallTraceView? SelectedRecallTrace
        => snapshot?.RecallTraces.FirstOrDefault(trace => trace.Id == selectedRecallTraceId);

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

    protected override async Task OnInitializedAsync()
    {
        await RefreshAsync();
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
        snapshot = await ReviewUiService.GetSnapshotAsync(
            new CognitiveMemoryReviewUiQuery(ProjectId),
            CancellationToken.None);
        selectedMemoryRecordId = ResolveSelectedMemoryRecordId(snapshot, selectedMemoryRecordId);
        selectedReviewItemId = ResolveSelectedReviewItemId(snapshot, selectedReviewItemId);
        selectedRecallTraceId = ResolveSelectedRecallTraceId(snapshot, selectedRecallTraceId);
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

    private static void RenderEmptyLine(RenderTreeBuilder builder, int sequence, string text)
    {
        builder.OpenElement(sequence, "p");
        builder.AddAttribute(sequence + 1, "class", "cognitive-memory-muted");
        builder.AddContent(sequence + 2, text);
        builder.CloseElement();
    }

    private static string FirstNonEmpty(params string[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;
}
