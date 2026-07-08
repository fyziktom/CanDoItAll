using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.AgentFramework.Hosting;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Builder;
using CanDoItAll.Processes.Core;
using CanDoItAll.Processes.Drivers.Abstractions;
using CanDoItAll.Processes.Drivers.Standard;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Projections;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.Processes.Templates;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Modules.Processes;

internal sealed partial class AgentFrameworkProcessExecutionAdapter
{
    private static ProcessCompletionIssue? ValidateGroundedOutcomeReferences(
        ProcessRuntimeStepAssignment assignment,
        ProcessStepOutcomeResult output,
        IReadOnlyList<ToolExecutionReceiptRecord>? toolReceipts)
    {
        var ungroundedRefs = FindUngroundedPathReferences(
            assignment,
            EnumerateOutcomePathReferences(output),
            toolReceipts);
        if (ungroundedRefs.Length == 0)
        {
            return null;
        }

        var refSummary = DescribeUngroundedReferenceSet(ungroundedRefs);
        return new ProcessCompletionIssue(
            "process.adapter.ungrounded_outcome_reference",
            $"Step '{assignment.StepKey}' claimed completion but cited {refSummary}. Those refs are not grounded in the current step brief, launch variables, required upstream refs, or current-run tool receipts. Retry the same step, remove the rejected path-like refs from the reason, summary, next actions, and evidence refs, and overwrite the managed artifact if needed. Do not quote or restate rejected literal path strings from diagnostics or earlier attempts. Keep a path-like ref only if this same retry first reads or writes current-run evidence that grounds the exact ref.",
            $"{assignment.RunId}:{assignment.StepInstanceId}:ungrounded-outcome-reference:{ComputeHash(string.Join("|", ungroundedRefs))}",
            assignment.ProducedArtifactSlotIds,
            ProcessDiagnosticRetrySafety.SafeToRetry,
            ProcessDiagnosticIdempotencyClassification.Idempotent);
    }

    private ProcessCompletionIssue? ValidateManagedArtifactBodyReferences(
        ProcessRuntimeStepAssignment assignment,
        ProcessStepOutcomeResult output,
        IReadOnlyList<ToolExecutionReceiptRecord>? toolReceipts)
    {
        if (output.Status != ProcessStepOutcomeStatus.Completed ||
            assignment.ProducedArtifactSlotIds.Count == 0)
        {
            return null;
        }

        var primaryRef = BuildManagedStepArtifactPath(assignment);
        var readResult = workspaceFiles.ReadTextFile(primaryRef, maxCharacters: 200000);
        if (!readResult.Succeeded || string.IsNullOrWhiteSpace(readResult.Content))
        {
            return null;
        }

        var content = readResult.Content;
        if (TryRemoveNonCitableSourceMetadataLines(content, out var sanitizedContent))
        {
            var writeResult = workspaceFiles.WriteTextFile(primaryRef, sanitizedContent, overwrite: true);
            if (writeResult.Succeeded)
            {
                content = sanitizedContent;
            }
        }

        var ungroundedRefs = FindUngroundedPathReferences(
            assignment,
            EnumerateTextPathReferences(content),
            toolReceipts,
            ReadTrustedManagedArtifactGroundingTexts(assignment));
        if (ungroundedRefs.Length == 0)
        {
            return null;
        }

        var refSummary = DescribeUngroundedReferenceSet(ungroundedRefs);
        return new ProcessCompletionIssue(
            "process.adapter.ungrounded_managed_artifact_reference",
            $"Step '{assignment.StepKey}' wrote primary managed artifact '{primaryRef}' with {refSummary}. Those refs are not grounded in the current step brief, launch variables, required upstream refs, or current-run successful tool receipts. Retry the same step, overwrite the artifact, and remove rejected path-like refs from the artifact body, reason, summary, next actions, and evidence refs. Do not quote or restate rejected literal path strings from diagnostics or earlier attempts. Keep a path-like ref only if this same retry first reads or writes current-run evidence that grounds the exact ref.",
            $"{assignment.RunId}:{assignment.StepInstanceId}:ungrounded-managed-artifact-reference:{ComputeHash(string.Join("|", ungroundedRefs))}",
            assignment.ProducedArtifactSlotIds,
            ProcessDiagnosticRetrySafety.SafeToRetry,
            ProcessDiagnosticIdempotencyClassification.Idempotent);
    }

    private static string DescribeUngroundedReferenceSet(IReadOnlyList<string> ungroundedRefs)
        => ungroundedRefs.Count == 1
            ? "1 ungrounded path-like ref"
            : $"{ungroundedRefs.Count} ungrounded path-like refs";

