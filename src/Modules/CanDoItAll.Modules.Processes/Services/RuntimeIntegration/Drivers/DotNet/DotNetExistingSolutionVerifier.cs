using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Runtime;

using static CanDoItAll.Modules.Processes.ProcessRuntimeOwnedToolReceiptFactory;

namespace CanDoItAll.Modules.Processes;

internal sealed class DotNetExistingSolutionVerifier
{
    public ValueTask<ProcessRuntimeOwnedStepExecutionResult> VerifyAsync(
        ProcessRuntimeStepAssignment assignment,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(assignment);
        cancellationToken.ThrowIfCancellationRequested();

        var executionRunId = Guid.NewGuid();
        if (!TryResolveInputs(assignment.LaunchVariables, out var inputs, out var issue))
        {
            return ValueTask.FromResult(Failed(assignment, executionRunId, [], issue));
        }

        var files = new WorkspaceFileService(inputs.ProductRoot);
        var receipts = new List<ToolExecutionReceiptRecord>();
        foreach (var path in inputs.RequiredFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var stat = files.StatPath(Path.GetRelativePath(inputs.ProductRoot, path));
            receipts.Add(From(executionRunId, stat));
            if (!stat.Succeeded || !stat.Exists || !string.Equals(stat.PathKind, "file", StringComparison.OrdinalIgnoreCase))
            {
                return ValueTask.FromResult(Failed(
                    assignment,
                    executionRunId,
                    receipts,
                    $"The declared existing .NET solution context is missing required file '{Path.GetFileName(path)}'."));
            }
        }

        var solutionRead = files.ReadTextFile(Path.GetRelativePath(inputs.ProductRoot, inputs.SolutionFile), maxCharacters: 200000);
        receipts.Add(From(executionRunId, solutionRead));
        if (!solutionRead.Succeeded)
        {
            return ValueTask.FromResult(Failed(
                assignment,
                executionRunId,
                receipts,
                "The declared existing .NET solution file could not be read."));
        }

        var normalizedSolution = NormalizePathText(solutionRead.Content);
        foreach (var projectFile in inputs.ImplementationProjectFiles)
        {
            var relativeProjectFile = NormalizePathText(Path.GetRelativePath(inputs.ProductRoot, projectFile));
            if (!normalizedSolution.Contains(relativeProjectFile, StringComparison.OrdinalIgnoreCase))
            {
                return ValueTask.FromResult(Failed(
                    assignment,
                    executionRunId,
                    receipts,
                    $"The declared existing .NET solution does not include required implementation project '{Path.GetFileName(projectFile)}'."));
            }
        }

        var output = new ProcessStepOutcomeResult
        {
            Status = ProcessStepOutcomeStatus.Completed,
            Reason = "The declared existing .NET solution context was verified without initialization or product mutation.",
            EvidenceRefs = [BuildManagedStepEvidenceRef(assignment)],
            NextActions = [],
            HumanReadableSummaryMarkdown = $"Read-only verification confirmed {inputs.ImplementationProjectFiles.Count} declared implementation project(s) and {inputs.TestProjectFiles.Count} declared test project(s)."
        };
        return ValueTask.FromResult(new ProcessRuntimeOwnedStepExecutionResult(
            true,
            output,
            receipts,
            executionRunId,
            "Runtime-owned existing .NET solution verification completed without product mutation.",
            $"{assignment.RunId}:{assignment.StepInstanceId}:runtime-owned-dotnet-existing-solution:verified"));
    }

    private static ProcessRuntimeOwnedStepExecutionResult Failed(
        ProcessRuntimeStepAssignment assignment,
        Guid executionRunId,
        IReadOnlyList<ToolExecutionReceiptRecord> receipts,
        string summary)
        => new(
            false,
            new ProcessStepOutcomeResult
            {
                Status = ProcessStepOutcomeStatus.Blocked,
                Reason = summary,
                EvidenceRefs = [BuildManagedStepEvidenceRef(assignment)],
                NextActions = [],
                HumanReadableSummaryMarkdown = summary
            },
            receipts,
            executionRunId,
            summary,
            $"{assignment.RunId}:{assignment.StepInstanceId}:runtime-owned-dotnet-existing-solution:failed:{summary}");

