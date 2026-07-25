using System.Text;
using System.Text.RegularExpressions;
using CanDoItAll.Processes.Contracts;
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
    private const string SubprocessLaunchToolName = "project_structure_process_subprocess_launch";

    private static readonly string[] ContractSectionHeadings =
    [
        "Step instructions:",
        "Input contract:",
        "Output contract:",
        "Evidence contract:",
        "Required upstream artifact slots:",
        "Produced artifact slots:",
        "Available branch outcomes:",
        "Subprocess mapping:"
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

        var isRuntimeOwnedSubprocess = IsRuntimeOwnedSubprocess(assignment.LaunchVariables);
        var effectiveRecoveryInstruction = FormatRecoveryInstruction(
            runtimeRecoveryInstruction,
            isRuntimeOwnedSubprocess);
        var requiredSlots = FormatSlotIds(assignment.RequiredArtifactSlotIds);
        var producedSlots = FormatSlotIds(assignment.ProducedArtifactSlotIds);
        var branchGate = assignment.BranchGate is null
            ? "none"
            : $"{assignment.BranchGate.SourceStepKey} -> {assignment.BranchGate.RequiredOutcomeKey}";
        var groundedRefs = FormatGroundedReferences(assignment.Prompt, effectiveRecoveryInstruction);
        var launchVariables = FormatLaunchVariables(assignment.LaunchVariables);
        var contractExcerpts = FormatContractExcerpts(assignment.Prompt, isRuntimeOwnedSubprocess);
        var recoveryChecklist = FormatRecoveryChecklist(effectiveRecoveryInstruction, isRuntimeOwnedSubprocess);
        var launchOwnershipSection = FormatLaunchOwnershipSection(isRuntimeOwnedSubprocess);

        return $"""
        {effectiveRecoveryInstruction}

        {ExecutionFocusHeading}
        This is a focused recovery attempt for the same process step, not a restart of its complete discovery and planning work. Resolve the rejected completion gate first. Reuse the existing product state and the exact grounded refs below. Read only the minimum files needed for the repair, perform the required action, verify its current-execution receipt, and only then rewrite the managed artifact and finalize.
        Do not claim an action, correction, rerun, mutation, validation, or artifact write unless this exact execution attempt has the corresponding successful tool receipt.

        {launchOwnershipSection}

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

    private static string FormatRecoveryChecklist(
        string runtimeRecoveryInstruction,
        bool isRuntimeOwnedSubprocess)
    {
        var items = MissingReceiptListRegex.Matches(runtimeRecoveryInstruction)
            .SelectMany(match => match.Groups["receipts"].Value.Split(
                [';', ','],
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Where(LooksLikeToolName)
            .Where(toolName =>
                !isRuntimeOwnedSubprocess ||
                !string.Equals(toolName, SubprocessLaunchToolName, StringComparison.OrdinalIgnoreCase))
            .Select(toolName => $"Invoke `{toolName}` successfully in this exact execution attempt.")
            .ToList();

        if (runtimeRecoveryInstruction.Contains(
                ProcessCompletionDiagnosticCodes.ProductSourceInspectionEvidenceMissing,
                StringComparison.OrdinalIgnoreCase))
        {
            items.Add("Read the concrete product material responsible for the diagnosed behavior in this execution; do not rely only on ancillary or presentation material.");
        }

        if (runtimeRecoveryInstruction.Contains(
                "process.adapter.runtime_lifecycle_correlation_missing",
                StringComparison.OrdinalIgnoreCase))
        {
            items.Add("Repeat the complete lifecycle required by this step in causal order, and cite only successful receipts created by this execution.");
        }

        if (runtimeRecoveryInstruction.Contains(
                ProcessCompletionDiagnosticCodes.ArtifactPayloadSchemaInvalid,
                StringComparison.OrdinalIgnoreCase))
        {
            items.Add("Reread the schema-bound Produced artifact slot and rewrite its declared payload exactly before finalizing.");
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

    private static bool IsRuntimeOwnedSubprocess(IReadOnlyDictionary<string, string> launchVariables)
        => ProcessRuntimeLaunchVariables.TryReadProcessStepSubprocessContract(
               launchVariables,
               out var contract) &&
           contract.LaunchMode == ProcessSubprocessLaunchMode.RuntimeOwned;

    private static string FormatLaunchOwnershipSection(bool isRuntimeOwnedSubprocess)
        => isRuntimeOwnedSubprocess
            ? $"Subprocess launch ownership:\n- process runtime owned\n- The process runtime launches, defers, and completes the parent step from typed child evidence. Do not call {SubprocessLaunchToolName}."
            : string.Empty;

    private static string FormatRecoveryInstruction(
        string runtimeRecoveryInstruction,
        bool isRuntimeOwnedSubprocess)
    {
        if (!isRuntimeOwnedSubprocess)
        {
            return runtimeRecoveryInstruction.Trim();
        }

        var retainedLines = new List<string>();
        foreach (var line in runtimeRecoveryInstruction.Split(["\r\n", "\n"], StringSplitOptions.None))
        {
            if (!line.Contains(SubprocessLaunchToolName, StringComparison.OrdinalIgnoreCase) ||
                line.Contains("do not call", StringComparison.OrdinalIgnoreCase))
            {
                retainedLines.Add(line);
                continue;
            }

            if (TryRemoveRuntimeOwnedLaunchReceipt(line, out var sanitizedLine))
            {
                retainedLines.Add(sanitizedLine);
            }
        }

        var retainedInstruction = string.Join(Environment.NewLine, retainedLines).Trim();
        if (string.IsNullOrWhiteSpace(retainedInstruction) ||
            string.Equals(
                retainedInstruction,
                "Runtime diagnostic rework instruction:",
                StringComparison.OrdinalIgnoreCase))
        {
            retainedInstruction = "Runtime diagnostic rework instruction:\nApply the typed runtime-owned subprocess contract.";
        }

        return retainedInstruction;
    }

    private static bool TryRemoveRuntimeOwnedLaunchReceipt(
        string line,
        out string sanitizedLine)
    {
        sanitizedLine = string.Empty;
        var match = MissingReceiptListRegex.Match(line);
        if (!match.Success)
        {
            return false;
        }

        var receiptGroup = match.Groups["receipts"];
        var retainedReceipts = receiptGroup.Value
            .Split([';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(receipt => !string.Equals(
                receipt.Trim('`'),
                SubprocessLaunchToolName,
                StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (retainedReceipts.Length == 0)
        {
            return false;
        }

        sanitizedLine = string.Concat(
            line.AsSpan(0, receiptGroup.Index),
            string.Join("; ", retainedReceipts),
            line.AsSpan(receiptGroup.Index + receiptGroup.Length));
        return true;
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

    private static string FormatContractExcerpts(
        string prompt,
        bool isRuntimeOwnedSubprocess)
    {
        var excerpts = ContractSectionHeadings
            .Select(heading => ExtractSection(prompt, heading))
            .Where(excerpt => !string.IsNullOrWhiteSpace(excerpt))
            .Select(excerpt => isRuntimeOwnedSubprocess
                ? RemoveAgentOwnedLaunchInstructions(excerpt)
                : excerpt)
            .Where(excerpt => !string.IsNullOrWhiteSpace(excerpt))
            .ToArray();
        if (excerpts.Length > 0)
        {
            return string.Join(Environment.NewLine + Environment.NewLine, excerpts);
        }

        var fallback = Clip(prompt.Trim(), MaxFallbackPromptCharacters);
        return isRuntimeOwnedSubprocess
            ? RemoveAgentOwnedLaunchInstructions(fallback)
            : fallback;
    }

    private static string RemoveAgentOwnedLaunchInstructions(string value)
    {
        return string.Join(
                Environment.NewLine,
                value
                    .Split(["\r\n", "\n"], StringSplitOptions.None)
                    .Where(line =>
                        !line.Contains(SubprocessLaunchToolName, StringComparison.OrdinalIgnoreCase) ||
                        line.Contains("do not call", StringComparison.OrdinalIgnoreCase) ||
                        line.Contains("process runtime owned", StringComparison.OrdinalIgnoreCase)))
            .Trim();
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
