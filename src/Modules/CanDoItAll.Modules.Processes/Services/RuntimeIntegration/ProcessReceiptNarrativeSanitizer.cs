using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Processes.Contracts;
using CanDoItAll.Processes.Drivers.Abstractions;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Processes;

internal static class ProcessReceiptNarrativeSanitizer
{
    private const string ConfiguredRootLabel = "[configured product root]";
    private const int MaximumEvidenceReferences = 64;
    private const int MaximumAcceptanceCriteria = 64;
    private const int MaximumNextActions = 16;

    internal static bool IsBoundedShape(ProcessStepOutcomeResult? output)
    {
        if (output is null ||
            !Enum.IsDefined(output.Status) ||
            !IsBoundedRequiredText(output.Reason, ProcessStrategyResultLimits.MaximumUserSafeSummaryLength) ||
            !IsBoundedOptionalText(output.BranchOutcomeKey, ProcessStrategyResultLimits.MaximumIdentifierLength) ||
            !IsBoundedOptionalText(output.BranchOutcomeTitle, ProcessStrategyResultLimits.MaximumManagerSignalSummaryLength) ||
            !IsBoundedOptionalText(output.HumanReadableSummaryMarkdown, ProcessStrategyResultLimits.MaximumUserSafeSummaryLength) ||
            !IsBoundedTextList(output.EvidenceRefs, MaximumEvidenceReferences, ProcessStrategyResultLimits.MaximumRestrictedEvidenceReferenceLength) ||
            !IsBoundedTextList(output.NextActions, MaximumNextActions, ProcessStrategyResultLimits.MaximumDiagnosticSummaryLength) ||
            output.AcceptanceCriteriaEvidence is null ||
            output.AcceptanceCriteriaEvidence.Count > MaximumAcceptanceCriteria)
        {
            return false;
        }

        return output.AcceptanceCriteriaEvidence.All(item =>
            item is not null &&
            Enum.IsDefined(item.Status) &&
            IsBoundedRequiredText(item.CriterionId, ProcessStrategyResultLimits.MaximumIdentifierLength) &&
            IsBoundedRequiredText(item.Summary, ProcessStrategyResultLimits.MaximumDiagnosticSummaryLength) &&
            IsBoundedTextList(
                item.EvidenceRefs,
                MaximumEvidenceReferences,
                ProcessStrategyResultLimits.MaximumRestrictedEvidenceReferenceLength));
    }

    internal static ProcessStepOutcomeResult Sanitize(
        ProcessRuntimeStepAssignment assignment,
        ProcessStepOutcomeResult output)
    {
        ArgumentNullException.ThrowIfNull(assignment);
        ArgumentNullException.ThrowIfNull(output);

        return new ProcessStepOutcomeResult
        {
            Status = output.Status,
            Reason = SanitizeText(
                assignment,
                output.Reason,
                ProcessStrategyResultLimits.MaximumUserSafeSummaryLength),
            BranchOutcomeKey = SanitizeText(
                assignment,
                output.BranchOutcomeKey,
                ProcessStrategyResultLimits.MaximumIdentifierLength),
            BranchOutcomeTitle = SanitizeText(
                assignment,
                output.BranchOutcomeTitle,
                ProcessStrategyResultLimits.MaximumManagerSignalSummaryLength),
            EvidenceRefs = (output.EvidenceRefs ?? [])
                .Take(MaximumEvidenceReferences)
                .Select(reference => SanitizeText(
                    assignment,
                    reference,
                    ProcessStrategyResultLimits.MaximumRestrictedEvidenceReferenceLength))
                .Where(reference => !string.IsNullOrWhiteSpace(reference))
                .Distinct(StringComparer.Ordinal)
                .ToArray(),
            AcceptanceCriteriaEvidence = (output.AcceptanceCriteriaEvidence ?? [])
                .Where(item => item is not null)
                .Take(MaximumAcceptanceCriteria)
                .Select(item => new ProcessAcceptanceCriterionEvidence
                {
                    CriterionId = SanitizeText(
                        assignment,
                        item.CriterionId,
                        ProcessStrategyResultLimits.MaximumIdentifierLength),
                    Status = item.Status,
                    Summary = SanitizeText(
                        assignment,
                        item.Summary,
                        ProcessStrategyResultLimits.MaximumDiagnosticSummaryLength),
                    EvidenceRefs = (item.EvidenceRefs ?? [])
                        .Take(MaximumEvidenceReferences)
                        .Select(reference => SanitizeText(
                            assignment,
                            reference,
                            ProcessStrategyResultLimits.MaximumRestrictedEvidenceReferenceLength))
                        .Where(reference => !string.IsNullOrWhiteSpace(reference))
                        .Distinct(StringComparer.Ordinal)
                        .ToArray()
                })
                .ToArray(),
            NextActions = (output.NextActions ?? [])
                .Take(MaximumNextActions)
                .Select(action => SanitizeText(
                    assignment,
                    action,
                    ProcessStrategyResultLimits.MaximumDiagnosticSummaryLength))
                .Where(action => !string.IsNullOrWhiteSpace(action))
                .ToArray(),
            HumanReadableSummaryMarkdown = SanitizeText(
                assignment,
                output.HumanReadableSummaryMarkdown,
                ProcessStrategyResultLimits.MaximumUserSafeSummaryLength)
        };
    }