    private static bool TryRemoveNonCitableSourceMetadataLines(
        string content,
        out string sanitizedContent)
    {
        sanitizedContent = Regex.Replace(
            content,
            @"(?im)^\s*(?:[-*]\s*)?SourceDoc(?:Name|Link)\s*:\s*.*(?:\r?\n|$)",
            string.Empty);
        sanitizedContent = Regex.Replace(
            sanitizedContent,
            @"(?im)^.*(?:[A-Za-z]:\\[^\r\n]*\\CanDoItAll\\workspace\\|artifacts/scopes[\\/]|managed-files[\\/]|project-media[\\/]|tool-runs[\\/]).*(?:\r?\n|$)",
            string.Empty);
        sanitizedContent = Regex.Replace(
            sanitizedContent,
            @"(\r?\n){3,}",
            $"{Environment.NewLine}{Environment.NewLine}");
        return !string.Equals(content, sanitizedContent, StringComparison.Ordinal);
    }

    private static ProcessStepOutcomeResult RemoveNonCitableSourceMetadataFromOutcome(
        ProcessStepOutcomeResult output)
    {
        var reason = RemoveNonCitableSourceMetadataText(output.Reason);
        if (string.IsNullOrWhiteSpace(reason))
        {
            reason = "Runtime removed non-citable source metadata from the structured outcome; no citable reason text remained.";
        }

        var summary = RemoveNonCitableSourceMetadataText(output.HumanReadableSummaryMarkdown);
        return new ProcessStepOutcomeResult
        {
            Status = output.Status,
            Reason = reason,
            BranchOutcomeKey = RemoveNonCitableSourceMetadataText(output.BranchOutcomeKey),
            BranchOutcomeTitle = RemoveNonCitableSourceMetadataText(output.BranchOutcomeTitle),
            EvidenceRefs = output.EvidenceRefs
                .Select(RemoveNonCitableEvidenceRef)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            NextActions = output.NextActions
                .Select(RemoveNonCitableSourceMetadataText)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .ToArray(),
            HumanReadableSummaryMarkdown = string.IsNullOrWhiteSpace(summary) ? null : summary
        };
    }

