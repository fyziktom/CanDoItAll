using System.Globalization;
using System.Diagnostics.CodeAnalysis;
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
using CanDoItAll.Processes.Contracts;
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

using static CanDoItAll.Modules.Processes.ProcessCompletionText;
using static CanDoItAll.Modules.Processes.ProcessManagedArtifactEvidence;
using static CanDoItAll.Modules.Processes.ProcessRequiredReceiptMatcher;
using static CanDoItAll.Modules.Processes.ProcessRuntimeLifecycleReceiptFacts;

namespace CanDoItAll.Modules.Processes;

internal static class ProcessRuntimeLifecycleReceiptGate
{
    internal static ProcessCompletionIssue? ValidateRuntimeLifecycleReceipts(
        ProcessRuntimeStepAssignment assignment,
        ProcessStepOutcomeResult output,
        IReadOnlyList<ToolExecutionReceiptRecord>? toolReceipts,
        Guid? currentExecutionRunId,
        IReadOnlyList<ProductCompletionRequiredToolReceiptRule> requiredToolReceiptRules)
    {
        if (output.Status != ProcessStepOutcomeStatus.Completed)
        {
            return null;
        }

        var requiredToolNames = ResolveProductCoveredRuntimeToolNames(requiredToolReceiptRules);
        if (!TryResolveRuntimeLifecycleToolNames(requiredToolNames, out var lifecycleToolNames))
        {
            return null;
        }

        var observedToolReceipts = toolReceipts ?? [];
        var currentReceipts = currentExecutionRunId is null
            ? observedToolReceipts
            : observedToolReceipts
                .Where(receipt => receipt.ExecutionRunId == currentExecutionRunId.Value)
                .ToArray();
        var runReceipt = FindSuccessfulReceipt(currentReceipts, lifecycleToolNames.RunToolName);
        var stopReceipt = FindSuccessfulReceipt(currentReceipts, lifecycleToolNames.StopToolName);
        var browserReceipts = currentReceipts
            .Where(receipt => receipt.ToolName.StartsWith("browser_", StringComparison.OrdinalIgnoreCase) &&
                              IsSuccessfulReceipt(receipt.ExitSummary))
            .ToArray();

        if (runReceipt is null ||
            stopReceipt is null ||
            browserReceipts.Length == 0)
        {
            return CreateRuntimeLifecycleIssue(
                assignment,
                output,
                $"Runtime/browser proof was not produced by the current execution-run host lifecycle. Retry QA by starting the product with {lifecycleToolNames.RunToolName}, collecting browser proof against that host, and stopping it with {lifecycleToolNames.StopToolName} in the same execution.",
                observedToolReceipts);
        }

        var runFacts = RuntimeLifecycleReceiptFacts.From(runReceipt);
        var stopFacts = RuntimeLifecycleReceiptFacts.From(stopReceipt);
        var browserFacts = browserReceipts
            .Select(RuntimeLifecycleReceiptFacts.From)
            .ToArray();
        if (runFacts.StartupReceiptPaths.Count == 0 ||
            stopFacts.StartupReceiptPaths.Count == 0 ||
            !runFacts.StartupReceiptPaths.Intersect(stopFacts.StartupReceiptPaths, StringComparer.OrdinalIgnoreCase).Any())
        {
            return CreateRuntimeLifecycleIssue(
                assignment,
                output,
                $"{lifecycleToolNames.RunToolName} and {lifecycleToolNames.StopToolName} receipts are not correlated by the same startup.json receipt. Retry QA using the startup.json path returned by the current {lifecycleToolNames.RunToolName} call when invoking {lifecycleToolNames.StopToolName}.",
                observedToolReceipts);
        }

        var runAuthorities = runFacts.LoopbackAuthorities;
        var browserAuthorities = browserFacts
            .SelectMany(facts => facts.LoopbackAuthorities)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (runAuthorities.Count == 0 ||
            browserAuthorities.Length == 0 ||
            !runAuthorities.Intersect(browserAuthorities, StringComparer.OrdinalIgnoreCase).Any())
        {
            return CreateRuntimeLifecycleIssue(
                assignment,
                output,
                $"Browser proof is not correlated to the host URL reported by {lifecycleToolNames.RunToolName}. Retry QA by navigating the browser to the current run's loopback host URL before collecting screenshots, snapshots, and console proof.",
                observedToolReceipts);
        }

        return null;
    }

