using CanDoItAll.AgentFramework.Models;
using System.Text.RegularExpressions;

namespace CanDoItAll.Modules.Processes;

internal delegate bool TryResolveBrowserOutputPath(
    string browserWorkingDirectory,
    string relativeOutputPath,
    out string fullPath);

internal static class ProcessProviderNativeBrowserOutputFacts
{
    internal static bool HasProviderNativeBrowserOutputForDeclaredPath(
        ProcessAutomationExecutionRunDetail detail,
        string declaredRelativePath,
        Func<ProcessAutomationExecutionRunDetail, IReadOnlyDictionary<string, IReadOnlyList<string>>> resolveSuccessfulBrowserToolOutputFiles,
        TryResolveBrowserOutputPath tryResolveSafeBrowserOutputPath)
    {
        ArgumentNullException.ThrowIfNull(detail);
        ArgumentNullException.ThrowIfNull(resolveSuccessfulBrowserToolOutputFiles);
        ArgumentNullException.ThrowIfNull(tryResolveSafeBrowserOutputPath);

        var expectedToolName = ProcessArtifactProviderNativeVisualValidationRules.ResolveProviderNativeBrowserToolName(declaredRelativePath);
        if (string.IsNullOrWhiteSpace(expectedToolName))
        {
            return false;
        }

        var browserOutputsByToolName = resolveSuccessfulBrowserToolOutputFiles(detail);
        if (!browserOutputsByToolName.TryGetValue(expectedToolName, out var outputFiles))
        {
            return false;
        }

        var matchingOutputFiles = outputFiles
            .Where(outputFile => ProcessArtifactProviderNativeVisualValidationRules.MatchesExpectedBrowserOutputFile(
                declaredRelativePath,
                outputFile))
            .ToList();
        if (matchingOutputFiles.Count == 0)
        {
            return false;
        }

        var browserWorkingDirectory = ResolveProviderNativeBrowserWorkingDirectory(detail);
        if (string.IsNullOrWhiteSpace(browserWorkingDirectory))
        {
            return true;
        }

        return matchingOutputFiles.Any(outputFile =>
            tryResolveSafeBrowserOutputPath(browserWorkingDirectory, outputFile, out var fullPath) &&
            File.Exists(fullPath) &&
            new FileInfo(fullPath).Length > 0);
    }

    internal static bool TryResolveRequestedManagedPath(
        string requestSummary,
        Regex managedWorkspacePathRegex,
        out string path)
    {
        ArgumentNullException.ThrowIfNull(managedWorkspacePathRegex);

        path = string.Empty;
        if (string.IsNullOrWhiteSpace(requestSummary))
        {
            return false;
        }

        var match = managedWorkspacePathRegex.Match(requestSummary);
        if (!match.Success)
        {
            return false;
        }

        path = WorkspaceScopeDescriptor.NormalizeRelativePath(match.Groups["path"].Value);
        return !string.IsNullOrWhiteSpace(path);
    }

    internal static string? ResolveProviderNativeBrowserWorkingDirectory(ProcessAutomationExecutionRunDetail detail)
    {
        ArgumentNullException.ThrowIfNull(detail);

        return detail.ToolReceipts
            .Where(receipt =>
                string.Equals(ProcessToolReceiptFacts.NormalizeToolToken(receipt.ToolName), "local_mcp_launch", StringComparison.Ordinal) &&
                receipt.RequestSummary.Contains("@playwright/mcp", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(receipt.WorkingDirectory) &&
                !ProcessToolReceiptFacts.IsFailedReceipt(receipt))
            .OrderByDescending(receipt => receipt.CompletedAtUtc)
            .ThenByDescending(receipt => receipt.StartedAtUtc)
            .Select(receipt => receipt.WorkingDirectory.Trim())
            .FirstOrDefault();
    }
}
