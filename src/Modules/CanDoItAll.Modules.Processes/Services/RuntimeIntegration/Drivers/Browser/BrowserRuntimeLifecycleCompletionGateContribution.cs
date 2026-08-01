using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Processes.Drivers.Abstractions;
using CanDoItAll.Processes.Runtime;

using static CanDoItAll.Modules.Processes.BrowserRuntimeLifecycleReceiptFacts;
using static CanDoItAll.Modules.Processes.ProcessManagedArtifactEvidence;
using static CanDoItAll.Modules.Processes.ProcessRequiredReceiptMatcher;

namespace CanDoItAll.Modules.Processes;

internal sealed class BrowserRuntimeLifecycleCompletionGateContribution : IProcessCompletionGateContribution
{
    public string ContributionKey => "browser.runtime-lifecycle";

    public int Order => 150;

    public ProcessCompletionGateContributionStage Stage => ProcessCompletionGateContributionStage.BeforeToolReceiptEvidence;

    public ProcessCompletionIssue? Validate(ProcessCompletionGateContext context)
    {
        if (context.Output.Status != ProcessStepOutcomeStatus.Completed)
        {
            return null;
        }

        var requiredToolReceiptRules = ResolveApplicableProductCompletionRequiredToolReceiptRules(
            context.Assignment,
            context.Output.BranchOutcomeKey);
        var requiredToolNames = ResolveProductCoveredRuntimeToolNames(requiredToolReceiptRules);
        if (!TryResolveBrowserRuntimeLifecycleToolNames(requiredToolNames, out var lifecycleToolNames))
        {
            return null;
        }

        var observedToolReceipts = context.ToolReceipts ?? [];
        var currentReceipts = context.CurrentExecutionRunId is null
            ? observedToolReceipts
            : observedToolReceipts
                .Where(receipt => receipt.ExecutionRunId == context.CurrentExecutionRunId.Value)
                .ToArray();
        var runReceipt = FindSuccessfulReceipt(currentReceipts, lifecycleToolNames.RunToolName);
        var stopReceipt = FindSuccessfulReceipt(currentReceipts, lifecycleToolNames.StopToolName);
        var browserReceipts = currentReceipts
            .Where(receipt => IsBrowserToolName(receipt.ToolName) && IsSuccessfulReceipt(receipt.ExitSummary))
            .ToArray();

        if (runReceipt is null ||
            stopReceipt is null ||
            browserReceipts.Length == 0)
        {
            return CreateIssue(
                context,
                $"Runtime/browser proof was not produced by the current execution-run host lifecycle. Retry QA by starting the product with {lifecycleToolNames.RunToolName}, collecting browser proof against that host, and stopping it with {lifecycleToolNames.StopToolName} in the same execution.",
                observedToolReceipts);
        }

        var runFacts = BrowserRuntimeLifecycleReceipt.From(runReceipt);
        var stopFacts = BrowserRuntimeLifecycleReceipt.From(stopReceipt);
        var browserFacts = browserReceipts
            .Select(BrowserRuntimeLifecycleReceipt.From)
            .ToArray();
        if (runFacts.StartupReceiptPaths.Count == 0 ||
            stopFacts.StartupReceiptPaths.Count == 0 ||
            !runFacts.StartupReceiptPaths.Intersect(stopFacts.StartupReceiptPaths, StringComparer.OrdinalIgnoreCase).Any())
        {
            return CreateIssue(
                context,
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
            return CreateIssue(
                context,
                $"Browser proof is not correlated to the host URL reported by {lifecycleToolNames.RunToolName}. Retry QA by navigating the browser to the current run's loopback host URL before collecting screenshots, snapshots, and console proof.",
                observedToolReceipts);
        }

        return null;
    }

    internal static bool TryResolveBrowserRuntimeLifecycleToolNames(
        IReadOnlySet<string> requiredToolNames,
        [NotNullWhen(true)] out BrowserRuntimeLifecycleToolNames? lifecycleToolNames)
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

        lifecycleToolNames = new BrowserRuntimeLifecycleToolNames(runToolName, stopToolName);
        return true;
    }

    private static bool IsWorkspaceRunToolName(string toolName)
        => toolName.StartsWith("workspace_", StringComparison.OrdinalIgnoreCase) &&
           toolName.EndsWith("_run", StringComparison.OrdinalIgnoreCase);

    private static bool IsWorkspaceStopToolName(string toolName)
        => toolName.StartsWith("workspace_", StringComparison.OrdinalIgnoreCase) &&
           toolName.EndsWith("_stop", StringComparison.OrdinalIgnoreCase);

    private static bool IsBrowserToolName(string toolName)
        => toolName.StartsWith("browser_", StringComparison.OrdinalIgnoreCase);

    private static ToolExecutionReceiptRecord? FindSuccessfulReceipt(
        IReadOnlyList<ToolExecutionReceiptRecord> receipts,
        string toolName)
        => receipts
            .Where(receipt => string.Equals(receipt.ToolName, toolName, StringComparison.OrdinalIgnoreCase) &&
                              IsSuccessfulReceipt(receipt.ExitSummary))
            .OrderByDescending(receipt => receipt.CompletedAtUtc)
            .FirstOrDefault();

    private static ProcessCompletionIssue CreateIssue(
        ProcessCompletionGateContext context,
        string summary,
        IReadOnlyList<ToolExecutionReceiptRecord> observedToolReceipts)
        => new(
            "process.adapter.runtime_lifecycle_correlation_missing",
            $"Step '{context.Assignment.StepKey}' claimed completion for branch '{context.Output.BranchOutcomeKey}', but runtime lifecycle proof is incomplete or stale. {summary}",
            $"{context.Assignment.RunId}:{context.Assignment.StepInstanceId}:runtime-lifecycle-correlation-missing:{context.Output.BranchOutcomeKey}:{string.Join("|", observedToolReceipts.Select(SummarizeReceipt))}",
            context.Assignment.ProducedArtifactSlotIds,
            ProcessDiagnosticRetrySafety.SafeToRetry,
            ProcessDiagnosticIdempotencyClassification.Idempotent);

    private static string SummarizeReceipt(ToolExecutionReceiptRecord receipt)
        => $"{receipt.ToolName}:{receipt.ExecutionRunId:N}:{MaskNativePaths(receipt.RequestSummary)}:{SummarizeReceiptExit(receipt.ExitSummary)}";

    private static string MaskNativePaths(string value)
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
