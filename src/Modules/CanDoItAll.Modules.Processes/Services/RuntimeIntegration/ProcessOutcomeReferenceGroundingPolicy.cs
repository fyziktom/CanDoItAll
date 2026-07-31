using System.Text.RegularExpressions;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Processes.Runtime;

using static CanDoItAll.Modules.Processes.ProcessCompletionText;
using static CanDoItAll.Modules.Processes.ProcessManagedArtifactEvidence;

namespace CanDoItAll.Modules.Processes;

internal static class ProcessOutcomeReferenceGroundingPolicy
{
    internal static string[] FindUngroundedPathReferences(
        ProcessRuntimeStepAssignment assignment,
        IEnumerable<string> candidateRefs,
        IReadOnlyList<ToolExecutionReceiptRecord>? toolReceipts,
        IEnumerable<string>? additionalGroundingTexts = null)
    {
        var distinctRefs = candidateRefs
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (distinctRefs.Length == 0)
        {
            return [];
        }

        var groundingTexts = BuildOutcomeReferenceGroundingTexts(
            assignment,
            toolReceipts,
            additionalGroundingTexts);
        return distinctRefs
            .Where(candidateRef => !IsOutcomeReferenceGrounded(
                assignment,
                candidateRef,
                groundingTexts))
            .Take(5)
            .ToArray();
    }

    internal static IReadOnlyList<string> BuildOutcomeReferenceGroundingTexts(
        ProcessRuntimeStepAssignment assignment,
        IReadOnlyList<ToolExecutionReceiptRecord>? toolReceipts,
        IEnumerable<string>? additionalGroundingTexts = null)
    {
        var groundingTexts = new List<string>
        {
            assignment.Prompt,
            BuildManagedStepArtifactPath(assignment),
            BuildManagedStepArtifactRoot(assignment)
        };
        groundingTexts.AddRange(assignment.LaunchVariables.SelectMany(item => new[] { item.Key, item.Value }));
        groundingTexts.AddRange(assignment.RequiredArtifactSlotIds.SelectMany(
            slotId => EnumerateManagedArtifactEvidenceRefs(assignment, slotId)));
        groundingTexts.AddRange(assignment.ProducedArtifactSlotIds.SelectMany(
            slotId => EnumerateManagedArtifactEvidenceRefs(assignment, slotId)));

        if (toolReceipts is not null)
        {
            groundingTexts.AddRange(toolReceipts
                .Where(IsGroundingToolReceipt)
                .SelectMany(receipt => new[]
                {
                    receipt.ToolName,
                    receipt.RequestSummary,
                    receipt.ExitSummary,
                    receipt.WorkingDirectory
                }));
        }

        if (additionalGroundingTexts is not null)
        {
            groundingTexts.AddRange(additionalGroundingTexts);
        }

        return groundingTexts
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(NormalizeOutcomeReferenceText)
            .Where(value => value.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    internal static bool IsManagedMarkdownArtifactRef(string value)
        => Regex.IsMatch(
            NormalizeManagedArtifactRef(value),
            @"^artifacts/process-runs/[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}/steps/[^/\\]+\.md$",
            RegexOptions.CultureInvariant);

    internal static bool IsOutcomeReferenceGrounded(
        ProcessRuntimeStepAssignment assignment,
        string candidateRef,
        IReadOnlyList<string> groundingTexts)
    {
        var normalizedCandidate = NormalizeOutcomeReferenceText(candidateRef);
        if (normalizedCandidate.Length == 0)
        {
            return true;
        }

        var currentRunManagedRoot = $"process-runs/{assignment.RunId.Value:D}";
        if (normalizedCandidate.Contains(currentRunManagedRoot, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return groundingTexts.Any(groundingText =>
            groundingText.Contains(normalizedCandidate, StringComparison.OrdinalIgnoreCase) ||
            IsPathReferenceUnderGroundedRoot(normalizedCandidate, groundingText));
    }

    internal static bool IsPathReferenceUnderGroundedRoot(
        string normalizedCandidate,
        string groundingText)
    {
        var normalizedRoot = NormalizeOutcomeReferenceText(groundingText).TrimEnd('/');
        if (normalizedRoot.Length == 0 ||
            normalizedCandidate.Length <= normalizedRoot.Length ||
            !normalizedCandidate.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return normalizedCandidate[normalizedRoot.Length] == '/';
    }

    internal static bool IsGroundingToolReceipt(ToolExecutionReceiptRecord receipt)
        => receipt.ExitSummary.StartsWith("Succeeded", StringComparison.OrdinalIgnoreCase);

    internal static IEnumerable<string> EnumerateOutcomePathReferences(
        ProcessStepOutcomeResult output)
    {
        foreach (var text in EnumerateOutcomeNarrativeText(output))
        {
            foreach (var candidate in EnumerateTextPathReferences(text))
            {
                yield return candidate;
            }
        }

        foreach (var evidenceRef in output.EvidenceRefs)
        {
            foreach (var candidate in EnumerateTextPathReferences(evidenceRef))
            {
                yield return candidate;
            }
        }
    }

    internal static IEnumerable<string> EnumerateAcceptanceCriteriaPathReferences(
        ProcessStepOutcomeResult output)
    {
        foreach (var evidence in output.AcceptanceCriteriaEvidence ?? [])
        {
            if (evidence is null)
            {
                continue;
            }

            foreach (var candidate in EnumerateTextPathReferences(evidence.CriterionId))
            {
                yield return candidate;
            }

            foreach (var candidate in EnumerateTextPathReferences(evidence.Summary))
            {
                yield return candidate;
            }

            foreach (var evidenceRef in evidence.EvidenceRefs ?? [])
            {
                foreach (var candidate in EnumerateTextPathReferences(evidenceRef))
                {
                    yield return candidate;
                }
            }
        }
    }

    internal static IEnumerable<string> EnumerateTextPathReferences(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            yield break;
        }

        foreach (Match match in OutcomePathReferenceRegex().Matches(text))
        {
            var candidate = TrimOutcomeReference(match.Value);
            if (!string.IsNullOrWhiteSpace(candidate))
            {
                yield return candidate;
            }
        }
    }

    internal static IEnumerable<string?> EnumerateOutcomeNarrativeText(
        ProcessStepOutcomeResult output)
    {
        yield return output.Reason;
        yield return output.BranchOutcomeKey;
        yield return output.BranchOutcomeTitle;
        yield return output.HumanReadableSummaryMarkdown;

        foreach (var nextAction in output.NextActions)
        {
            yield return nextAction;
        }
    }

    internal static string TrimOutcomeReference(string value)
        => value.Trim().Trim('`', '"', '\'', '*', '.', ',', ';', ':', ')', ']', '}');

    internal static string NormalizeOutcomeReferenceText(string value)
        => TrimOutcomeReference(value)
            .Replace('\\', '/')
            .Replace("%5C", "/", StringComparison.OrdinalIgnoreCase)
            .Replace("%2F", "/", StringComparison.OrdinalIgnoreCase);
}
