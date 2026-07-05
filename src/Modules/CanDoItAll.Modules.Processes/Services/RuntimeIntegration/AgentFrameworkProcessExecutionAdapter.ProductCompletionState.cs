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
    private static ProcessCompletionIssue? ValidateRequiredProductStateCompletion(
        ProcessRuntimeStepAssignment assignment,
        ProcessStepOutcomeResult output)
    {
        if (output.Status != ProcessStepOutcomeStatus.Completed ||
            !TryResolveInspectableProductRoot(assignment.LaunchVariables, out var productRoot))
        {
            return null;
        }

        if (ValidateRequiredProductPaths(assignment, productRoot) is { } requiredPathIssue)
        {
            return requiredPathIssue;
        }

        return ValidateRequiredProductFileContentChecks(assignment, output, productRoot);
    }

    private static ProcessCompletionIssue? ValidateCompletedOutcomeDoesNotDeclareBlockers(
        ProcessRuntimeStepAssignment assignment,
        ProcessStepOutcomeResult output)
    {
        if (output.Status != ProcessStepOutcomeStatus.Completed)
        {
            return null;
        }

        var blockerLines = EnumerateOutcomeText(output)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .SelectMany(value => SplitOutcomeLines(value!))
            .Where(DeclaresUnresolvedBlocker)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(5)
            .ToArray();
        if (blockerLines.Length == 0)
        {
            return null;
        }

        var blockerSummary = string.Join(" | ", blockerLines);
        return new ProcessCompletionIssue(
            "process.adapter.completed_outcome_declares_unresolved_blocker",
            $"Step '{assignment.StepKey}' returned Completed while its outcome text still declares unresolved blocker or missing-acceptance state: {blockerSummary}. Return Blocked or repair the missing state before claiming completion.",
            $"{assignment.RunId}:{assignment.StepInstanceId}:completed-outcome-declares-blocker:{ComputeHash(blockerSummary)}",
            assignment.ProducedArtifactSlotIds,
            ProcessDiagnosticRetrySafety.SafeToRetry,
            ProcessDiagnosticIdempotencyClassification.Idempotent);
    }

    private static IEnumerable<string> SplitOutcomeLines(string value)
        => value.Split(
                ['\r', '\n'],
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(line => !string.IsNullOrWhiteSpace(line));

    private static bool DeclaresUnresolvedBlocker(string line)
    {
        if (ContainsNegatedBlockerPhrase(line))
        {
            return false;
        }

        return ContainsAny(
                line,
                "remaining blocker",
                "unresolved blocker",
                "still blocked",
                "cannot be treated as accepted",
                "cannot be accepted",
                "not launcher-compatible",
                "not launcher compatible",
                "pending writeback receipt") ||
            MissingRequiredReceiptRegex().IsMatch(line);
    }

    private static bool ContainsNegatedBlockerPhrase(string line)
        => ContainsAny(
            line,
            "no remaining blocker",
            "no remaining blockers",
            "no unresolved blocker",
            "no unresolved blockers",
            "without remaining blocker",
            "without unresolved blocker",
            "no missing receipt",
            "no missing receipts",
            "no required receipt missing",
            "no required receipts missing",
            "without missing receipt",
            "blockers: none");


}
