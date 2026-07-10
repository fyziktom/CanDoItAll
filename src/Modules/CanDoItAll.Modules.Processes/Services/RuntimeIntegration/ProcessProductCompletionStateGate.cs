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

using static CanDoItAll.Modules.Processes.ProcessAgentRightsDiagnosticPolicy;
using static CanDoItAll.Modules.Processes.ProcessCompletionRuleParser;
using static CanDoItAll.Modules.Processes.ProcessProductCompletionRuleParser;
using static CanDoItAll.Modules.Processes.ProcessProductRootResolver;
using static CanDoItAll.Modules.Processes.ProcessCompletionText;
using static CanDoItAll.Modules.Processes.ProcessExecutionResultFactory;
using static CanDoItAll.Modules.Processes.ProcessProductCompletionPathGate;

namespace CanDoItAll.Modules.Processes;

internal static class ProcessProductCompletionStateGate
{
    internal static ProcessCompletionIssue? ValidateRequiredProductStateCompletion(
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

    internal static ProcessCompletionIssue? ValidateCompletedOutcomeDoesNotDeclareBlockers(
        ProcessRuntimeStepAssignment assignment,
        ProcessStepOutcomeResult output,
        ProcessToolReceiptPolicyCatalog toolReceiptPolicies)
    {
        if (output.Status != ProcessStepOutcomeStatus.Completed)
        {
            return null;
        }

        if (toolReceiptPolicies.AllowsCompletedOutcomeWithDeclaredBlockers(assignment))
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

    internal static IEnumerable<string> SplitOutcomeLines(string value)
        => value.Split(
                ['\r', '\n'],
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(line => !string.IsNullOrWhiteSpace(line));

    internal static bool DeclaresUnresolvedBlocker(string line)
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
            MissingRequiredReceiptRegex().IsMatch(line) ||
            DeclaresDeferredValidationProof(line);
    }

    internal static bool DeclaresDeferredValidationProof(string line)
    {
        if (!ContainsAny(line, "validation", "build", "test", "restore", "receipt", "proof", "evidence"))
        {
            return false;
        }

        return ContainsAny(
                line,
                "will be added",
                "will be captured",
                "will be recorded",
                "will be executed",
                "will run",
                "to be added",
                "to be captured",
                "to be recorded",
                "not yet captured",
                "not yet recorded",
                "not yet executed",
                "still planned",
                "planned rather than recorded") ||
            ContainsAny(line, "no current-run", "no current run", "missing current-run", "missing current run") &&
            ContainsAny(line, "receipt", "proof", "evidence", "command");
    }

    internal static bool ContainsNegatedBlockerPhrase(string line)
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
