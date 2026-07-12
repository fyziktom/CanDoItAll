using CanDoItAll.AgentFramework.Models;
using System.Security.Cryptography;

namespace CanDoItAll.AgentFramework.Core;

internal sealed class WorkspaceCommandProcessRunner
{
    private readonly IWorkspaceProcessHost processHost;
    private readonly WorkspaceCommandEnvironmentPolicy environmentPolicy;
    private readonly WorkspaceExecutableLocator executableLocator;
    private readonly WorkspaceCommandReceiptWriter receiptWriter;

    public WorkspaceCommandProcessRunner(
        IWorkspaceProcessHost processHost,
        WorkspaceCommandEnvironmentPolicy environmentPolicy,
        WorkspaceExecutableLocator executableLocator,
        WorkspaceCommandReceiptWriter receiptWriter)
    {
        this.processHost = processHost;
        this.environmentPolicy = environmentPolicy;
        this.executableLocator = executableLocator;
        this.receiptWriter = receiptWriter;
    }

    public ExecutionBoundaryDescriptor DescribeBoundary() => processHost.DescribeBoundary();

    public WorkspaceCommandExecutionResult CreateBoundaryResult()
    {
        var boundary = DescribeBoundary();
        var now = DateTimeOffset.UtcNow;
        var message = $"Workspace command host '{boundary.HostLabel}' reports isolation mode '{boundary.Mode}' (host-enforced: {boundary.IsEnforcedByHost.ToString().ToLowerInvariant()}).";
        var receipt = receiptWriter.CreateAuditedReceipt(
            operation: "workspace_execution_boundary",
            mutatesWorkspace: false,
            boundary: WorkspaceCommandReceiptWriter.BuildBoundarySummary(boundary),
            outcome: "Succeeded",
            message: message,
            receiptRelativePath: string.Empty,
            targetPaths: [],
            artifactReferences: [],
            startedAtUtc: now,
            completedAtUtc: now,
            toolFamily: "workspace-process",
            riskClass: "ReadOnly",
            approvalMode: "NotRequired",
            isolationGuarantee: WorkspaceCommandReceiptWriter.BuildBoundarySummary(boundary),
            requestSummary: "execution boundary",
            workingDirectory: ".",
            exitSummary: "Succeeded");

        return new WorkspaceCommandExecutionResult(
            Succeeded: true,
            Message: message,
            Receipt: receipt,
            ToolName: "workspace_execution_boundary",
            RecipeId: "execution_boundary",
            RiskClass: "ReadOnly",
            ApprovalRequired: false,
            Boundary: boundary,
            WorkingDirectory: ".",
            ArgumentsSummary: "(none)",
            ExitCode: 0,
            StdoutPreview: string.Empty,
            StderrPreview: string.Empty,
            StdoutTruncated: false,
            StderrTruncated: false);
    }

    public WorkspaceCommandExecutionResult CreateDeniedResult(string toolName, string recipeId, string riskClass, bool approvalRequired, string message)
    {
        var boundary = DescribeBoundary();
        var now = DateTimeOffset.UtcNow;
        var receipt = receiptWriter.CreateAuditedReceipt(
            operation: toolName,
            mutatesWorkspace: false,
            boundary: WorkspaceCommandReceiptWriter.BuildBoundarySummary(boundary),
            outcome: "Denied",
            message: message,
            receiptRelativePath: string.Empty,
            targetPaths: [],
            artifactReferences: [],
            startedAtUtc: now,
            completedAtUtc: now,
            toolFamily: "workspace-process",
            riskClass: riskClass,
            approvalMode: approvalRequired ? "Required" : "NotRequired",
            isolationGuarantee: WorkspaceCommandReceiptWriter.BuildBoundarySummary(boundary),
            requestSummary: recipeId,
            workingDirectory: ".",
            exitSummary: "Denied");

        return new WorkspaceCommandExecutionResult(
            Succeeded: false,
            Message: message,
            Receipt: receipt,
            ToolName: toolName,
            RecipeId: recipeId,
            RiskClass: riskClass,
            ApprovalRequired: approvalRequired,
            Boundary: boundary,
            WorkingDirectory: ".",
            ArgumentsSummary: string.Empty,
            ExitCode: -1,
            StdoutPreview: string.Empty,
            StderrPreview: string.Empty,
            StdoutTruncated: false,
            StderrTruncated: false);
    }

