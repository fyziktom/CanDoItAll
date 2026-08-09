using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure;
using CanDoItAll.Infrastructure.FileSystem;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Runtime;

using static CanDoItAll.Modules.Processes.ProcessRuntimeOwnedToolReceiptFactory;

namespace CanDoItAll.Modules.Processes;

internal sealed class DotNetExistingSolutionVerifier(
    IPhysicalFileSystemPathPolicyFactory physicalPathPolicyFactory)
{
    public ValueTask<ProcessRuntimeOwnedStepExecutionResult> VerifyAsync(
        ProcessRuntimeStepAssignment assignment,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(assignment);
        cancellationToken.ThrowIfCancellationRequested();

        var executionRunId = Guid.NewGuid();
        if (!TryResolveInputs(assignment.LaunchVariables, physicalPathPolicyFactory, out var inputs, out var issue))
        {
            return ValueTask.FromResult(Failed(
                assignment,
                executionRunId,
                [],
                issue,
                ProcessRuntimeOwnedStepFailures.ContractInvalid));
        }

        var files = new WorkspaceFileService(inputs.ProductRoot, physicalPathPolicyFactory);
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

        string? solutionFile = null;
        var foundExistingSolutionCandidate = false;
        var candidateIssues = new List<string>();
        foreach (var candidate in inputs.SolutionCandidateFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var relativeCandidate = Path.GetRelativePath(inputs.ProductRoot, candidate);
            var stat = files.StatPath(relativeCandidate);
            receipts.Add(From(executionRunId, stat));
            if (stat.IsKnownMissing())
            {
                continue;
            }

            if (!stat.Succeeded)
            {
                candidateIssues.Add(
                    $"'{Path.GetFileName(candidate)}' could not be inspected: {stat.Message}");
                continue;
            }

            if (!stat.Exists)
            {
                continue;
            }

            foundExistingSolutionCandidate = true;
            if (!string.Equals(stat.PathKind, "file", StringComparison.OrdinalIgnoreCase))
            {
                candidateIssues.Add($"'{Path.GetFileName(candidate)}' is not a file");
                continue;
            }

            var solutionRead = files.ReadTextFile(relativeCandidate, maxCharacters: 200000);
            receipts.Add(From(executionRunId, solutionRead));
            if (!solutionRead.Succeeded || solutionRead.IsTruncated)
            {
                candidateIssues.Add(
                    $"'{Path.GetFileName(candidate)}' could not be read completely: {solutionRead.Message}");
                continue;
            }

            var normalizedSolution = NormalizePathText(solutionRead.Content);
            var missingProjects = inputs.RequiredFiles
                .Where(projectFile =>
                    !normalizedSolution.Contains(
                        NormalizePathText(Path.GetRelativePath(inputs.ProductRoot, projectFile)),
                        inputs.PathComparison))
                .Select(Path.GetFileName)
                .ToArray();
            if (missingProjects.Length > 0)
            {
                candidateIssues.Add(
                    $"'{Path.GetFileName(candidate)}' does not include required project(s): {string.Join(", ", missingProjects)}");
                continue;
            }

            solutionFile = candidate;
            break;
        }

        if (solutionFile is null)
        {
            var summary = !foundExistingSolutionCandidate && candidateIssues.Count == 0
                ? $"The declared existing .NET solution context is missing every solution candidate: {string.Join(", ", inputs.SolutionCandidateFiles.Select(Path.GetFileName))}."
                : $"No declared existing .NET solution candidate satisfied the verification contract: {string.Join("; ", candidateIssues)}.";
            return ValueTask.FromResult(Failed(
                assignment,
                executionRunId,
                receipts,
                summary));
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
        string summary,
        ProcessRuntimeOwnedStepFailure? failure = null)
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
            $"{assignment.RunId}:{assignment.StepInstanceId}:runtime-owned-dotnet-existing-solution:failed:{summary}",
            failure ?? ProcessRuntimeOwnedStepFailures.VerificationFailed);

    internal static bool TryResolveInputs(
        IReadOnlyDictionary<string, string> launchVariables,
        IPhysicalFileSystemPathPolicyFactory physicalPathPolicyFactory,
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

        IPhysicalFileSystemPathPolicy productRootPolicy;
        try
        {
            PhysicalPathSyntaxPolicy.EnsureNativeOrRelative(
                productRoot,
                "read-only .NET solution verification ProductRoot");
            if (!Path.IsPathRooted(productRoot))
            {
                issue = "Read-only .NET solution verification requires ProductRoot or ExternalTargetRoot to be an absolute path.";
                return false;
            }

            productRoot = Path.GetFullPath(productRoot);
            productRootPolicy = physicalPathPolicyFactory.Create(productRoot);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or NotSupportedException or PathTooLongException or PhysicalPathValidationException)
        {
            issue = "Read-only .NET solution verification received an invalid product root.";
            return false;
        }

        if (!TryResolveSolutionCandidatePaths(launchVariables, productRootPolicy, out var solutionCandidateFiles, out issue) ||
            !TryReadProductPathList(launchVariables, "DotNetRequiredProjectFiles", productRootPolicy, requireAtLeastOne: true, out var implementationProjectFiles, out issue) ||
            !TryReadProductPathList(launchVariables, "DotNetTestProjectFiles", productRootPolicy, requireAtLeastOne: false, out var testProjectFiles, out issue))
        {
            return false;
        }

        inputs = new DotNetExistingSolutionVerificationInputs(
            productRoot,
            solutionCandidateFiles,
            implementationProjectFiles,
            testProjectFiles,
            [.. implementationProjectFiles, .. testProjectFiles],
            productRootPolicy.PathComparison);
        issue = string.Empty;
        return true;
    }

    private static bool TryResolveSolutionCandidatePaths(
        IReadOnlyDictionary<string, string> launchVariables,
        IPhysicalFileSystemPathPolicy productRootPolicy,
        out IReadOnlyList<string> paths,
        out string issue)
    {
        paths = [];
        var configured = ResolveVariable(launchVariables, "DotNetSolutionFileCandidates");
        var preferred = ResolveVariable(launchVariables, "DotNetSolutionFile");
        var candidates = configured.Split(
                ';',
                StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .ToList();
        if (!string.IsNullOrWhiteSpace(preferred) &&
            !candidates.Contains(preferred, productRootPolicy.PathComparer))
        {
            candidates.Insert(0, preferred);
        }

        if (candidates.Count == 0)
        {
            issue = "Read-only .NET solution verification requires 'DotNetSolutionFile' or 'DotNetSolutionFileCandidates'.";
            return false;
        }

        var resolved = new List<string>();
        foreach (var candidate in candidates)
        {
            if (!TryResolveProductPath(
                    candidate,
                    "DotNetSolutionFileCandidates",
                    productRootPolicy,
                    out var path,
                    out issue))
            {
                return false;
            }

            if (!resolved.Contains(path, productRootPolicy.PathComparer))
            {
                resolved.Add(path);
            }
        }

        paths = DotNetSolutionContextPathResolver
            .IncludeSupportedSolutionFormatAlternatives(resolved, productRootPolicy.PathComparer);
        issue = string.Empty;
        return true;
    }

    private static bool TryReadProductPathList(
        IReadOnlyDictionary<string, string> launchVariables,
        string variableKey,
        IPhysicalFileSystemPathPolicy productRootPolicy,
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
                if (!TryResolveProductPath(candidate, variableKey, productRootPolicy, out var path, out issue))
                {
                    return false;
                }

                if (!resolved.Contains(path, productRootPolicy.PathComparer))
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
        IPhysicalFileSystemPathPolicy productRootPolicy,
        out string path,
        out string issue)
        => TryResolveProductPath(ResolveVariable(launchVariables, variableKey), variableKey, productRootPolicy, out path, out issue);

    private static bool TryResolveProductPath(
        string value,
        string fieldName,
        IPhysicalFileSystemPathPolicy productRootPolicy,
        out string path,
        out string issue)
    {
        path = string.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            issue = $"Read-only .NET solution verification requires authoritative absolute product path '{fieldName}'.";
            return false;
        }

        try
        {
            PhysicalPathSyntaxPolicy.EnsureNativeOrRelative(
                value,
                $"read-only .NET solution verification {fieldName}");
            if (!Path.IsPathRooted(value))
            {
                issue = $"Read-only .NET solution verification requires authoritative absolute product path '{fieldName}'.";
                return false;
            }

            path = Path.GetFullPath(value);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or NotSupportedException or PathTooLongException)
        {
            issue = $"Read-only .NET solution verification received an invalid path for '{fieldName}'.";
            return false;
        }

        if (!productRootPolicy.IsWithinRoot(path))
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

    private static string NormalizePathText(string value)
        => value.Replace('\\', '/').Trim();

    private static string BuildManagedStepEvidenceRef(ProcessRuntimeStepAssignment assignment)
        => $"artifacts/process-runs/{assignment.RunId:D}/steps/{assignment.StepKey}.md";

    internal sealed record DotNetExistingSolutionVerificationInputs(
        string ProductRoot,
        IReadOnlyList<string> SolutionCandidateFiles,
        IReadOnlyList<string> ImplementationProjectFiles,
        IReadOnlyList<string> TestProjectFiles,
        IReadOnlyList<string> RequiredFiles,
        StringComparison PathComparison);
}
