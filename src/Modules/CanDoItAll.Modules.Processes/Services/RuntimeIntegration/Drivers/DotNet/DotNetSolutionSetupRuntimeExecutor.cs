using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Runtime;
using static CanDoItAll.Modules.Processes.ProcessRuntimeOwnedToolReceiptFactory;

namespace CanDoItAll.Modules.Processes;

internal sealed class DotNetSolutionSetupRuntimeExecutor(
    IWorkspaceCommandExecutionService workspaceCommands,
    WorkspaceManagedScriptPlanExecutor managedScriptPlanExecutor,
    DotNetExistingSolutionVerifier? existingSolutionVerifier = null) : IProcessRuntimeOwnedStepExecutor
{
    internal const string DriverKey = "dotnet.solution-setup";
    private const string WorkspaceDotnetNew = "workspace_dotnet_new";
    private const string WorkspacePowerShellRunScript = "workspace_pwsh_run_script";
    private readonly DotNetExistingSolutionVerifier existingSolutionVerifier = existingSolutionVerifier ?? new DotNetExistingSolutionVerifier();

    public string ExecutorKey => DriverKey;

    public async ValueTask<ProcessRuntimeOwnedStepExecutionResult?> TryExecuteAsync(
        ProcessRuntimeStepAssignment assignment,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(assignment);

        if (DotNetSolutionProvisioningModeReader.TryRead(
                assignment.LaunchVariables,
                out var provisioningMode,
                out var provisioningIssue))
        {
            if (provisioningMode == DotNetSolutionProvisioningMode.VerifyExisting)
            {
                return await this.existingSolutionVerifier
                    .VerifyAsync(assignment, cancellationToken)
                    .ConfigureAwait(false);
            }
        }
        else if (!string.IsNullOrWhiteSpace(provisioningIssue))
        {
            var modeExecutionRunId = Guid.NewGuid();
            return RuntimeOwnedStepExecutionResultFailure(
                modeExecutionRunId,
                [],
                provisioningIssue,
                $"{assignment.RunId}:{assignment.StepInstanceId}:runtime-owned-dotnet-setup:provisioning-mode:{provisioningIssue}");
        }

        var guard = DotNetSolutionSetupToolPlanGuard.Evaluate(assignment);
        var plan = guard.Plan;
        if (plan is null && guard.IsSatisfied)
        {
            return null;
        }

        var executionRunId = Guid.NewGuid();
        if (!guard.IsSatisfied || plan is null)
        {
            return RuntimeOwnedStepExecutionResultFailure(
                executionRunId,
                [],
                $"Runtime-owned .NET setup cannot execute because the deterministic tool plan is invalid: {string.Join("; ", guard.Issues.Select(issue => issue.Code))}.",
                $"{assignment.RunId}:{assignment.StepInstanceId}:runtime-owned-dotnet-setup:guard:{string.Join("|", guard.Issues.Select(issue => issue.Evidence))}");
        }

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
            if (!TryBuildTemplateSpecification(
                    ResolveLaunchVariable(assignment.LaunchVariables, "DotNetAppTemplate"),
                    ResolveLaunchVariable(assignment.LaunchVariables, "DotNetAppTemplateOptions"),
                    out var appTemplate,
                    out var templateIssue))
            {
                return RuntimeOwnedStepExecutionResultFailure(
                    executionRunId,
                    receipts,
                    $"Runtime-owned .NET setup cannot execute because the deterministic tool plan is invalid: {templateIssue}",
                    $"{assignment.RunId}:{assignment.StepInstanceId}:runtime-owned-dotnet-setup:guard:{templateIssue}");
            }

            var targetFramework = ResolveLaunchVariable(assignment.LaunchVariables, "DotNetTargetFramework");
            if (string.IsNullOrWhiteSpace(targetFramework))
            {
                return RuntimeOwnedStepExecutionResultFailure(
                    executionRunId,
                    receipts,
                    "Runtime-owned .NET setup cannot execute because the deterministic tool plan is invalid: dotnet.setup.plan.target_framework_missing",
                    $"{assignment.RunId}:{assignment.StepInstanceId}:runtime-owned-dotnet-setup:guard:dotnet.setup.plan.target_framework_missing");
            }

            var solutionScaffold = await EnsureDotNetNewTargetAsync(
                    executionRunId,
                    inputs.SolutionCandidateFiles,
                    "sln",
                    Path.GetFileNameWithoutExtension(inputs.SolutionFile),
                    ResolveDotNetNewParentDirectory(inputs.SolutionFile, inputs.ProductRoot),
                    null,
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
                    [appProject],
                    appTemplate,
                    Path.GetFileNameWithoutExtension(appProject),
                    ResolveDotNetProjectCreationParentDirectory(appProject, inputs.ProductRoot),
                    targetFramework,
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

        if (!TryResolveReadbackChecks(assignment.LaunchVariables, out var readbackChecks, out var readbackIssue))
        {
            return RuntimeOwnedStepExecutionResultFailure(
                executionRunId,
                receipts,
                readbackIssue,
                $"{assignment.RunId}:{assignment.StepInstanceId}:runtime-owned-dotnet-setup:readback:{readbackIssue}");
        }

        var managedScriptResult = await managedScriptPlanExecutor.ExecuteAsync(
                new WorkspaceManagedScriptPlanExecutionRequest(
                    executionRunId,
                    plan.ScriptRef,
                    plan.Script,
                    plan.SideEffectManifest,
                    inputs.ProductRootAlias,
                    BuildRuntimeOwnedOutputPath(assignment, plan),
                    inputs.ProductRoot,
                    readbackChecks,
                    $"{assignment.RunId}:{assignment.StepInstanceId}:runtime-owned-dotnet-setup"),
                cancellationToken)
            .ConfigureAwait(false);
        receipts.AddRange(managedScriptResult.ToolReceipts);
        if (!managedScriptResult.Succeeded)
        {
            return RuntimeOwnedStepExecutionResultFailure(
                executionRunId,
                receipts,
                managedScriptResult.Summary,
                managedScriptResult.Evidence);
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
        IReadOnlyList<string> targetPaths,
        string template,
        string name,
        string parentDirectory,
        string? targetFramework,
        List<ToolExecutionReceiptRecord> receipts,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var existingTarget = targetPaths.FirstOrDefault(targetPath => File.Exists(targetPath) || Directory.Exists(targetPath));
        if (existingTarget is not null)
        {
            receipts.Add(CreateIdempotentSkipReceipt(
                executionRunId,
                WorkspaceDotnetNew,
                $"new {template} idempotent-skip existing target",
                ToExternalTargetAliasOrNative(parentDirectory),
                $"Succeeded: Existing .NET target '{Path.GetFileName(existingTarget)}' was verified; destructive regeneration was skipped."));
            return DotNetSolutionSetupOperationResult.Ok;
        }

        Directory.CreateDirectory(parentDirectory);
        var dotnetNew = await workspaceCommands.DotnetNew(
                template,
                name,
                ToExternalTargetAliasOrNative(parentDirectory),
                force: false,
                timeoutSeconds: 300,
                targetFramework: targetFramework)
            .ConfigureAwait(false);
        receipts.Add(From(executionRunId, dotnetNew));
        if (!dotnetNew.Succeeded)
        {
            return new DotNetSolutionSetupOperationResult(
                false,
                $"Runtime-owned .NET setup failed to scaffold '{name}' with template '{template}': {dotnetNew.Message}",
                $"dotnet-new:{template}:{name}:{dotnetNew.ExitCode}:{dotnetNew.Message}:{dotnetNew.StderrPreview}");
        }

        var createdTarget = targetPaths.FirstOrDefault(File.Exists);
        if (createdTarget is null)
        {
            return new DotNetSolutionSetupOperationResult(
                false,
                $"Runtime-owned .NET setup completed dotnet new for '{name}', but none of the contracted target candidates were created: {string.Join(", ", targetPaths.Select(Path.GetFileName))}.",
                $"dotnet-new-target-missing:{template}:{name}:{string.Join("|", targetPaths)}");
        }

        return DotNetSolutionSetupOperationResult.Ok;
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

    private static bool TryBuildTemplateSpecification(
        string template,
        string options,
        out string specification,
        out string issue)
    {
        specification = string.Empty;
        issue = "dotnet.setup.plan.app_template_missing";
        var normalizedTemplate = template.Trim();
        if (string.IsNullOrWhiteSpace(normalizedTemplate))
        {
            return false;
        }

        var templateTokens = normalizedTemplate.Split(
            [' ', '\t', '\r', '\n'],
            StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (templateTokens.Length != 1)
        {
            issue = "dotnet.setup.plan.app_template_invalid";
            return false;
        }

        var optionTokens = string.IsNullOrWhiteSpace(options)
            ? []
            : options.Split(
                [' ', '\t', '\r', '\n'],
                StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (optionTokens.Any(option => !option.StartsWith("--", StringComparison.Ordinal)))
        {
            issue = "dotnet.setup.plan.app_template_options_invalid";
            return false;
        }

        if (optionTokens.Any(option => string.Equals(option, "--framework", StringComparison.OrdinalIgnoreCase)))
        {
            issue = "dotnet.setup.plan.app_template_framework_option_conflict";
            return false;
        }

        specification = string.Join(" ", [templateTokens[0], .. optionTokens]);
        issue = string.Empty;
        return true;
    }

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

        try
        {
            productRoot = Path.GetFullPath(productRoot);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            issue = "Runtime-owned .NET setup received an invalid ProductRoot or ExternalTargetRoot.";
            return false;
        }

        if (!TryResolveContractedProductFile(
                assignment.LaunchVariables,
                "DotNetSolutionFile",
                productRoot,
                out var solutionFile,
                out issue) ||
            !TryResolveSolutionCandidateFiles(
                assignment.LaunchVariables,
                productRoot,
                solutionFile,
                out var solutionCandidateFiles,
                out issue) ||
            !TryResolveContractedProductFile(
                assignment.LaunchVariables,
                "DotNetAppProjectFile",
                productRoot,
                out var appProjectFile,
                out issue))
        {
            return false;
        }

        var testProjectFile = string.Empty;
        if (plan.Kind != DotNetSolutionSetupToolPlanKind.CreateProject &&
            !TryResolveContractedProductFile(
                assignment.LaunchVariables,
                "DotNetTestProjectFile",
                productRoot,
                out testProjectFile,
                out issue))
        {
            return false;
        }

        inputs = new DotNetSolutionSetupRuntimeExecutionInputs(
            productRoot,
            ToExternalTargetAliasOrNative(productRoot),
            solutionFile,
            solutionCandidateFiles,
            appProjectFile,
            testProjectFile);
        return true;
    }

    private static bool TryResolveContractedProductFile(
        IReadOnlyDictionary<string, string> launchVariables,
        string variableKey,
        string productRoot,
        out string path,
        out string issue)
    {
        var configuredPath = ResolveLaunchVariable(launchVariables, variableKey);
        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            path = string.Empty;
            issue = $"Runtime-owned .NET setup requires authoritative launch variable '{variableKey}' from its bound bootstrap decision.";
            return false;
        }

        return TryNormalizeContractedProductFile(configuredPath, variableKey, productRoot, out path, out issue);
    }

    private static bool TryResolveSolutionCandidateFiles(
        IReadOnlyDictionary<string, string> launchVariables,
        string productRoot,
        string solutionFile,
        out IReadOnlyList<string> candidateFiles,
        out string issue)
    {
        var configuredCandidates = ResolveLaunchVariable(launchVariables, "DotNetSolutionFileCandidates");
        if (string.IsNullOrWhiteSpace(configuredCandidates))
        {
            candidateFiles = [solutionFile];
            issue = string.Empty;
            return true;
        }

        var normalizedCandidates = new List<string> { solutionFile };
        foreach (var configuredCandidate in configuredCandidates.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (!TryNormalizeContractedProductFile(
                    configuredCandidate,
                    "DotNetSolutionFileCandidates",
                    productRoot,
                    out var candidate,
                    out issue))
            {
                candidateFiles = [];
                return false;
            }

            if (!normalizedCandidates.Contains(candidate, StringComparer.OrdinalIgnoreCase))
            {
                normalizedCandidates.Add(candidate);
            }
        }

        candidateFiles = normalizedCandidates;
        issue = string.Empty;
        return true;
    }

    private static bool TryNormalizeContractedProductFile(
        string configuredPath,
        string variableKey,
        string productRoot,
        out string path,
        out string issue)
    {
        path = configuredPath;
        issue = string.Empty;
        if (!Path.IsPathRooted(path))
        {
            issue = $"Runtime-owned .NET setup requires '{variableKey}' to be an absolute path under ProductRoot.";
            return false;
        }

        try
        {
            path = Path.GetFullPath(path);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            issue = $"Runtime-owned .NET setup received an invalid '{variableKey}' path.";
            return false;
        }

        var normalizedRoot = EnsureTrailingDirectorySeparator(productRoot);
        if (!path.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
        {
            issue = $"Runtime-owned .NET setup requires '{variableKey}' to remain under ProductRoot.";
            return false;
        }

        return true;
    }

    private static string EnsureTrailingDirectorySeparator(string path)
        => path.EndsWith(Path.DirectorySeparatorChar) || path.EndsWith(Path.AltDirectorySeparatorChar)
            ? path
            : path + Path.DirectorySeparatorChar;

    private static bool TryResolveReadbackChecks(
        IReadOnlyDictionary<string, string> launchVariables,
        out IReadOnlyList<WorkspaceManagedScriptReadbackCheck> checks,
        out string issue)
    {
        checks = [];
        var value = ResolveLaunchVariable(launchVariables, ProcessRuntimeLaunchVariables.ProductCompletionRequiredFileContentChecks);
        if (string.IsNullOrWhiteSpace(value))
        {
            issue = "Runtime-owned .NET setup cannot execute because the deterministic tool plan is invalid: dotnet.setup.plan.readback_checks_missing.";
            return false;
        }

        if (WorkspaceManagedScriptReadbackContractParser.TryParse(value, out checks, out _))
        {
            issue = string.Empty;
            return true;
        }

        issue = "Runtime-owned .NET setup cannot execute because the deterministic tool plan is invalid: dotnet.setup.plan.readback_check_invalid.";
        return false;
    }

    private static string ResolveDotNetNewParentDirectory(string projectFile, string productRoot)
    {
        var projectDirectory = Path.GetDirectoryName(projectFile);
        if (string.IsNullOrWhiteSpace(projectDirectory))
        {
            return productRoot;
        }

        return projectDirectory;
    }

    private static string ResolveDotNetProjectCreationParentDirectory(string projectFile, string productRoot)
    {
        var projectDirectory = Path.GetDirectoryName(projectFile);
        if (string.IsNullOrWhiteSpace(projectDirectory))
        {
            return productRoot;
        }

        return Directory.GetParent(projectDirectory)?.FullName ?? productRoot;
    }

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
        IReadOnlyList<string> SolutionCandidateFiles,
        string AppProjectFile,
        string TestProjectFile);

    private sealed record DotNetSolutionSetupOperationResult(
        bool Succeeded,
        string Summary,
        string Evidence)
    {
        public static DotNetSolutionSetupOperationResult Ok { get; } = new(true, string.Empty, string.Empty);
    }
}
