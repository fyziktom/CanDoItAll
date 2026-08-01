using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Processes.Drivers.Abstractions;
using CanDoItAll.Processes.Runtime;

using static CanDoItAll.Modules.Processes.ProcessManagedArtifactOutcomeParser;

namespace CanDoItAll.Modules.Processes;

internal static class ProcessOutcomeGroundingSanitizer
{
    internal static bool CanRemoveUngroundedNonAuthoritativeCriterionEvidenceRefs(
        ProcessRuntimeStepAssignment assignment,
        ProcessStepOutcomeResult output,
        ProcessStepExecutionContract stepContract)
        => output.Status == ProcessStepOutcomeStatus.Completed &&
           (string.IsNullOrWhiteSpace(output.BranchOutcomeKey) ||
            (ProcessBranchOutcomeResolver.TryResolveExactConfiguredBranchOutcome(
                 output,
                 stepContract,
                 out _) &&
             !ProcessAcceptanceCriteriaGate.IsAcceptanceCriteriaBranch(
                 assignment,
                 output.BranchOutcomeKey)));

    internal static ProcessStepOutcomeResult RemoveUngroundedNonAuthoritativeCriterionEvidenceRefs(
        ProcessRuntimeStepAssignment assignment,
        ProcessStepOutcomeResult output,
        IReadOnlyList<string> groundingTexts,
        out int removedCount)
    {
        removedCount = 0;
        var authoritativeRefs = ProcessOutcomeReferenceGroundingPolicy
            .EnumerateOutcomePathReferences(output)
            .Concat(EnumerateAuthoritativeAcceptanceCriteriaPathReferences(output));
        if (authoritativeRefs.Any(reference =>
                !ProcessOutcomeReferenceGroundingPolicy.IsOutcomeReferenceGrounded(
                    assignment,
                    reference,
                    groundingTexts)))
        {
            return output;
        }

        var totalRemovedCount = 0;
        var normalizedCriteria = (output.AcceptanceCriteriaEvidence ?? [])
            .Where(evidence => evidence is not null)
            .Select(evidence =>
            {
                if (evidence.Status != ProcessAcceptanceCriterionEvidenceStatus.NotVerified)
                {
                    return evidence;
                }

                var retainedRefs = (evidence.EvidenceRefs ?? [])
                    .Where(evidenceRef => !ProcessOutcomeReferenceGroundingPolicy
                        .EnumerateTextPathReferences(evidenceRef)
                        .Any(reference =>
                            !ProcessOutcomeReferenceGroundingPolicy.IsOutcomeReferenceGrounded(
                                assignment,
                                reference,
                                groundingTexts)))
                    .ToArray();
                var criterionRemovedCount = (evidence.EvidenceRefs ?? []).Count - retainedRefs.Length;
                if (criterionRemovedCount == 0)
                {
                    return evidence;
                }

                totalRemovedCount += criterionRemovedCount;
                return new ProcessAcceptanceCriterionEvidence
                {
                    CriterionId = evidence.CriterionId,
                    Status = evidence.Status,
                    Summary = evidence.Summary,
                    EvidenceRefs = retainedRefs
                };
            })
            .ToArray();
        if (totalRemovedCount == 0)
        {
            return output;
        }

        removedCount = totalRemovedCount;
        return CopyWithAcceptanceCriteriaEvidence(output, normalizedCriteria);
    }

    internal static bool CanRemoveUngroundedSupplementalEvidenceRefsFromGroundedDefectOutcome(
        ProcessRuntimeStepAssignment assignment,
        ProcessStepOutcomeResult output,
        ProcessStepExecutionContract stepContract)
        => output.Status == ProcessStepOutcomeStatus.Completed &&
           !string.IsNullOrWhiteSpace(output.BranchOutcomeKey) &&
           ProcessBranchOutcomeResolver.TryResolveExactConfiguredBranchOutcome(
               output,
               stepContract,
               out _) &&
           !ProcessAcceptanceCriteriaGate.IsAcceptanceCriteriaBranch(
               assignment,
               output.BranchOutcomeKey) &&
           ProcessAcceptanceCriteriaGate.TryGetFailedCriterionEvidence(
               assignment,
               output,
               out _);

