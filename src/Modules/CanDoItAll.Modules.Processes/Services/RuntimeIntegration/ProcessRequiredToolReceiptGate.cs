using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Processes.Contracts;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Modules.Processes;

internal static class ProcessRequiredToolReceiptGate
{
    public static ProcessRequiredToolReceiptGateResult Evaluate(
        ProcessRuntimeStepAssignment assignment,
        IReadOnlyList<ToolExecutionReceiptRecord>? toolReceipts,
        IReadOnlySet<string>? activeLaunchContextToolNames = null,
        Guid? currentExecutionRunId = null,
        string? branchOutcomeKey = null,
        IReadOnlySet<string>? productCoveredToolNames = null)
    {
        ArgumentNullException.ThrowIfNull(assignment);

        var requiredReceipts = ProcessCapabilityScope.Normalize(assignment.CapabilityScope)
            .RequiredReceipts
            .Where(receipt => ProcessRequiredRuntimeToolNames.IsActive(receipt, activeLaunchContextToolNames))
            .Where(receipt => ProcessRequiredRuntimeToolNames.IsApplicableToBranchOutcome(receipt, branchOutcomeKey))
            .Where(receipt => !IsCoveredByProductReceiptRule(receipt, productCoveredToolNames))
            .ToArray();
        if (requiredReceipts.Length == 0)
        {
            return ProcessRequiredToolReceiptGateResult.Empty;
        }

        var observedReceipts = toolReceipts ?? [];
        var missingReceipts = requiredReceipts
            .Where(requiredReceipt =>
                CountMatchingReceipts(observedReceipts, requiredReceipt, currentExecutionRunId) < requiredReceipt.MinimumCount)
            .ToArray();
        return new ProcessRequiredToolReceiptGateResult(requiredReceipts, missingReceipts);
    }

    public static string FormatMissingSummary(IReadOnlyList<ProcessRequiredToolReceipt> missingReceipts)
    {
        return ProcessPublicReceiptTextPolicy.NormalizePublicMessage(
            string.Join("; ", missingReceipts.Select(FormatRequirement)),
            "Required tool evidence is missing.");
    }

    public static IReadOnlyList<string> ResolveRequiredRuntimeToolNames(ProcessCapabilityScope? capabilityScope)
        => ProcessRequiredRuntimeToolNames.FromUnconditionalCapabilityScope(capabilityScope);

    private static int CountMatchingReceipts(
        IReadOnlyList<ToolExecutionReceiptRecord> observedReceipts,
        ProcessRequiredToolReceipt requiredReceipt,
        Guid? currentExecutionRunId)
    {
        return observedReceipts.Count(receipt =>
            MatchesSelector(receipt, requiredReceipt) &&
            IsUsableReceipt(receipt, requiredReceipt, currentExecutionRunId));
    }

    private static bool IsCoveredByProductReceiptRule(
        ProcessRequiredToolReceipt requiredReceipt,
        IReadOnlySet<string>? productCoveredToolNames)
    {
        if (productCoveredToolNames is null || productCoveredToolNames.Count == 0)
        {
            return false;
        }

        var toolName = requiredReceipt.Kind switch
        {
            ProcessRequiredToolReceiptKind.RuntimeToolName => requiredReceipt.ToolName,
            ProcessRequiredToolReceiptKind.RuntimeToolNameWithProvider => requiredReceipt.ToolName,
            ProcessRequiredToolReceiptKind.McpToolName => requiredReceipt.ToolName,
            _ => string.Empty
        };
        return !string.IsNullOrWhiteSpace(toolName) &&
               productCoveredToolNames.Contains(toolName.Trim());
    }