    internal static bool TryResolveRuntimeLifecycleToolNames(
        IReadOnlySet<string> requiredToolNames,
        [NotNullWhen(true)] out RuntimeLifecycleToolNames? lifecycleToolNames)
    {
        var runToolName = requiredToolNames
            .Where(IsWorkspaceRunToolName)
            .OrderBy(toolName => toolName, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
        var stopToolName = requiredToolNames
            .Where(IsWorkspaceStopToolName)
            .OrderBy(toolName => toolName, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
        if (string.IsNullOrWhiteSpace(runToolName) ||
            string.IsNullOrWhiteSpace(stopToolName) ||
            !requiredToolNames.Any(IsBrowserToolName))
        {
            lifecycleToolNames = null;
            return false;
        }

        lifecycleToolNames = new RuntimeLifecycleToolNames(runToolName, stopToolName);
        return true;
    }

    internal static bool IsWorkspaceRunToolName(string toolName)
        => toolName.StartsWith("workspace_", StringComparison.OrdinalIgnoreCase) &&
           toolName.EndsWith("_run", StringComparison.OrdinalIgnoreCase);

    internal static bool IsWorkspaceStopToolName(string toolName)
        => toolName.StartsWith("workspace_", StringComparison.OrdinalIgnoreCase) &&
           toolName.EndsWith("_stop", StringComparison.OrdinalIgnoreCase);

    internal static bool IsBrowserToolName(string toolName)
        => toolName.StartsWith("browser_", StringComparison.OrdinalIgnoreCase);

    internal static ToolExecutionReceiptRecord? FindSuccessfulReceipt(
        IReadOnlyList<ToolExecutionReceiptRecord> receipts,
        string toolName)
        => receipts
            .Where(receipt => string.Equals(receipt.ToolName, toolName, StringComparison.OrdinalIgnoreCase) &&
                              IsSuccessfulReceipt(receipt.ExitSummary))
            .OrderByDescending(receipt => receipt.CompletedAtUtc)
            .FirstOrDefault();

    internal static ProcessCompletionIssue CreateRuntimeLifecycleIssue(
        ProcessRuntimeStepAssignment assignment,
        ProcessStepOutcomeResult output,
        string summary,
        IReadOnlyList<ToolExecutionReceiptRecord> observedToolReceipts)
        => new(
            "process.adapter.runtime_lifecycle_correlation_missing",
            $"Step '{assignment.StepKey}' claimed completion for branch '{output.BranchOutcomeKey}', but runtime lifecycle proof is incomplete or stale. {summary}",
            $"{assignment.RunId}:{assignment.StepInstanceId}:runtime-lifecycle-correlation-missing:{output.BranchOutcomeKey}:{string.Join("|", observedToolReceipts.Select(SummarizeRuntimeLifecycleReceipt))}",
            assignment.ProducedArtifactSlotIds,
            ProcessDiagnosticRetrySafety.SafeToRetry,
            ProcessDiagnosticIdempotencyClassification.Idempotent);

    internal static string SummarizeRuntimeLifecycleReceipt(ToolExecutionReceiptRecord receipt)
        => $"{receipt.ToolName}:{receipt.ExecutionRunId:N}:{MaskNativePaths(receipt.RequestSummary)}:{SummarizeReceiptExit(receipt.ExitSummary)}";

    internal static string MaskNativePaths(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return Regex.Replace(
            value,
            @"[A-Za-z]:\\[^\s;|]+",
            "[native-path]",
            RegexOptions.CultureInvariant);
    }
}