    private static string RemoveNonCitableSourceMetadataText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.Trim();
        return TryRemoveNonCitableSourceMetadataLines(normalized, out var sanitized)
            ? sanitized.Trim()
            : normalized;
    }

    private static string RemoveNonCitableSourceMetadataFragments(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.Trim();
        var withoutNativeWorkspacePaths = Regex.Replace(
            normalized,
            @"[A-Za-z]:\\[^\s`""'<>]*\\CanDoItAll\\workspace\\[^\s`""'<>]*",
            "[non-citable source path removed]",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        var withoutStoragePaths = Regex.Replace(
            withoutNativeWorkspacePaths,
            @"(?:artifacts/scopes|managed-files|project-media|tool-runs)[/\\][^\s`""'<>]*",
            "[non-citable source path removed]",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        return Regex.Replace(withoutStoragePaths, @"\s+", " ").Trim();
    }

    private static string RemoveNonCitableEvidenceRef(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.Trim();
        if (Regex.IsMatch(normalized, @"(?im)^\s*(?:[-*]\s*)?SourceDoc(?:Name|Link)\s*:\s*.*$"))
        {
            return string.Empty;
        }

        var containsManagedProcessRef = normalized.Contains("/process-runs/", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("\\process-runs\\", StringComparison.OrdinalIgnoreCase);
        if (containsManagedProcessRef)
        {
            return normalized;
        }

        return Regex.IsMatch(
            normalized,
            @"(?im)(?:[A-Za-z]:\\[^\r\n]*\\CanDoItAll\\workspace\\|artifacts/scopes[\\/]|managed-files[\\/]|project-media[\\/]|tool-runs[\\/])")
                ? string.Empty
                : normalized;
    }

    private static string[] FindUngroundedPathReferences(
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

        var groundingTexts = BuildOutcomeReferenceGroundingTexts(assignment, toolReceipts, additionalGroundingTexts);
        return distinctRefs
            .Where(candidateRef => !IsOutcomeReferenceGrounded(assignment, candidateRef, groundingTexts))
            .Take(5)
            .ToArray();
    }

    private static IReadOnlyList<string> BuildOutcomeReferenceGroundingTexts(
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
        groundingTexts.AddRange(assignment.RequiredArtifactSlotIds.SelectMany(slotId => EnumerateManagedArtifactEvidenceRefs(assignment, slotId)));
        groundingTexts.AddRange(assignment.ProducedArtifactSlotIds.SelectMany(slotId => EnumerateManagedArtifactEvidenceRefs(assignment, slotId)));

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

    private IReadOnlyList<string> ReadTrustedManagedArtifactGroundingTexts(ProcessRuntimeStepAssignment assignment)
    {
        var primaryRef = NormalizeManagedArtifactRef(BuildManagedStepArtifactPath(assignment));
        return EnumerateTrustedManagedArtifactRefs(assignment, primaryRef)
            .Select(ReadManagedArtifactGroundingText)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToArray();
    }

    private string ReadManagedArtifactGroundingText(string artifactRef)
    {
        var readResult = workspaceFiles.ReadTextFile(artifactRef, maxCharacters: 200000);
        return readResult.Succeeded && !string.IsNullOrWhiteSpace(readResult.Content)
            ? readResult.Content
            : string.Empty;
    }

    private static IEnumerable<string> EnumerateTrustedManagedArtifactRefs(
        ProcessRuntimeStepAssignment assignment,
        string primaryRef)
    {
        var trustedTexts = new List<string?> { assignment.Prompt };
        trustedTexts.AddRange(assignment.LaunchVariables.SelectMany(item => new[] { item.Key, item.Value }));

        foreach (var trustedText in trustedTexts)
        {
            foreach (var candidateRef in EnumerateTextPathReferences(trustedText))
            {
                var normalizedRef = NormalizeManagedArtifactRef(candidateRef);
                if (IsManagedMarkdownArtifactRef(normalizedRef) &&
                    !string.Equals(normalizedRef, primaryRef, StringComparison.OrdinalIgnoreCase))
                {
                    yield return normalizedRef;
                }
            }
        }
    }

    private static bool IsManagedMarkdownArtifactRef(string value)
        => Regex.IsMatch(
            NormalizeManagedArtifactRef(value),
            @"^artifacts/process-runs/[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}/steps/[^/\\]+\.md$",
            RegexOptions.CultureInvariant);

    private static bool IsOutcomeReferenceGrounded(
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

    private static bool IsPathReferenceUnderGroundedRoot(string normalizedCandidate, string groundingText)
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

    private static bool IsGroundingToolReceipt(ToolExecutionReceiptRecord receipt)
        => receipt.ExitSummary.StartsWith("Succeeded", StringComparison.OrdinalIgnoreCase);

    private static IEnumerable<string> EnumerateOutcomePathReferences(ProcessStepOutcomeResult output)
    {
        foreach (var text in EnumerateOutcomeNarrativeText(output))
        {
            foreach (var candidate in EnumerateTextPathReferences(text))
            {
                yield return candidate;
            }
        }
    }

    private static IEnumerable<string> EnumerateTextPathReferences(string? text)
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

    private static IEnumerable<string?> EnumerateOutcomeNarrativeText(ProcessStepOutcomeResult output)
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

    private static string TrimOutcomeReference(string value)
        => value.Trim().Trim('`', '"', '\'', '.', ',', ';', ':', ')', ']', '}');

    private static string NormalizeOutcomeReferenceText(string value)
        => TrimOutcomeReference(value)
            .Replace('\\', '/')
            .Replace("%5C", "/", StringComparison.OrdinalIgnoreCase)
            .Replace("%2F", "/", StringComparison.OrdinalIgnoreCase);

    private static bool LooksLikeAgentOutputContractFailure(Exception exception)
    {
        var text = exception.ToString();
        return ContainsAny(
            text,
            "submit_process_step_outcome",
            "Required finalizer tool",
            "process_step_outcome_result",
            "ProcessStepOutcomeResult",
            "process.step_outcome",
            "agent.finalizer",
            "agent.output");
    }

    private static bool LooksLikeTransientAgentExecutionFailure(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var hasTransientMarker = ContainsAny(
            text,
            "Service request failed",
            "Status: 408",
            "Status: 429",
            "Status: 500",
            "Status: 502",
            "Status: 503",
            "Status: 504",
            "Status: 520",
            "Status: 529",
            "temporarily unavailable",
            "temporary failure",
            "transient",
            "rate limit",
            "timeout",
            "timed out",
            "connection reset",
            "connection refused",
            "transport error");
        if (!hasTransientMarker)
        {
            return false;
        }

        return !LooksLikeRightsOrToolBoundary(text) ||
               LooksLikeProviderRuntimeTransientFailure(text);
    }

    private static bool LooksLikeProviderRuntimeTransientFailure(string text)
    {
        return ContainsAny(
            text,
            "provider detail",
            "provider runtime",
            "service request failed",
            "initialization timed out",
            "initialisation timed out",
            "runtime initialization timed out",
            "runtime initialisation timed out");
    }

    private static string LimitDiagnosticText(string text, int maxLength = 800)
    {
        var normalized = text.ReplaceLineEndings(" ").Trim();
        return normalized.Length <= maxLength
            ? normalized
            : normalized[..maxLength] + "...";
    }

}