    private static bool MatchesSelector(
        ToolExecutionReceiptRecord receipt,
        ProcessRequiredToolReceipt requiredReceipt)
    {
        return requiredReceipt.Kind switch
        {
            ProcessRequiredToolReceiptKind.RuntimeToolName => ToolNameMatches(receipt, requiredReceipt.ToolName),
            ProcessRequiredToolReceiptKind.RuntimeToolProviderKey => RuntimeToolProviderMatches(receipt, requiredReceipt.RuntimeToolProviderKey),
            ProcessRequiredToolReceiptKind.RuntimeToolNameWithProvider => ToolNameMatches(receipt, requiredReceipt.ToolName) &&
                                                                          RuntimeToolProviderMatches(receipt, requiredReceipt.RuntimeToolProviderKey),
            ProcessRequiredToolReceiptKind.McpToolName => ToolNameMatches(receipt, requiredReceipt.ToolName) &&
                                                          McpServerMatches(receipt, requiredReceipt.McpServerKey),
            _ => false
        };
    }

    private static bool IsUsableReceipt(
        ToolExecutionReceiptRecord receipt,
        ProcessRequiredToolReceipt requiredReceipt,
        Guid? currentExecutionRunId)
    {
        if (requiredReceipt.RequireCurrentRun &&
            (currentExecutionRunId is null || receipt.ExecutionRunId != currentExecutionRunId.Value))
        {
            return false;
        }

        return !requiredReceipt.RequireSuccessfulExit || IsSuccessfulReceipt(receipt.ExitSummary);
    }

    private static bool ToolNameMatches(ToolExecutionReceiptRecord receipt, string toolName)
        => !string.IsNullOrWhiteSpace(toolName) &&
           string.Equals(receipt.ToolName, toolName.Trim(), StringComparison.OrdinalIgnoreCase);

    private static bool RuntimeToolProviderMatches(ToolExecutionReceiptRecord receipt, string runtimeToolProviderKey)
        => !string.IsNullOrWhiteSpace(runtimeToolProviderKey) &&
           string.Equals(receipt.RuntimeToolProviderKey, runtimeToolProviderKey.Trim(), StringComparison.OrdinalIgnoreCase);

    private static bool McpServerMatches(ToolExecutionReceiptRecord receipt, string mcpServerKey)
    {
        if (string.IsNullOrWhiteSpace(mcpServerKey))
        {
            return true;
        }

        var normalized = mcpServerKey.Trim();
        return receipt.RuntimeToolProviderKey.Contains(normalized, StringComparison.OrdinalIgnoreCase) ||
               receipt.RequestSummary.Contains(normalized, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSuccessfulReceipt(string exitSummary)
    {
        if (string.IsNullOrWhiteSpace(exitSummary))
        {
            return false;
        }

        return exitSummary.Contains("Succeeded", StringComparison.OrdinalIgnoreCase) ||
               exitSummary.Contains("exit 0", StringComparison.OrdinalIgnoreCase) ||
               exitSummary.Contains("ExitCode=0", StringComparison.OrdinalIgnoreCase);
    }

    private static string FormatRequirement(ProcessRequiredToolReceipt receipt)
    {
        var selector = receipt.Kind switch
        {
            ProcessRequiredToolReceiptKind.RuntimeToolName => receipt.ToolName,
            ProcessRequiredToolReceiptKind.RuntimeToolProviderKey => receipt.RuntimeToolProviderKey,
            ProcessRequiredToolReceiptKind.RuntimeToolNameWithProvider => $"{receipt.RuntimeToolProviderKey}/{receipt.ToolName}",
            ProcessRequiredToolReceiptKind.McpToolName => string.IsNullOrWhiteSpace(receipt.McpServerKey)
                ? receipt.ToolName
                : $"{receipt.McpServerKey}/{receipt.ToolName}",
            _ => receipt.Key
        };
        var minimum = receipt.MinimumCount <= 1 ? string.Empty : $" x{receipt.MinimumCount}";
        return string.IsNullOrWhiteSpace(receipt.Reason)
            ? $"{selector}{minimum}"
            : $"{selector}{minimum} ({receipt.Reason})";
    }
}

internal sealed record ProcessRequiredToolReceiptGateResult(
    IReadOnlyList<ProcessRequiredToolReceipt> RequiredReceipts,
    IReadOnlyList<ProcessRequiredToolReceipt> MissingReceipts)
{
    public static ProcessRequiredToolReceiptGateResult Empty { get; } = new([], []);

    public bool IsSatisfied => MissingReceipts.Count == 0;
}
