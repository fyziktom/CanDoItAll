using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Processes.Drivers.Abstractions;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Processes;

internal static class WorkspaceProductSourceInspectionReceiptFacts
{
    internal static bool TryGetGroundedProductRootAlias(
        ProcessRuntimeStepAssignment assignment,
        out string productRootAlias)
    {
        ArgumentNullException.ThrowIfNull(assignment);

        return TryGetGroundedProductRootAlias(assignment.LaunchVariables, out productRootAlias);
    }

    internal static bool TryGetGroundedProductRootAlias(
        IReadOnlyDictionary<string, string> launchVariables,
        out string productRootAlias)
    {
        ArgumentNullException.ThrowIfNull(launchVariables);

        productRootAlias = launchVariables.GetValueOrDefault(
            ProcessRuntimeLaunchVariables.ProductRootAlias,
            string.Empty);
        productRootAlias = NormalizePath(productRootAlias);
        return ExternalTargetAliasCodec.IsAnyAlias(productRootAlias);
    }

    internal static bool IsSuccessfulProductSourceRead(
        ToolExecutionReceiptRecord receipt,
        string productRootAlias,
        IReadOnlyList<string>? excludedPathFragments = null)
    {
        if (!TryGetSuccessfulProductSourceReadPath(receipt, productRootAlias, out var requestSummary))
        {
            return false;
        }

        return excludedPathFragments?.Any(fragment =>
                   requestSummary.Contains(fragment, StringComparison.OrdinalIgnoreCase)) != true;
    }

    internal static IReadOnlyList<string> ResolveRejectedProductSourceReadPaths(
        IReadOnlyList<ToolExecutionReceiptRecord>? receipts,
        Guid executionRunId,
        string productRootAlias,
        IReadOnlyList<string> excludedPathFragments)
    {
        if (receipts is null || excludedPathFragments.Count == 0)
        {
            return [];
        }

        return receipts
            .Where(receipt => receipt.ExecutionRunId == executionRunId)
            .Select(receipt => TryGetSuccessfulProductSourceReadPath(receipt, productRootAlias, out var path)
                ? path
                : string.Empty)
            .Where(path => path.Length > 0 && excludedPathFragments.Any(fragment =>
                path.Contains(fragment, StringComparison.OrdinalIgnoreCase)))
            .Distinct(ExternalTargetAliasCodec.EqualityComparer)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
    }

    private static bool TryGetSuccessfulProductSourceReadPath(
        ToolExecutionReceiptRecord receipt,
        string productRootAlias,
        out string requestSummary)
    {
        requestSummary = string.Empty;
        if (!string.Equals(receipt.ToolName, ToolContractCatalog.WorkspaceReadFile, StringComparison.OrdinalIgnoreCase) ||
            !receipt.ExitSummary.StartsWith("Succeeded", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        requestSummary = NormalizePath(receipt.RequestSummary);
        return ExternalTargetAliasCodec.IsAliasWithinRoot(requestSummary, productRootAlias);
    }

    private static string NormalizePath(string value)
        => value.Trim().Replace('\\', '/').TrimEnd('/');
}
