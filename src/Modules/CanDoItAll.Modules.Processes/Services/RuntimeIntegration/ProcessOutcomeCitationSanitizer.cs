using System.Text.RegularExpressions;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Modules.Processes;

internal static class ProcessOutcomeCitationSanitizer
{
    internal static bool TryRemoveNonCitableSourceMetadataLines(
        string content,
        out string sanitizedContent)
    {
        var protectedEnvelopes = new List<ProtectedEnvelope>();
        if (!TryProtectCompleteEnvelopes(
            content,
            ParentSubprocessVerifiedChildOutputEnvelope.BeginMarker,
            ParentSubprocessVerifiedChildOutputEnvelope.EndMarker,
            protectedEnvelopes,
            out var contentForSanitization) ||
            !TryProtectCompleteEnvelopes(
            contentForSanitization,
            ParentSubprocessForwardedContextEnvelope.BeginMarker,
            ParentSubprocessForwardedContextEnvelope.EndMarker,
            protectedEnvelopes,
            out contentForSanitization))
        {
            sanitizedContent = content;
            return false;
        }

        if (ParentSubprocessVerifiedChildOutputEnvelope.ContainsReservedMarker(contentForSanitization) ||
            ParentSubprocessForwardedContextEnvelope.ContainsReservedMarker(contentForSanitization))
        {
            sanitizedContent = content;
            return false;
        }

        sanitizedContent = Regex.Replace(
            contentForSanitization,
            @"(?im)^\s*(?:[-*]\s*)?SourceDoc(?:Name|Link)\s*:\s*.*(?:\r?\n|$)",
            string.Empty);
        sanitizedContent = Regex.Replace(
            sanitizedContent,
            @"(?im)^.*(?:[A-Za-z]:\\[^\r\n]*\\CanDoItAll\\workspace\\|artifacts/scopes[\\/]|managed-files[\\/]|project-media[\\/]|tool-runs[\\/]).*(?:\r?\n|$)",
            string.Empty);
        if (!string.Equals(contentForSanitization, sanitizedContent, StringComparison.Ordinal))
        {
            sanitizedContent = Regex.Replace(
                sanitizedContent,
                @"(\r?\n){3,}",
                $"{Environment.NewLine}{Environment.NewLine}");
        }
        for (var index = protectedEnvelopes.Count - 1; index >= 0; index--)
        {
            var protectedEnvelope = protectedEnvelopes[index];
            sanitizedContent = sanitizedContent.Replace(
                protectedEnvelope.Placeholder,
                protectedEnvelope.Content,
                StringComparison.Ordinal);
        }

        return !string.Equals(content, sanitizedContent, StringComparison.Ordinal);
    }

    internal static ProcessStepOutcomeResult RemoveNonCitableSourceMetadataFromOutcome(
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
            AcceptanceCriteriaEvidence = SanitizeAcceptanceCriteriaEvidence(output.AcceptanceCriteriaEvidence),
            NextActions = output.NextActions
                .Select(RemoveNonCitableSourceMetadataText)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .ToArray(),
            HumanReadableSummaryMarkdown = string.IsNullOrWhiteSpace(summary) ? null : summary
        };
    }

    internal static string RemoveNonCitableSourceMetadataText(string? value)
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

    internal static string RemoveNonCitableSourceMetadataFragments(string? value)
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

    internal static string RemoveNonCitableEvidenceRef(string? value)
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

    private static IReadOnlyList<ProcessAcceptanceCriterionEvidence> SanitizeAcceptanceCriteriaEvidence(
        IReadOnlyList<ProcessAcceptanceCriterionEvidence> evidence)
        => (evidence ?? [])
            .Where(item => item is not null)
            .Select(item => new ProcessAcceptanceCriterionEvidence
            {
                CriterionId = RemoveNonCitableSourceMetadataText(item.CriterionId),
                Status = item.Status,
                Summary = RemoveNonCitableSourceMetadataText(item.Summary),
                EvidenceRefs = (item.EvidenceRefs ?? [])
                    .Select(RemoveNonCitableEvidenceRef)
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray()
            })
            .ToArray();

    private static bool TryProtectCompleteEnvelopes(
        string content,
        string beginMarker,
        string endMarker,
        ICollection<ProtectedEnvelope> protectedEnvelopes,
        out string protectedContent)
    {
        protectedContent = content;
        if (!ParentSubprocessRuntimeEnvelopeFraming.TryFindTopLevelSpans(
                content,
                beginMarker,
                endMarker,
                out var spans))
        {
            return false;
        }

        foreach (var span in spans.OrderByDescending(span => span.Start))
        {
            var envelope = protectedContent.Substring(span.Start, span.Length);
            var placeholder = BuildUniquePlaceholder(content, protectedEnvelopes.Count);
            protectedEnvelopes.Add(new ProtectedEnvelope(placeholder, envelope));
            protectedContent = protectedContent
                .Remove(span.Start, span.Length)
                .Insert(span.Start, placeholder);
        }

        return true;
    }

    private static string BuildUniquePlaceholder(string content, int index)
    {
        var candidateIndex = index;
        while (true)
        {
            var placeholder = $"\u001Ecandoitall-runtime-envelope-{candidateIndex}\u001E";
            if (!content.Contains(placeholder, StringComparison.Ordinal))
            {
                return placeholder;
            }

            candidateIndex++;
        }
    }

    private sealed record ProtectedEnvelope(string Placeholder, string Content);
}
