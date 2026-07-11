using System.Text;
using System.Text.RegularExpressions;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Processes.Application;

internal static class ProcessAutomaticRecoveryPromptBuilder
{
    internal const string ExecutionFocusHeading = "Automatic recovery execution focus:";
    internal const string CompletionChecklistHeading = "Recovery completion checklist:";

    private const int MaxFallbackPromptCharacters = 6000;
    private const int MaxSectionCharacters = 3000;
    private const int MaxLaunchVariableCharacters = 500;
    private const int MaxLaunchVariableBlockCharacters = 6000;
    private const int MaxReferenceCount = 48;

    private static readonly string[] ContractSectionHeadings =
    [
        "Step instructions:",
        "Input contract:",
        "Output contract:",
        "Evidence contract:",
        "Available branch outcomes:"
    ];

    private static readonly Regex GroundedReferenceRegex = new(
        @"(?<![A-Za-z0-9._-])(?:artifacts|external-target)/[^\s`""'<>|,;]+",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex MissingReceiptListRegex = new(
        @"required\s+current-run\s+(?:product|process)?\s*tool\s+receipt\(s\)\s+are\s+missing:\s*(?<receipts>[^.\r\n]+)",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    internal static string Build(
        ProcessRuntimeStepAssignment assignment,
        string runtimeRecoveryInstruction)
    {
        ArgumentNullException.ThrowIfNull(assignment);
        ArgumentException.ThrowIfNullOrWhiteSpace(runtimeRecoveryInstruction);

        var requiredSlots = FormatSlotIds(assignment.RequiredArtifactSlotIds);
        var producedSlots = FormatSlotIds(assignment.ProducedArtifactSlotIds);
        var branchGate = assignment.BranchGate is null
            ? "none"
            : $"{assignment.BranchGate.SourceStepKey} -> {assignment.BranchGate.RequiredOutcomeKey}";
        var groundedRefs = FormatGroundedReferences(assignment.Prompt, runtimeRecoveryInstruction);
        var launchVariables = FormatLaunchVariables(assignment.LaunchVariables);
        var contractExcerpts = FormatContractExcerpts(assignment.Prompt);
        var recoveryChecklist = FormatRecoveryChecklist(runtimeRecoveryInstruction);

        return $"""
        {runtimeRecoveryInstruction.Trim()}

        {ExecutionFocusHeading}
        This is a focused recovery attempt for the same process step, not a restart of its complete discovery and planning work. Resolve the rejected completion gate first. Reuse the existing product state and the exact grounded refs below. Read only the minimum files needed for the repair, perform the required action, verify its current-execution receipt, and only then rewrite the managed artifact and finalize.
        Do not claim an action, correction, rerun, mutation, validation, or artifact write unless this exact execution attempt has the corresponding successful tool receipt.

        {CompletionChecklistHeading}
        {recoveryChecklist}
        Complete this checklist in order. Before writing the primary artifact or calling the finalizer, compare the checklist with the current execution's actual tool calls. Invoke any omitted item first; prose in an artifact does not satisfy it.

        Step identity:
        - run id: {assignment.RunId}
        - step id: {assignment.StepInstanceId}
        - step key: {assignment.StepKey}
        - role: {assignment.RoleKey} - {assignment.RoleDisplayName}
        - executor: {assignment.ExecutorDisplayName}
        - allowed operations: {string.Join(", ", assignment.AllowedOperations)}
        - operation target scope: {assignment.OperationTargetScope}
        - required artifact slots: {requiredSlots}
        - produced artifact slots: {producedSlots}
        - branch gate: {branchGate}

        Exact grounded refs retained from the original step and recovery packet:
        {groundedRefs}

        Compact launch context:
        {launchVariables}

        Original step-contract excerpts:
        {contractExcerpts}
        """;
    }

    private static string FormatRecoveryChecklist(string runtimeRecoveryInstruction)
    {
        var items = MissingReceiptListRegex.Matches(runtimeRecoveryInstruction)
            .SelectMany(match => match.Groups["receipts"].Value.Split(
                [';', ','],
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Where(LooksLikeToolName)
            .Select(toolName => $"Invoke `{toolName}` successfully in this exact execution attempt.")
            .ToList();

        if (runtimeRecoveryInstruction.Contains(
                ProcessCompletionDiagnosticCodes.ProductSourceInspectionEvidenceMissing,
                StringComparison.OrdinalIgnoreCase))
        {
            items.Add("Read concrete owning product source under the grounded external-target alias in this execution. For a workflow defect, read application/component/domain source rather than only navigation or styling.");
        }

        if (runtimeRecoveryInstruction.Contains(
                "process.adapter.runtime_lifecycle_correlation_missing",
                StringComparison.OrdinalIgnoreCase))
        {
            items.Add("Start the product, collect browser proof against that same host, and stop it in this execution using the matching startup receipt.");
        }

        if (items.Any(item => item.Contains("`browser_evaluate`", StringComparison.OrdinalIgnoreCase)))
        {
            items.Add("Run `browser_evaluate` after the representative interaction and before stopping the runtime.");
        }

        var distinctItems = items
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return distinctItems.Length == 0
            ? "1. Resolve the exact rejected gate in the runtime diagnostic.\n2. Verify its successful current-execution evidence before finalizing."
            : string.Join(
                Environment.NewLine,
                distinctItems.Select((item, index) => $"{index + 1}. {item}"));
    }

    private static bool LooksLikeToolName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return Regex.IsMatch(
            value,
            @"^[a-z][a-z0-9]*(?:_[a-z0-9]+)+$",
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    }

    private static string FormatSlotIds<T>(IReadOnlyList<T> slotIds)
        => slotIds.Count == 0
            ? "none"
            : string.Join(", ", slotIds);

    private static string FormatGroundedReferences(params string[] sources)
    {
        var references = sources
            .SelectMany(source => GroundedReferenceRegex.Matches(source).Select(match => TrimReference(match.Value)))
            .Where(reference => !string.IsNullOrWhiteSpace(reference))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(MaxReferenceCount)
            .ToArray();
        return references.Length == 0
            ? "- none retained; use the compact launch context and recovery packet"
            : string.Join(Environment.NewLine, references.Select(reference => $"- {reference}"));
    }

    private static string TrimReference(string value)
        => value.TrimEnd('.', ':', ')', ']', '}');

    private static string FormatLaunchVariables(IReadOnlyDictionary<string, string> launchVariables)
    {
        if (launchVariables.Count == 0)
        {
            return "- none";
        }

        var visibleLaunchVariables = launchVariables
            .Where(pair => ProcessAgentVisibleLaunchVariablePolicy.IsVisible(pair.Key))
            .ToArray();
        if (visibleLaunchVariables.Length == 0)
        {
            return "- none";
        }

        var builder = new StringBuilder();
        var omittedCount = 0;
        foreach (var pair in visibleLaunchVariables
                     .OrderByDescending(pair => HasGroundedReference(pair.Value))
                     .ThenBy(pair => pair.Value.Length > MaxLaunchVariableCharacters)
                     .ThenBy(pair => pair.Key, StringComparer.Ordinal))
        {
            var value = CollapseWhitespace(pair.Value);
            var clippedValue = Clip(value, MaxLaunchVariableCharacters);
            var line = $"- {pair.Key}: {clippedValue}";
            if (builder.Length + line.Length + Environment.NewLine.Length > MaxLaunchVariableBlockCharacters)
            {
                omittedCount++;
                continue;
            }

            builder.AppendLine(line);
        }

        if (omittedCount > 0)
        {
            builder.Append($"- [... {omittedCount} lower-priority launch variable(s) omitted from automatic recovery context ...]");
        }

        return builder.ToString().TrimEnd();
    }

    private static bool HasGroundedReference(string value)
        => value.Contains("artifacts/", StringComparison.OrdinalIgnoreCase) ||
           value.Contains("external-target/", StringComparison.OrdinalIgnoreCase);

    private static string FormatContractExcerpts(string prompt)
    {
        var excerpts = ContractSectionHeadings
            .Select(heading => ExtractSection(prompt, heading))
            .Where(excerpt => !string.IsNullOrWhiteSpace(excerpt))
            .ToArray();
        if (excerpts.Length > 0)
        {
            return string.Join(Environment.NewLine + Environment.NewLine, excerpts);
        }

        return Clip(prompt.Trim(), MaxFallbackPromptCharacters);
    }

    private static string ExtractSection(string prompt, string heading)
    {
        var start = prompt.IndexOf(heading, StringComparison.OrdinalIgnoreCase);
        if (start < 0)
        {
            return string.Empty;
        }

        var contentStart = start + heading.Length;
        var end = ContractSectionHeadings
            .Where(candidate => !string.Equals(candidate, heading, StringComparison.OrdinalIgnoreCase))
            .Select(candidate => prompt.IndexOf(candidate, contentStart, StringComparison.OrdinalIgnoreCase))
            .Where(index => index >= 0)
            .DefaultIfEmpty(prompt.Length)
            .Min();
        var content = prompt[contentStart..end].Trim();
        return $"{heading}{Environment.NewLine}{Clip(content, MaxSectionCharacters)}";
    }

    private static string CollapseWhitespace(string value)
        => Regex.Replace(value.Trim(), @"\s+", " ", RegexOptions.CultureInvariant);

    private static string Clip(string value, int maximumCharacters)
    {
        if (value.Length <= maximumCharacters)
        {
            return value;
        }

        const string marker = " [... recovery context clipped ...]";
        return value[..(maximumCharacters - marker.Length)] + marker;
    }
}