    public async Task<WorkspaceCommandExecutionResult> ExecuteAsync(WorkspaceCommandPlan plan, CancellationToken cancellationToken = default)
    {
        var executablePath = executableLocator.ResolveExecutablePath(plan.ExecutableCandidates);
        var productTargetAudit = ProductTargetMutationAudit.CaptureBefore(
            plan,
            WorkspaceExecutionAuditContext.Current);
        using var pathAliasSession = WorkspacePathAliasSession.TryCreate(
            plan.WorkspaceRootPath,
            plan.WorkingDirectoryPath,
            plan.Arguments);
        var effectiveWorkingDirectoryPath = pathAliasSession?.RewritePath(plan.WorkingDirectoryPath) ?? plan.WorkingDirectoryPath;
        var effectiveArguments = pathAliasSession?.RewriteArguments(plan.Arguments) ?? plan.Arguments;
        var processResult = await processHost.ExecuteAsync(
            new WorkspaceProcessExecutionRequest(
                ToolName: plan.Decision.ToolName,
                RecipeId: plan.Decision.RecipeId,
                ExecutablePath: executablePath,
                Arguments: effectiveArguments,
                WorkingDirectory: effectiveWorkingDirectoryPath,
                EnvironmentVariables: environmentPolicy.MergeEnvironmentVariables(plan.EnvironmentVariables),
                TimeoutSeconds: plan.TimeoutSeconds,
                StdoutLimitCharacters: plan.StdoutLimitCharacters,
                StderrLimitCharacters: plan.StderrLimitCharacters),
            cancellationToken).ConfigureAwait(false);

        var effectiveProcessResult = NormalizeProcessResult(plan.Decision.ToolName, processResult);
        effectiveProcessResult = productTargetAudit.Apply(effectiveProcessResult);
        var succeeded = effectiveProcessResult.Started && !effectiveProcessResult.TimedOut && effectiveProcessResult.ExitCode == 0;
        if (effectiveProcessResult.TimedOut)
        {
            AgentFrameworkTelemetry.RecordCommandTimeout(plan.Decision.RecipeId, plan.Decision.RiskClass);
        }

        var message = !effectiveProcessResult.Started
            ? $"Recipe '{plan.Decision.RecipeId}' failed to start: {effectiveProcessResult.FailureMessage}"
            : effectiveProcessResult.TimedOut
                ? $"Recipe '{plan.Decision.RecipeId}' timed out after {plan.TimeoutSeconds} second(s)."
                : succeeded
                    ? $"Recipe '{plan.Decision.RecipeId}' completed successfully."
                    : string.IsNullOrWhiteSpace(effectiveProcessResult.FailureMessage)
                        ? $"Recipe '{plan.Decision.RecipeId}' failed with exit code {effectiveProcessResult.ExitCode}."
                        : $"Recipe '{plan.Decision.RecipeId}' failed with exit code {effectiveProcessResult.ExitCode}. {effectiveProcessResult.FailureMessage}";
        var receipt = receiptWriter.PersistProcessReceipt(
            plan.Decision.ToolName,
            plan.Decision.RecipeId,
            plan.Decision,
            plan.WorkingDirectory,
            plan.Arguments,
            plan.TargetPaths,
            plan.MutatesWorkspace,
            message,
            effectiveProcessResult,
            plan.DeclaredSideEffectMode);
        var resultMessage = AppendFailureDiagnosticHint(message, receipt, effectiveProcessResult, succeeded);

        return new WorkspaceCommandExecutionResult(
            Succeeded: succeeded,
            Message: resultMessage,
            Receipt: receipt,
            ToolName: plan.Decision.ToolName,
            RecipeId: plan.Decision.RecipeId,
            RiskClass: plan.Decision.RiskClass,
            ApprovalRequired: plan.Decision.ApprovalRequired,
            Boundary: effectiveProcessResult.Boundary,
            WorkingDirectory: plan.WorkingDirectory,
            ArgumentsSummary: WorkspaceCommandReceiptWriter.BuildArgumentsSummary(plan.Arguments),
            ExitCode: effectiveProcessResult.ExitCode,
            StdoutPreview: effectiveProcessResult.Stdout,
            StderrPreview: effectiveProcessResult.Stderr,
            StdoutTruncated: effectiveProcessResult.StdoutTruncated,
            StderrTruncated: effectiveProcessResult.StderrTruncated);
    }

    private static string AppendFailureDiagnosticHint(
        string message,
        WorkspaceToolReceipt receipt,
        WorkspaceProcessExecutionResult processResult,
        bool succeeded)
    {
        if (succeeded)
        {
            return message;
        }

        var stdoutPath = receipt.ArtifactReferences.FirstOrDefault(item =>
            item.DisplayName.Contains("stdout", StringComparison.OrdinalIgnoreCase))?.RelativePath;
        var stderrPath = receipt.ArtifactReferences.FirstOrDefault(item =>
            item.DisplayName.Contains("stderr", StringComparison.OrdinalIgnoreCase))?.RelativePath;
        var diagnosticPreview = BuildFailureDiagnosticPreview(processResult);
        if (string.IsNullOrWhiteSpace(stdoutPath) && string.IsNullOrWhiteSpace(stderrPath) &&
            string.IsNullOrWhiteSpace(diagnosticPreview))
        {
            return message;
        }

        var diagnosticHint = $"Inspect captured diagnostics before editing or retrying. stdout: {stdoutPath ?? "(none)"}; stderr: {stderrPath ?? "(none)"}.";
        return string.IsNullOrWhiteSpace(diagnosticPreview)
            ? $"{message} {diagnosticHint}"
            : $"{message} {diagnosticHint}{Environment.NewLine}{diagnosticPreview}";
    }