    internal static string SanitizeText(
        ProcessRuntimeStepAssignment assignment,
        string? value,
        int maximumLength)
    {
        var sanitized = SensitiveTextRedactor.Redact(value);
        foreach (var protectedRoot in ResolveProtectedRootSpellings(assignment.LaunchVariables))
        {
            sanitized = sanitized.Replace(
                protectedRoot,
                ConfiguredRootLabel,
                StringComparison.OrdinalIgnoreCase);
        }

        sanitized = ProcessPublicReceiptTextPolicy.Sanitize(sanitized).Trim();
        return sanitized.Length <= maximumLength
            ? sanitized
            : sanitized[..(maximumLength - 3)].TrimEnd() + "...";
    }

    private static IReadOnlyList<string> ResolveProtectedRootSpellings(
        IReadOnlyDictionary<string, string> launchVariables)
    {
        var roots = new[]
        {
            ResolveLaunchVariable(launchVariables, ProcessRuntimeLaunchVariables.ProductRoot),
            ResolveLaunchVariable(launchVariables, ProcessRuntimeLaunchVariables.OutputRoot),
            ResolveLaunchVariable(launchVariables, "ExternalTargetRoot")
        };
        return roots
            .Where(root => !string.IsNullOrWhiteSpace(root) && !ExternalTargetAliasCodec.IsAnyAlias(root))
            .SelectMany(root =>
            {
                var trimmed = root.TrimEnd('\\', '/');
                return new[]
                {
                    root,
                    trimmed,
                    root.Replace('\\', '/'),
                    trimmed.Replace('\\', '/'),
                    root.Replace('/', '\\'),
                    trimmed.Replace('/', '\\')
                };
            })
            .Where(root => !string.IsNullOrWhiteSpace(root))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(root => root.Length)
            .ToArray();
    }

    private static bool IsBoundedTextList(
        IReadOnlyList<string>? values,
        int maximumCount,
        int maximumLength)
        => values is not null &&
           values.Count <= maximumCount &&
           values.All(value => IsBoundedRequiredText(value, maximumLength));

    private static bool IsBoundedRequiredText(string? value, int maximumLength)
        => value is not null && value.Length <= maximumLength;

    private static bool IsBoundedOptionalText(string? value, int maximumLength)
        => value is null || value.Length <= maximumLength;

    private static string ResolveLaunchVariable(
        IReadOnlyDictionary<string, string> launchVariables,
        string key)
        => launchVariables.TryGetValue(key, out var value)
            ? value
            : string.Empty;
}