    internal static bool TryResolveInputs(
        IReadOnlyDictionary<string, string> launchVariables,
        out DotNetExistingSolutionVerificationInputs inputs,
        out string issue)
    {
        inputs = null!;
        if (!DotNetSolutionProvisioningModeReader.TryRead(launchVariables, out var provisioningMode, out issue) ||
            provisioningMode != DotNetSolutionProvisioningMode.VerifyExisting)
        {
            issue = string.IsNullOrWhiteSpace(issue)
                ? "Read-only .NET solution verification requires provisioningMode 'verify-existing'."
                : issue;
            return false;
        }

        var productRoot = ResolveVariable(launchVariables, "ProductRoot", ResolveVariable(launchVariables, "ExternalTargetRoot"));
        if (string.IsNullOrWhiteSpace(productRoot))
        {
            issue = "Read-only .NET solution verification requires ProductRoot or ExternalTargetRoot.";
            return false;
        }

        try
        {
            productRoot = Path.GetFullPath(productRoot);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            issue = "Read-only .NET solution verification received an invalid product root.";
            return false;
        }

        if (!TryResolveProductPath(launchVariables, "DotNetSolutionFile", productRoot, out var solutionFile, out issue) ||
            !TryReadProductPathList(launchVariables, "DotNetRequiredProjectFiles", productRoot, requireAtLeastOne: true, out var implementationProjectFiles, out issue) ||
            !TryReadProductPathList(launchVariables, "DotNetTestProjectFiles", productRoot, requireAtLeastOne: false, out var testProjectFiles, out issue))
        {
            return false;
        }

        inputs = new DotNetExistingSolutionVerificationInputs(
            productRoot,
            solutionFile,
            implementationProjectFiles,
            testProjectFiles,
            [solutionFile, .. implementationProjectFiles, .. testProjectFiles]);
        issue = string.Empty;
        return true;
    }

    private static bool TryReadProductPathList(
        IReadOnlyDictionary<string, string> launchVariables,
        string variableKey,
        string productRoot,
        bool requireAtLeastOne,
        out IReadOnlyList<string> paths,
        out string issue)
    {
        paths = [];
        var value = ResolveVariable(launchVariables, variableKey);
        if (string.IsNullOrWhiteSpace(value))
        {
            issue = $"Read-only .NET solution verification requires launch variable '{variableKey}'.";
            return false;
        }

        try
        {
            var declared = JsonSerializer.Deserialize<string[]>(value) ?? [];
            var resolved = new List<string>();
            foreach (var candidate in declared)
            {
                if (!TryResolveProductPath(candidate, variableKey, productRoot, out var path, out issue))
                {
                    return false;
                }

                if (!resolved.Contains(path, StringComparer.OrdinalIgnoreCase))
                {
                    resolved.Add(path);
                }
            }

            paths = resolved;
            if (requireAtLeastOne && paths.Count == 0)
            {
                issue = $"Read-only .NET solution verification requires at least one declared path in '{variableKey}'.";
                return false;
            }

            issue = string.Empty;
            return true;
        }
        catch (JsonException)
        {
            issue = $"Read-only .NET solution verification requires '{variableKey}' to be a JSON string array.";
            return false;
        }
    }

    private static bool TryResolveProductPath(
        IReadOnlyDictionary<string, string> launchVariables,
        string variableKey,
        string productRoot,
        out string path,
        out string issue)
        => TryResolveProductPath(ResolveVariable(launchVariables, variableKey), variableKey, productRoot, out path, out issue);

    private static bool TryResolveProductPath(
        string value,
        string fieldName,
        string productRoot,
        out string path,
        out string issue)
    {
        path = string.Empty;
        if (string.IsNullOrWhiteSpace(value) || !Path.IsPathRooted(value))
        {
            issue = $"Read-only .NET solution verification requires authoritative absolute product path '{fieldName}'.";
            return false;
        }

        try
        {
            path = Path.GetFullPath(value);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            issue = $"Read-only .NET solution verification received an invalid path for '{fieldName}'.";
            return false;
        }

        var normalizedRoot = EnsureTrailingDirectorySeparator(productRoot);
        if (!path.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
        {
            issue = $"Read-only .NET solution verification requires '{fieldName}' to remain under ProductRoot.";
            return false;
        }

        issue = string.Empty;
        return true;
    }

    private static string ResolveVariable(IReadOnlyDictionary<string, string> variables, string key, string fallback = "")
        => variables.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : fallback;

    private static string EnsureTrailingDirectorySeparator(string path)
        => path.EndsWith(Path.DirectorySeparatorChar) || path.EndsWith(Path.AltDirectorySeparatorChar)
            ? path
            : path + Path.DirectorySeparatorChar;

    private static string NormalizePathText(string value)
        => value.Replace('\\', '/').Trim().ToLowerInvariant();

    private static string BuildManagedStepEvidenceRef(ProcessRuntimeStepAssignment assignment)
        => $"artifacts/process-runs/{assignment.RunId:D}/steps/{assignment.StepKey}.md";

    internal sealed record DotNetExistingSolutionVerificationInputs(
        string ProductRoot,
        string SolutionFile,
        IReadOnlyList<string> ImplementationProjectFiles,
        IReadOnlyList<string> TestProjectFiles,
        IReadOnlyList<string> RequiredFiles);
}