    private static string BuildFailureDiagnosticPreview(WorkspaceProcessExecutionResult processResult)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(processResult.Stderr))
        {
            parts.Add(BuildPreviewBlock("stderr", processResult.Stderr, processResult.StderrTruncated));
        }

        if (!string.IsNullOrWhiteSpace(processResult.Stdout))
        {
            parts.Add(BuildPreviewBlock("stdout", processResult.Stdout, processResult.StdoutTruncated));
        }

        return parts.Count == 0
            ? string.Empty
            : "Captured diagnostics preview:" + Environment.NewLine + string.Join(Environment.NewLine, parts);
    }

    private static string BuildPreviewBlock(string label, string text, bool truncated)
    {
        const int MaxPreviewCharacters = 4000;
        var trimmed = text.Trim();
        var preview = trimmed.Length <= MaxPreviewCharacters
            ? trimmed
            : trimmed[^MaxPreviewCharacters..];
        var truncationNote = truncated || trimmed.Length > MaxPreviewCharacters
            ? " tail"
            : string.Empty;
        return $"{label}{truncationNote}:{Environment.NewLine}{preview}";
    }

    private static WorkspaceProcessExecutionResult NormalizeProcessResult(
        string toolName,
        WorkspaceProcessExecutionResult processResult)
    {
        if (!string.Equals(toolName, "workspace_pwsh_run_script", StringComparison.OrdinalIgnoreCase) ||
            !processResult.Started ||
            processResult.TimedOut ||
            processResult.ExitCode != 0 ||
            !ContainsPowerShellErrorRecord(processResult.Stderr))
        {
            return processResult;
        }

        var failureMessage = string.IsNullOrWhiteSpace(processResult.FailureMessage)
            ? "PowerShell reported errors on stderr despite exit code 0."
            : $"PowerShell reported errors on stderr despite exit code 0. {processResult.FailureMessage}";

        return processResult with
        {
            ExitCode = 1,
            FailureMessage = failureMessage
        };
    }

    private static bool ContainsPowerShellErrorRecord(string? stderr)
    {
        if (string.IsNullOrWhiteSpace(stderr))
        {
            return false;
        }

        return stderr.Contains("WriteError:", StringComparison.OrdinalIgnoreCase) ||
               stderr.Contains("ParserError:", StringComparison.OrdinalIgnoreCase) ||
               stderr.Contains("RuntimeException:", StringComparison.OrdinalIgnoreCase) ||
               stderr.Contains("CommandNotFoundException:", StringComparison.OrdinalIgnoreCase) ||
               stderr.Contains("ParameterBindingException:", StringComparison.OrdinalIgnoreCase) ||
               stderr.Contains("FullyQualifiedErrorId", StringComparison.OrdinalIgnoreCase) ||
               stderr.Contains("Cannot overwrite variable PID", StringComparison.OrdinalIgnoreCase);
    }

    private sealed class ProductTargetMutationAudit
    {
        private readonly IReadOnlyList<ProductTargetSnapshot> beforeSnapshots;

        private ProductTargetMutationAudit(IReadOnlyList<ProductTargetSnapshot> beforeSnapshots)
        {
            this.beforeSnapshots = beforeSnapshots;
        }

        public static ProductTargetMutationAudit CaptureBefore(
            WorkspaceCommandPlan plan,
            WorkspaceExecutionAuditContext.WorkspaceExecutionAuditScopeState? auditScope)
        {
            if (auditScope is null ||
                auditScope.ProcessAllowsProductMutation ||
                !IsScriptExecutionTool(plan.Decision.ToolName))
            {
                return new ProductTargetMutationAudit([]);
            }

            var pathPolicy = new WorkspacePathPolicy(plan.WorkspaceRootPath);
            var snapshots = auditScope.AllowedExternalTargetAliases
                .Concat(auditScope.ReadOnlyExternalTargetAliases)
                .Where(alias => IsProductExternalTargetAlias(alias))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(alias => CaptureSnapshot(pathPolicy, alias))
                .ToArray();
            return new ProductTargetMutationAudit(snapshots);
        }

        public WorkspaceProcessExecutionResult Apply(WorkspaceProcessExecutionResult processResult)
        {
            if (beforeSnapshots.Count == 0)
            {
                return processResult;
            }

            var changedAliases = beforeSnapshots
                .Where(snapshot => snapshot.HasChanged())
                .Select(snapshot => snapshot.Alias)
                .ToArray();
            if (changedAliases.Length == 0)
            {
                return processResult;
            }

            var failureMessage = $"Post-execution product target audit detected file-system changes under non-mutating governed script roots: {string.Join(", ", changedAliases)}.";
            return processResult with
            {
                ExitCode = processResult.ExitCode == 0 ? 1 : processResult.ExitCode,
                FailureMessage = string.IsNullOrWhiteSpace(processResult.FailureMessage)
                    ? failureMessage
                    : $"{processResult.FailureMessage} {failureMessage}"
            };
        }

        private static ProductTargetSnapshot CaptureSnapshot(WorkspacePathPolicy pathPolicy, string alias)
        {
            if (!pathPolicy.TryResolveWorkspacePath(alias, allowWorkspaceRoot: false, out var resolution, out _))
            {
                return new ProductTargetSnapshot(
                    alias,
                    string.Empty,
                    new Dictionary<string, ProductTargetFileFingerprint>(StringComparer.OrdinalIgnoreCase));
            }

            return new ProductTargetSnapshot(
                alias,
                resolution.FullPath,
                CaptureFileFingerprints(resolution.FullPath));
        }

        private static IReadOnlyDictionary<string, ProductTargetFileFingerprint> CaptureFileFingerprints(
            string rootPath)
        {
            if (string.IsNullOrWhiteSpace(rootPath))
            {
                return new Dictionary<string, ProductTargetFileFingerprint>(StringComparer.OrdinalIgnoreCase);
            }

            if (File.Exists(rootPath))
            {
                return new Dictionary<string, ProductTargetFileFingerprint>(StringComparer.OrdinalIgnoreCase)
                {
                    ["."] = CreateFingerprint(rootPath)
                };
            }

            if (!Directory.Exists(rootPath))
            {
                return new Dictionary<string, ProductTargetFileFingerprint>(StringComparer.OrdinalIgnoreCase);
            }

            return Directory
                .EnumerateFiles(
                    rootPath,
                    "*",
                    new EnumerationOptions
                    {
                        RecurseSubdirectories = true,
                        IgnoreInaccessible = false,
                        AttributesToSkip = FileAttributes.ReparsePoint
                    })
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    path => WorkspacePathPolicy.NormalizeRelativePath(Path.GetRelativePath(rootPath, path)),
                    CreateFingerprint,
                    StringComparer.OrdinalIgnoreCase);
        }

        private static ProductTargetFileFingerprint CreateFingerprint(string path)
        {
            var fileInfo = new FileInfo(path);
            return new ProductTargetFileFingerprint(
                fileInfo.Length,
                fileInfo.LastWriteTimeUtc,
                ComputeFileHash(path));
        }

        private static string ComputeFileHash(string path)
        {
            using var stream = File.OpenRead(path);
            using var sha256 = SHA256.Create();
            return Convert.ToHexString(sha256.ComputeHash(stream));
        }

        private static bool IsScriptExecutionTool(string toolName)
        {
            return string.Equals(toolName, AgentToolInvocationPolicyMetadata.WorkspacePowerShellRunScript, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(toolName, AgentToolInvocationPolicyMetadata.WorkspacePythonRunFile, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsProductExternalTargetAlias(string alias)
        {
            var normalizedAlias = AgentWorkspaceToolAccessMetadata.NormalizeExternalTargetAlias(alias);
            if (string.IsNullOrWhiteSpace(normalizedAlias) ||
                !normalizedAlias.StartsWith("external-target/", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var segments = normalizedAlias
                .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Skip(2)
                .ToArray();
            if (segments.Length == 0)
            {
                return false;
            }

            if (segments.Any(segment =>
                    string.Equals(segment, "product", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(segment, "source", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(segment, "src", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(segment, "app", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            return !segments.Any(segment =>
                string.Equals(segment, "artifact", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(segment, "artifacts", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(segment, "evidence", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(segment, "report", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(segment, "reports", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(segment, "decision", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(segment, "decisions", StringComparison.OrdinalIgnoreCase));
        }

        private sealed record ProductTargetSnapshot(
            string Alias,
            string RootPath,
            IReadOnlyDictionary<string, ProductTargetFileFingerprint> Files)
        {
            public bool HasChanged()
            {
                var afterFiles = CaptureFileFingerprints(RootPath);
                if (Files.Count != afterFiles.Count)
                {
                    return true;
                }

                foreach (var beforeFile in Files)
                {
                    if (!afterFiles.TryGetValue(beforeFile.Key, out var afterFile) ||
                        beforeFile.Value != afterFile)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        private sealed record ProductTargetFileFingerprint(
            long Length,
            DateTime LastWriteTimeUtc,
            string Sha256);
    }
}
