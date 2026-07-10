using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Modules.Processes;

internal sealed class DotNetSolutionSetupRuntimeExecutor(
    IWorkspaceFileService workspaceFiles,
    IWorkspaceCommandExecutionService workspaceCommands) : IProcessRuntimeOwnedStepExecutor
{
    private const string WorkspaceDotnetNew = "workspace_dotnet_new";
    private const string WorkspacePowerShellRunScript = "workspace_pwsh_run_script";

    public async ValueTask<ProcessRuntimeOwnedStepExecutionResult?> TryExecuteAsync(
        ProcessRuntimeStepAssignment assignment,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(assignment);

        var guard = DotNetSolutionSetupToolPlanGuard.Evaluate(assignment);
        if (guard.Plan is null)
        {
            return null;
        }

        var executionRunId = Guid.NewGuid();
        if (!guard.IsSatisfied)
        {
            return RuntimeOwnedStepExecutionResultFailure(
                executionRunId,
                [],
                $"Runtime-owned .NET setup cannot execute because the deterministic tool plan is invalid: {string.Join("; ", guard.Issues.Select(issue => issue.Code))}.",
                $"{assignment.RunId}:{assignment.StepInstanceId}:runtime-owned-dotnet-setup:guard:{string.Join("|", guard.Issues.Select(issue => issue.Evidence))}");
        }

        var plan = guard.Plan;
        var receipts = new List<ToolExecutionReceiptRecord>();
        if (!TryResolveExecutionInputs(assignment, plan, out var inputs, out var inputIssue))
        {
            return RuntimeOwnedStepExecutionResultFailure(
                executionRunId,
                receipts,
                inputIssue,
                $"{assignment.RunId}:{assignment.StepInstanceId}:runtime-owned-dotnet-setup:inputs:{inputIssue}");
        }

        if (plan.Kind == DotNetSolutionSetupToolPlanKind.CreateProject)
        {
            var solutionScaffold = await EnsureDotNetNewTargetAsync(
                    executionRunId,
                    inputs.SolutionFile,
                    "sln",
                    Path.GetFileNameWithoutExtension(inputs.SolutionFile),
                    inputs.ProductRoot,
                    receipts,
                    cancellationToken)
                .ConfigureAwait(false);
            if (!solutionScaffold.Succeeded)
            {
                return RuntimeOwnedStepExecutionResultFailure(
                    executionRunId,
                    receipts,
                    solutionScaffold.Summary,
                    solutionScaffold.Evidence);
            }

            var appProject = inputs.AppProjectFile;
            if (string.IsNullOrWhiteSpace(appProject))
            {
                return RuntimeOwnedStepExecutionResultFailure(
                    executionRunId,
                    receipts,
                    "Runtime-owned .NET setup cannot create the app project because DotNetAppProjectFile was not resolved.",
                    $"{assignment.RunId}:{assignment.StepInstanceId}:runtime-owned-dotnet-setup:app-project-missing");
            }

            var appScaffold = await EnsureDotNetNewTargetAsync(
                    executionRunId,
                    appProject,
                    ResolveLaunchVariable(assignment.LaunchVariables, "DotNetAppTemplate", "console"),
                    Path.GetFileNameWithoutExtension(appProject),
                    ResolveDotNetNewParentDirectory(appProject, inputs.ProductRoot),
                    receipts,
                    cancellationToken)
                .ConfigureAwait(false);
            if (!appScaffold.Succeeded)
            {
                return RuntimeOwnedStepExecutionResultFailure(
                    executionRunId,
                    receipts,
                    appScaffold.Summary,
                    appScaffold.Evidence);
            }
        }

        var writeScript = workspaceFiles.WriteTextFile(plan.ScriptRef, plan.Script, overwrite: true);
        receipts.Add(ToToolReceipt(executionRunId, writeScript));
        if (!writeScript.Succeeded)
        {
            return RuntimeOwnedStepExecutionResultFailure(
                executionRunId,
                receipts,
                $"Runtime-owned .NET setup could not write helper script '{plan.ScriptRef}': {writeScript.Message}",
                $"{assignment.RunId}:{assignment.StepInstanceId}:runtime-owned-dotnet-setup:script-write:{plan.ScriptRef}:{writeScript.Message}");
        }

        var scriptStat = workspaceFiles.StatPath(plan.ScriptRef);
        receipts.Add(ToToolReceipt(executionRunId, scriptStat));
        if (!scriptStat.Succeeded || !scriptStat.Exists || !string.Equals(scriptStat.PathKind, "file", StringComparison.OrdinalIgnoreCase))
        {
            return RuntimeOwnedStepExecutionResultFailure(
                executionRunId,
                receipts,
                $"Runtime-owned .NET setup could not verify helper script '{plan.ScriptRef}': {scriptStat.Message}",
                $"{assignment.RunId}:{assignment.StepInstanceId}:runtime-owned-dotnet-setup:script-stat:{plan.ScriptRef}:{scriptStat.Message}");
        }

        var scriptRun = await workspaceCommands.PowerShellRunScript(
                plan.ScriptRef,
                arguments: null,
                outputPaths: [BuildRuntimeOwnedOutputPath(assignment, plan)],
                workingDirectory: inputs.ProductRootAlias,
                timeoutSeconds: 300,
                sideEffectManifest: plan.SideEffectManifest)
            .ConfigureAwait(false);
        receipts.Add(ToToolReceipt(executionRunId, scriptRun));
        if (!scriptRun.Succeeded)
        {
            return RuntimeOwnedStepExecutionResultFailure(
                executionRunId,
                receipts,
                $"Runtime-owned .NET setup helper failed for step '{assignment.StepKey}': {scriptRun.Message}",
                $"{assignment.RunId}:{assignment.StepInstanceId}:runtime-owned-dotnet-setup:script-run:{scriptRun.ExitCode}:{scriptRun.Message}:{scriptRun.StderrPreview}");
        }

        if (!ValidateRequiredReadback(assignment, inputs.ProductRoot, out var readbackIssue))
        {
            return RuntimeOwnedStepExecutionResultFailure(
                executionRunId,
                receipts,
                readbackIssue,
                $"{assignment.RunId}:{assignment.StepInstanceId}:runtime-owned-dotnet-setup:readback:{readbackIssue}");
        }

        var evidenceRefs = receipts
            .Select(receipt => receipt.RequestSummary)
            .Where(value => value.StartsWith("artifacts/", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(5)
            .ToArray();
        var outcome = new ProcessStepOutcomeResult
        {
            Status = ProcessStepOutcomeStatus.Completed,
            Reason = $"Runtime-owned .NET solution setup completed for step '{assignment.StepKey}'.",
            EvidenceRefs = evidenceRefs.Length == 0 ? [BuildRuntimeOwnedOutputPath(assignment, plan)] : evidenceRefs,
            NextActions = [],
            HumanReadableSummaryMarkdown = $"Runtime-owned .NET solution setup wrote and verified the helper script, ran {WorkspacePowerShellRunScript}, and read back required solution membership."
        };

        return new ProcessRuntimeOwnedStepExecutionResult(
            true,
            outcome,
            receipts,
            executionRunId,
            $"Runtime-owned .NET setup completed for step '{assignment.StepKey}'.",
            $"{assignment.RunId}:{assignment.StepInstanceId}:runtime-owned-dotnet-setup:succeeded:{string.Join("|", receipts.Select(receipt => $"{receipt.ToolName}:{receipt.ExitSummary}"))}");
    }

    private async Task<DotNetSolutionSetupOperationResult> EnsureDotNetNewTargetAsync(
        Guid executionRunId,
        string targetPath,
        string template,
        string name,
        string parentDirectory,
        List<ToolExecutionReceiptRecord> receipts,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (File.Exists(targetPath) || Directory.Exists(targetPath))
        {
            receipts.Add(CreateIdempotentSkipReceipt(
                executionRunId,
                WorkspaceDotnetNew,
                $"new {template} idempotent-skip existing target",
                ToExternalTargetAliasOrNative(parentDirectory),
                $"Succeeded: Existing .NET target '{Path.GetFileName(targetPath)}' was verified; destructive regeneration was skipped."));
            return DotNetSolutionSetupOperationResult.Ok;
        }

        Directory.CreateDirectory(parentDirectory);
        var dotnetNew = await workspaceCommands.DotnetNew(
                template,
                name,
                ToExternalTargetAliasOrNative(parentDirectory),
                force: false,
                timeoutSeconds: 300)
            .ConfigureAwait(false);
        receipts.Add(ToToolReceipt(executionRunId, dotnetNew));
        return dotnetNew.Succeeded
            ? DotNetSolutionSetupOperationResult.Ok
            : new DotNetSolutionSetupOperationResult(
                false,
                $"Runtime-owned .NET setup failed to scaffold '{name}' with template '{template}': {dotnetNew.Message}",
                $"dotnet-new:{template}:{name}:{dotnetNew.ExitCode}:{dotnetNew.Message}:{dotnetNew.StderrPreview}");
    }

    private static ProcessRuntimeOwnedStepExecutionResult RuntimeOwnedStepExecutionResultFailure(
        Guid executionRunId,
        IReadOnlyList<ToolExecutionReceiptRecord> receipts,
        string summary,
        string evidence)
        => new(
            false,
            null,
            receipts,
            executionRunId,
            summary,
            evidence);

    private static bool TryResolveExecutionInputs(
        ProcessRuntimeStepAssignment assignment,
        DotNetSolutionSetupToolPlan plan,
        out DotNetSolutionSetupRuntimeExecutionInputs inputs,
        out string issue)
    {
        inputs = null!;
        issue = string.Empty;

        var productRoot = ResolveLaunchVariable(assignment.LaunchVariables, "ProductRoot", ResolveLaunchVariable(assignment.LaunchVariables, "ExternalTargetRoot"));
        if (string.IsNullOrWhiteSpace(productRoot))
        {
            issue = "Runtime-owned .NET setup requires ProductRoot or ExternalTargetRoot.";
            return false;
        }

        productRoot = Path.GetFullPath(productRoot);
        var solutionFile = ResolveLaunchVariable(assignment.LaunchVariables, "DotNetSolutionFile");
        if (string.IsNullOrWhiteSpace(solutionFile))
        {
            solutionFile = plan.RequiredPaths.FirstOrDefault(path =>
                path.EndsWith(".slnx", StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith(".sln", StringComparison.OrdinalIgnoreCase)) ?? string.Empty;
        }

        if (string.IsNullOrWhiteSpace(solutionFile))
        {
            issue = "Runtime-owned .NET setup could not resolve DotNetSolutionFile.";
            return false;
        }

        var appProjectFile = ResolveLaunchVariable(assignment.LaunchVariables, "DotNetAppProjectFile");
        if (string.IsNullOrWhiteSpace(appProjectFile))
        {
            appProjectFile = plan.RequiredPaths.FirstOrDefault(path =>
                path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase)) ?? string.Empty;
        }

        var testProjectFile = ResolveLaunchVariable(assignment.LaunchVariables, "DotNetTestProjectFile");
        inputs = new DotNetSolutionSetupRuntimeExecutionInputs(
            productRoot,
            ToExternalTargetAliasOrNative(productRoot),
            Path.GetFullPath(solutionFile),
            string.IsNullOrWhiteSpace(appProjectFile) ? string.Empty : Path.GetFullPath(appProjectFile),
            string.IsNullOrWhiteSpace(testProjectFile) ? string.Empty : Path.GetFullPath(testProjectFile));
        return true;
    }

    private static bool ValidateRequiredReadback(
        ProcessRuntimeStepAssignment assignment,
        string productRoot,
        out string issue)
    {
        issue = string.Empty;
        var checks = ResolveReadbackChecks(assignment.LaunchVariables, assignment.StepKey);
        if (checks.Count == 0)
        {
            return true;
        }

        foreach (var check in checks)
        {
            var path = check.PathCandidates
                .Select(candidate => ResolveProductPath(productRoot, candidate))
                .FirstOrDefault(File.Exists);
            if (string.IsNullOrWhiteSpace(path))
            {
                if (!check.MustExist)
                {
                    continue;
                }

                issue = $"Required readback path was not found for step '{assignment.StepKey}'.";
                return false;
            }

            var content = File.ReadAllText(path);
            var normalizedContent = NormalizeReadbackText(content);
            var hasRequiredGroup = check.RequiredTextAnyGroups.Count == 0 ||
                                   check.RequiredTextAnyGroups.Any(group =>
                                       group.Any(value => normalizedContent.Contains(NormalizeReadbackText(value), StringComparison.OrdinalIgnoreCase)));
            if (!hasRequiredGroup)
            {
                issue = $"Required readback content was not found for step '{assignment.StepKey}'.";
                return false;
            }
        }

        return true;
    }

    private static IReadOnlyList<DotNetSolutionSetupReadbackCheck> ResolveReadbackChecks(
        IReadOnlyDictionary<string, string> launchVariables,
        string stepKey)
    {
        var value = ResolveLaunchVariable(launchVariables, ProcessRuntimeLaunchVariables.ProductCompletionRequiredFileContentChecks);
        if (!string.IsNullOrWhiteSpace(value))
        {
            return ParseReadbackChecks(value);
        }

        value = ResolveLaunchVariable(launchVariables, ProcessRuntimeLaunchVariables.ProductCompletionRequiredFileContentChecksByStep);
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        try
        {
            using var document = JsonDocument.Parse(value);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return [];
            }

            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (string.Equals(property.Name, stepKey, StringComparison.OrdinalIgnoreCase))
                {
                    return ParseReadbackChecks(property.Value);
                }
            }
        }
        catch (JsonException)
        {
            return [];
        }

        return [];
    }

    private static IReadOnlyList<DotNetSolutionSetupReadbackCheck> ParseReadbackChecks(string value)
    {
        try
        {
            using var document = JsonDocument.Parse(value);
            return ParseReadbackChecks(document.RootElement);
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static IReadOnlyList<DotNetSolutionSetupReadbackCheck> ParseReadbackChecks(JsonElement element)
    {
        var elements = element.ValueKind == JsonValueKind.Array
            ? element.EnumerateArray().ToArray()
            : [element];
        return elements
            .Where(item => item.ValueKind == JsonValueKind.Object)
            .Select(item => new DotNetSolutionSetupReadbackCheck(
                ReadStringArray(item, "pathCandidates"),
                ReadStringGroupArray(item, "requiredTextAnyGroups"),
                ReadBoolean(item, "mustExist", defaultValue: true)))
            .Where(check => check.PathCandidates.Count > 0)
            .ToArray();
    }

    private static IReadOnlyList<string> ReadStringArray(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) ||
            property.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return property.EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString() ?? string.Empty)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToArray();
    }

    private static IReadOnlyList<IReadOnlyList<string>> ReadStringGroupArray(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) ||
            property.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return property.EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.Array)
            .Select(item => (IReadOnlyList<string>)item.EnumerateArray()
                .Where(value => value.ValueKind == JsonValueKind.String)
                .Select(value => value.GetString() ?? string.Empty)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .ToArray())
            .Where(group => group.Count > 0)
            .ToArray();
    }

    private static bool ReadBoolean(JsonElement element, string propertyName, bool defaultValue)
        => element.TryGetProperty(propertyName, out var property) && property.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? property.GetBoolean()
            : defaultValue;

    private static string ResolveProductPath(string productRoot, string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        return Path.IsPathRooted(path)
            ? Path.GetFullPath(path)
            : Path.GetFullPath(Path.Combine(productRoot, path));
    }

    private static string ResolveDotNetNewParentDirectory(string projectFile, string productRoot)
    {
        var projectDirectory = Path.GetDirectoryName(projectFile);
        if (string.IsNullOrWhiteSpace(projectDirectory))
        {
            return productRoot;
        }

        return Directory.GetParent(projectDirectory)?.FullName ?? productRoot;
    }

    private static string NormalizeReadbackText(string value)
        => value.Replace('\\', '/').ReplaceLineEndings("\n");

    private static string ResolveLaunchVariable(
        IReadOnlyDictionary<string, string> launchVariables,
        string key,
        string fallback = "")
        => launchVariables.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : fallback;

    private static string BuildRuntimeOwnedOutputPath(
        ProcessRuntimeStepAssignment assignment,
        DotNetSolutionSetupToolPlan plan)
        => $"artifacts/process-runs/{assignment.RunId.Value:D}/tool-runs/{assignment.StepKey}.{plan.Kind}.runtime-owned-dotnet-setup.json";

    private static ToolExecutionReceiptRecord ToToolReceipt(
        Guid executionRunId,
        WorkspaceFileMutationResult result)
        => new(
            Guid.NewGuid(),
            executionRunId,
            "workspace-file",
            result.Receipt.Operation,
            result.Receipt.MutatesWorkspace ? "WorkspaceMutation" : "ReadOnlyWorkspace",
            "NotRequired",
            result.Receipt.Boundary,
            result.Path,
            ".",
            BuildExitSummary(result.Succeeded, result.Message),
            result.Receipt.StartedAtUtc,
            result.Receipt.CompletedAtUtc);

    private static ToolExecutionReceiptRecord ToToolReceipt(
        Guid executionRunId,
        WorkspacePathStatResult result)
        => new(
            Guid.NewGuid(),
            executionRunId,
            "workspace-file",
            result.Receipt.Operation,
            "ReadOnlyWorkspace",
            "NotRequired",
            result.Receipt.Boundary,
            result.Path,
            ".",
            BuildExitSummary(result.Succeeded, result.Message),
            result.Receipt.StartedAtUtc,
            result.Receipt.CompletedAtUtc);

    private static ToolExecutionReceiptRecord ToToolReceipt(
        Guid executionRunId,
        WorkspaceCommandExecutionResult result)
        => new(
            Guid.NewGuid(),
            executionRunId,
            "workspace-process",
            result.ToolName,
            result.RiskClass,
            result.ApprovalRequired ? "Required" : "NotRequired",
            result.Boundary.Notes,
            result.ArgumentsSummary,
            result.WorkingDirectory,
            result.Succeeded
                ? $"Succeeded (exit {result.ExitCode}): {result.Message}"
                : $"Failed (exit {result.ExitCode}): {result.Message}",
            result.Receipt.StartedAtUtc,
            result.Receipt.CompletedAtUtc);

    private static ToolExecutionReceiptRecord CreateIdempotentSkipReceipt(
        Guid executionRunId,
        string toolName,
        string requestSummary,
        string workingDirectory,
        string exitSummary)
        => new(
            Guid.NewGuid(),
            executionRunId,
            "process-runtime",
            toolName,
            "RuntimeOwned:IdempotentSkip",
            "NotRequired",
            "Runtime-owned deterministic .NET setup verified existing product state.",
            requestSummary,
            workingDirectory,
            exitSummary,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);

    private static string BuildExitSummary(bool succeeded, string message)
        => succeeded ? $"Succeeded: {message}" : $"Failed: {message}";

    private static string ToExternalTargetAliasOrNative(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return path;
        }

        var fullPath = Path.GetFullPath(path);
        var rootPath = Path.GetPathRoot(fullPath);
        if (string.IsNullOrWhiteSpace(rootPath))
        {
            return fullPath;
        }

        var trimmedRoot = rootPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (trimmedRoot.Length != 2 ||
            trimmedRoot[1] != ':' ||
            !char.IsLetter(trimmedRoot[0]))
        {
            return fullPath;
        }

        var relative = fullPath.Length <= rootPath.Length
            ? string.Empty
            : fullPath[rootPath.Length..]
                .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar)
                .TrimStart(Path.DirectorySeparatorChar);
        return string.IsNullOrWhiteSpace(relative)
            ? $"external-target/{char.ToUpperInvariant(trimmedRoot[0])}"
            : $"external-target/{char.ToUpperInvariant(trimmedRoot[0])}/{relative.Replace(Path.DirectorySeparatorChar, '/')}";
    }

    private sealed record DotNetSolutionSetupRuntimeExecutionInputs(
        string ProductRoot,
        string ProductRootAlias,
        string SolutionFile,
        string AppProjectFile,
        string TestProjectFile);

    private sealed record DotNetSolutionSetupReadbackCheck(
        IReadOnlyList<string> PathCandidates,
        IReadOnlyList<IReadOnlyList<string>> RequiredTextAnyGroups,
        bool MustExist);

    private sealed record DotNetSolutionSetupOperationResult(
        bool Succeeded,
        string Summary,
        string Evidence)
    {
        public static DotNetSolutionSetupOperationResult Ok { get; } = new(true, string.Empty, string.Empty);
    }
}

