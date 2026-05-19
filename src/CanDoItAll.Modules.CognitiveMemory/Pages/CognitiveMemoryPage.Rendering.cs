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
    internal static void RenderFact(
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

    internal static RenderFragment RenderFactFragment(string label, string value)
        => builder => RenderFact(builder, 0, label, value);

    internal void RenderDecisionButton(
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

    internal static void RenderTraceCollections(
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

    internal static void RenderCandidateRows(
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

    internal static void RenderSourceRows(
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

    internal static void RenderMemorySourceRows(
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

    internal void RenderProjectionRows(RenderTreeBuilder builder, int sequence)
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

    internal void RenderConsolidationRows(RenderTreeBuilder builder, int sequence)
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

    internal void RenderReplayRows(RenderTreeBuilder builder, int sequence)
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

    internal void RenderProcedureRows(RenderTreeBuilder builder, int sequence)
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

    internal void RenderProbeRows(RenderTreeBuilder builder, int sequence)
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

    internal void RenderSelfRegulationRows(RenderTreeBuilder builder, int sequence)
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

    internal void RenderAnswerGateRows(RenderTreeBuilder builder, int sequence)
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

    internal void RenderProfessorReviewRows(RenderTreeBuilder builder, int sequence)
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

    internal void RenderLearningProposalRows(RenderTreeBuilder builder, int sequence)
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

    internal void RenderCrossProjectRows(RenderTreeBuilder builder, int sequence)
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

    internal void RenderDistributedRows(RenderTreeBuilder builder, int sequence)
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

    internal static void RenderRows<TItem>(
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

    internal static void RenderRowStart(
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

    internal static void RenderStatus(
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

    internal static void RenderRowText(
        RenderTreeBuilder builder,
        ref int sequence,
        string text)
    {
        builder.OpenElement(sequence++, "span");
        builder.AddContent(sequence++, text);
        builder.CloseElement();
    }

    internal static void RenderRowSmall(
        RenderTreeBuilder builder,
        ref int sequence,
        string text)
    {
        builder.OpenElement(sequence++, "small");
        builder.AddContent(sequence++, text);
        builder.CloseElement();
    }

    internal static void RenderEmptyLine(RenderTreeBuilder builder, int sequence, string text)
    {
        builder.OpenElement(sequence, "p");
        builder.AddAttribute(sequence + 1, "class", "cognitive-memory-muted");
        builder.AddContent(sequence + 2, text);
        builder.CloseElement();
    }

    internal static string FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;

    internal static string TruncateListText(string value, int maxLength)
    {
        var normalized = string.Join(" ", value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        return normalized.Length <= maxLength
            ? normalized
            : $"{normalized[..Math.Max(0, maxLength - 1)]}...";
    }

    internal sealed class CognitiveMemoryProviderSelection(
        Guid id,
        string name,
        ProviderKind kind,
        string defaultModel,
        string baseUrl,
        bool isEnabled,
        bool isLocal,
        bool isAllowed)
    {
        public Guid Id { get; } = id;

        public string Name { get; } = name;

        public ProviderKind Kind { get; } = kind;

        public string DefaultModel { get; } = defaultModel;

        public string BaseUrl { get; } = baseUrl;

        public bool IsEnabled { get; } = isEnabled;

        public bool IsLocal { get; } = isLocal;

        public bool IsAllowed { get; set; } = isAllowed;
    }

    internal enum CognitiveMemoryProbeVoiceCaptureTarget
    {
        Question,
        Correction,
        Confirmation
    }
}