    internal static ProcessStepOutcomeResult RemoveUngroundedSupplementalEvidenceRefsFromGroundedDefectOutcome(
        ProcessRuntimeStepAssignment assignment,
        ProcessStepOutcomeResult output,
        IReadOnlyList<ToolExecutionReceiptRecord>? toolReceipts,
        IReadOnlyList<string> groundingTexts,
        out int removedCount)
    {
        removedCount = 0;
        var authoritativeRefs = ProcessOutcomeReferenceGroundingPolicy
            .EnumerateOutcomeNarrativeText(output)
            .SelectMany(ProcessOutcomeReferenceGroundingPolicy.EnumerateTextPathReferences)
            .Concat(ProcessOutcomeReferenceGroundingPolicy
                .EnumerateAcceptanceCriteriaPathReferences(output));
        if (ProcessOutcomeReferenceGroundingPolicy.FindUngroundedPathReferences(
                assignment,
                authoritativeRefs,
                toolReceipts,
                groundingTexts).Length != 0)
        {
            return output;
        }

        var retainedEvidenceRefs = output.EvidenceRefs
            .Where(evidenceRef => ProcessOutcomeReferenceGroundingPolicy.FindUngroundedPathReferences(
                assignment,
                ProcessOutcomeReferenceGroundingPolicy.EnumerateTextPathReferences(evidenceRef),
                toolReceipts,
                groundingTexts).Length == 0)
            .ToArray();
        removedCount = output.EvidenceRefs.Count - retainedEvidenceRefs.Length;
        if (removedCount == 0 ||
            !HasAllManagedArtifactEvidence(assignment, retainedEvidenceRefs))
        {
            removedCount = 0;
            return output;
        }

        return CopyWithEvidenceRefs(output, retainedEvidenceRefs);
    }

    private static IEnumerable<string> EnumerateAuthoritativeAcceptanceCriteriaPathReferences(
        ProcessStepOutcomeResult output)
    {
        foreach (var evidence in output.AcceptanceCriteriaEvidence ?? [])
        {
            if (evidence is null)
            {
                continue;
            }

            foreach (var candidate in ProcessOutcomeReferenceGroundingPolicy
                         .EnumerateTextPathReferences(evidence.CriterionId))
            {
                yield return candidate;
            }

            foreach (var candidate in ProcessOutcomeReferenceGroundingPolicy
                         .EnumerateTextPathReferences(evidence.Summary))
            {
                yield return candidate;
            }

            if (evidence.Status == ProcessAcceptanceCriterionEvidenceStatus.NotVerified)
            {
                continue;
            }

            foreach (var evidenceRef in evidence.EvidenceRefs ?? [])
            {
                foreach (var candidate in ProcessOutcomeReferenceGroundingPolicy
                             .EnumerateTextPathReferences(evidenceRef))
                {
                    yield return candidate;
                }
            }
        }
    }

    private static ProcessStepOutcomeResult CopyWithAcceptanceCriteriaEvidence(
        ProcessStepOutcomeResult output,
        IReadOnlyList<ProcessAcceptanceCriterionEvidence> acceptanceCriteriaEvidence)
        => new()
        {
            Status = output.Status,
            Reason = output.Reason,
            BranchOutcomeKey = output.BranchOutcomeKey,
            BranchOutcomeTitle = output.BranchOutcomeTitle,
            EvidenceRefs = output.EvidenceRefs,
            AcceptanceCriteriaEvidence = acceptanceCriteriaEvidence,
            NextActions = output.NextActions,
            HumanReadableSummaryMarkdown = output.HumanReadableSummaryMarkdown
        };
}
