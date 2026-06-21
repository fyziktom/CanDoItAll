using CanDoItAll.AgentFramework.Models;
using System.Text.RegularExpressions;

namespace CanDoItAll.Modules.Processes;

internal static class ProcessProviderNativeBrowserProbeFailureRules
{
    internal static bool ShouldIgnoreProviderNativeBrowserOutputFileProbeFailure(
        ProcessAutomationExecutionRunDetail detail,
        ProcessAutomationToolExecutionReceipt receipt,
        Regex managedWorkspacePathRegex,
        Func<ProcessAutomationExecutionRunDetail, IReadOnlyDictionary<string, IReadOnlyList<string>>> resolveSuccessfulBrowserToolOutputFiles,
        TryResolveBrowserOutputPath tryResolveSafeBrowserOutputPath)
    {
        ArgumentNullException.ThrowIfNull(detail);
        ArgumentNullException.ThrowIfNull(receipt);
        ArgumentNullException.ThrowIfNull(managedWorkspacePathRegex);
        ArgumentNullException.ThrowIfNull(resolveSuccessfulBrowserToolOutputFiles);
        ArgumentNullException.ThrowIfNull(tryResolveSafeBrowserOutputPath);

        var normalizedToolName = ProcessToolReceiptFacts.NormalizeToolToken(receipt.ToolName);
        if (normalizedToolName is not ("workspace_read_file" or "workspace_stat_path") ||
            !receipt.ExitSummary.StartsWith("Failed", StringComparison.OrdinalIgnoreCase) ||
            !receipt.ExitSummary.Contains("does not exist", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var browserWorkingDirectory = ProcessProviderNativeBrowserOutputFacts.ResolveProviderNativeBrowserWorkingDirectory(detail);
        if (string.IsNullOrWhiteSpace(browserWorkingDirectory) ||
            !ProcessProviderNativeBrowserOutputFacts.TryResolveRequestedManagedPath(
                receipt.RequestSummary,
                managedWorkspacePathRegex,
                out var requestedPath))
        {
            return false;
        }

        var browserOutputsByToolName = resolveSuccessfulBrowserToolOutputFiles(detail);
        foreach (var outputFileName in browserOutputsByToolName.Values.SelectMany(item => item))
        {
            if (ProcessArtifactProviderNativeVisualValidationRules.MatchesExpectedBrowserOutputFile(requestedPath, outputFileName) &&
                tryResolveSafeBrowserOutputPath(browserWorkingDirectory, outputFileName, out var fullPath) &&
                File.Exists(fullPath))
            {
                return true;
            }
        }

        return false;
    }
}
