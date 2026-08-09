using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.FileSystem;
using CanDoItAll.SharedKernel;
using System.Security.Cryptography;

namespace CanDoItAll.AgentFramework.Core;

internal sealed class WorkspaceCommandProcessRunner
{
    private readonly IWorkspaceProcessHost processHost;
    private readonly WorkspaceCommandEnvironmentPolicy environmentPolicy;
    private readonly WorkspaceExecutableLocator executableLocator;
    private readonly WorkspaceCommandReceiptWriter receiptWriter;
    private readonly WorkspacePathPolicy pathPolicy;

    public WorkspaceCommandProcessRunner(
        IWorkspaceProcessHost processHost,
        WorkspaceCommandEnvironmentPolicy environmentPolicy,
        WorkspaceExecutableLocator executableLocator,
        WorkspaceCommandReceiptWriter receiptWriter,
        WorkspacePathPolicy pathPolicy)
    {
        this.processHost = processHost;
        this.environmentPolicy = environmentPolicy;
        this.executableLocator = executableLocator;
        this.receiptWriter = receiptWriter;
        this.pathPolicy = pathPolicy;
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
            WorkspaceExecutionAuditContext.Current,
            pathPolicy);
        using var pathAliasSession = WorkspacePathAliasSession.TryCreate(
            plan.WorkspaceRootPath,
            plan.WorkingDirectoryPath,
            plan.Arguments,
            pathPolicy);
        var effectiveWorkingDirectoryPath = pathAliasSession?.RewritePath(plan.WorkingDirectoryPath) ?? plan.WorkingDirectoryPath;
        var effectiveArguments = pathAliasSession?.RewriteArguments(plan.Arguments) ?? plan.Arguments;
        var environmentVariables = environmentPolicy.MergeEnvironmentVariables(plan.EnvironmentVariables);
        pathPolicy.ValidatePathForUse(plan.WorkingDirectoryPath);
        var processResult = await processHost.ExecuteAsync(
            new WorkspaceProcessExecutionRequest(
                ToolName: plan.Decision.ToolName,
                RecipeId: plan.Decision.RecipeId,
                ExecutablePath: executablePath,
                Arguments: effectiveArguments,
                WorkingDirectory: effectiveWorkingDirectoryPath,
                EnvironmentVariables: environmentVariables,
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
            plan.DeclaredSideEffectMode,
            environmentVariables.Keys
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToArray());
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
        private const int MaximumAuditRoots = 32;
        private const int MaximumAuditEntries = 8192;
        private const int MaximumAuditDirectories = 4096;
        private const int MaximumAuditFiles = 2048;
        private const long MaximumAuditBytes = 100L * 1024 * 1024;

        private readonly IReadOnlyList<ProductTargetSnapshot> beforeSnapshots;

        private ProductTargetMutationAudit(IReadOnlyList<ProductTargetSnapshot> beforeSnapshots)
        {
            this.beforeSnapshots = beforeSnapshots;
        }

        public static ProductTargetMutationAudit CaptureBefore(
            WorkspaceCommandPlan plan,
            WorkspaceExecutionAuditContext.WorkspaceExecutionAuditScopeState? auditScope,
            WorkspacePathPolicy pathPolicy)
        {
            if (auditScope is null ||
                auditScope.ProcessAllowsProductMutation ||
                !IsScriptExecutionTool(plan.Decision.ToolName))
            {
                return new ProductTargetMutationAudit([]);
            }

            var aliases = auditScope.AllowedExternalTargetAliases
                .Concat(auditScope.ReadOnlyExternalTargetAliases)
                .Where(alias => IsProductExternalTargetAlias(alias))
                .Distinct(ExternalTargetAliasCodec.EqualityComparer)
                .Take(MaximumAuditRoots + 1)
                .ToArray();
            if (aliases.Length > MaximumAuditRoots)
            {
                throw new ProductTargetAuditUnavailableException(aliases[MaximumAuditRoots]);
            }

            var budget = new ProductTargetAuditBudget();
            var snapshots = aliases
                .Select(alias => CaptureSnapshot(pathPolicy, alias, budget))
                .ToArray();
            return new ProductTargetMutationAudit(snapshots);
        }

        public WorkspaceProcessExecutionResult Apply(WorkspaceProcessExecutionResult processResult)
        {
            if (beforeSnapshots.Count == 0)
            {
                return processResult;
            }

            var changedAliases = new List<string>();
            var inaccessibleAliases = new List<string>();
            var budget = new ProductTargetAuditBudget();
            foreach (var snapshot in beforeSnapshots)
            {
                try
                {
                    if (snapshot.HasChanged(budget))
                    {
                        changedAliases.Add(snapshot.Alias);
                    }
                }
                catch (Exception exception) when (
                    exception is UnauthorizedAccessException or IOException or ProductTargetAuditBoundsExceededException)
                {
                    inaccessibleAliases.Add(snapshot.Alias);
                }
            }

            if (changedAliases.Count == 0 && inaccessibleAliases.Count == 0)
            {
                return processResult;
            }

            var auditFailures = new List<string>(2);
            if (changedAliases.Count > 0)
            {
                auditFailures.Add(
                    $"detected file-system changes under non-mutating governed script roots: {string.Join(", ", changedAliases)}");
            }

            if (inaccessibleAliases.Count > 0)
            {
                auditFailures.Add(
                    $"could not complete the post-execution inspection for governed script roots: {string.Join(", ", inaccessibleAliases)}");
            }

            var failureMessage =
                $"Post-execution product target audit {string.Join("; and ", auditFailures)}. Treat the command outcome as unverified and inspect the captured diagnostics before retrying.";
            return processResult with
            {
                ExitCode = processResult.ExitCode == 0 ? 1 : processResult.ExitCode,
                FailureMessage = string.IsNullOrWhiteSpace(processResult.FailureMessage)
                    ? failureMessage
                    : $"{processResult.FailureMessage} {failureMessage}"
            };
        }

        private static ProductTargetSnapshot CaptureSnapshot(
            WorkspacePathPolicy pathPolicy,
            string alias,
            ProductTargetAuditBudget budget)
        {
            if (!pathPolicy.TryResolveWorkspacePath(alias, allowWorkspaceRoot: false, out var resolution, out _))
            {
                throw WorkspaceToolAccessDeniedException.InaccessiblePath(alias);
            }

            try
            {
                var physicalPathPolicy = pathPolicy.GetPhysicalPathPolicy(resolution.FullPath);
                return new ProductTargetSnapshot(
                    alias,
                    resolution.FullPath,
                    physicalPathPolicy,
                    CaptureFileFingerprints(resolution.FullPath, physicalPathPolicy, budget));
            }
            catch (Exception exception) when (
                exception is UnauthorizedAccessException or IOException)
            {
                throw WorkspaceToolAccessDeniedException.InaccessiblePath(alias);
            }
            catch (ProductTargetAuditBoundsExceededException)
            {
                throw new ProductTargetAuditUnavailableException(alias);
            }
        }

        private static IReadOnlyDictionary<string, ProductTargetFileFingerprint> CaptureFileFingerprints(
            string rootPath,
            IPhysicalFileSystemPathPolicy physicalPathPolicy,
            ProductTargetAuditBudget budget)
        {
            if (string.IsNullOrWhiteSpace(rootPath))
            {
                return CreateFingerprintDictionary(physicalPathPolicy.PathComparer);
            }

            physicalPathPolicy.EnsureSafePath(rootPath);
            var rootIsFile = File.Exists(rootPath);
            if (!rootIsFile && !Directory.Exists(rootPath))
            {
                return CreateFingerprintDictionary(physicalPathPolicy.PathComparer);
            }

            IEnumerable<string> paths = rootIsFile
                ? [rootPath]
                : EnumerateAuditFiles(rootPath, physicalPathPolicy, budget);
            var fingerprints = CreateFingerprintDictionary(physicalPathPolicy.PathComparer);
            foreach (var path in paths)
            {
                physicalPathPolicy.EnsureSafePath(path);
                var key = rootIsFile
                    ? "."
                    : WorkspacePathPolicy.NormalizeRelativePath(
                        Path.GetRelativePath(rootPath, path));
                fingerprints.Add(key, CreateFingerprint(path, budget));
            }

            return fingerprints;
        }

        private static Dictionary<string, ProductTargetFileFingerprint> CreateFingerprintDictionary(
            StringComparer pathComparer)
            => new(pathComparer);

        private static IEnumerable<string> EnumerateAuditFiles(
            string rootPath,
            IPhysicalFileSystemPathPolicy physicalPathPolicy,
            ProductTargetAuditBudget budget)
        {
            var pendingDirectories = new Stack<string>();
            pendingDirectories.Push(rootPath);
            while (pendingDirectories.TryPop(out var directoryPath))
            {
                physicalPathPolicy.EnsureSafePath(directoryPath);
                budget.CountDirectory();
                string[] entryPaths = Directory.EnumerateFileSystemEntries(
                        directoryPath,
                        "*",
                        SearchOption.TopDirectoryOnly)
                    .OrderBy(
                        entryPath => WorkspacePathPolicy.NormalizeRelativePath(
                            Path.GetRelativePath(rootPath, entryPath)),
                        StringComparer.Ordinal)
                    .ToArray();
                var childDirectories = new List<string>();
                foreach (var entryPath in entryPaths)
                {
                    budget.CountEntry();
                    physicalPathPolicy.EnsureSafePath(entryPath);
                    var attributes = File.GetAttributes(entryPath);
                    if ((attributes & FileAttributes.Directory) != 0)
                    {
                        childDirectories.Add(entryPath);
                        continue;
                    }

                    yield return entryPath;
                }

                for (int index = childDirectories.Count - 1; index >= 0; index--)
                {
                    pendingDirectories.Push(childDirectories[index]);
                }
            }
        }

        private static ProductTargetFileFingerprint CreateFingerprint(
            string path,
            ProductTargetAuditBudget budget)
        {
            using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 81920,
                options: FileOptions.SequentialScan);
            var length = stream.Length;
            budget.CountFile(length);
            var lastWriteTimeUtc = File.GetLastWriteTimeUtc(path);
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            var buffer = new byte[81920];
            long remaining = length;
            while (remaining > 0)
            {
                var read = stream.Read(buffer, 0, (int)Math.Min(buffer.Length, remaining));
                if (read == 0)
                {
                    throw new IOException($"File '{path}' changed while its mutation-audit fingerprint was captured.");
                }

                hash.AppendData(buffer, 0, read);
                remaining -= read;
            }

            if (stream.ReadByte() != -1 ||
                stream.Length != length ||
                File.GetLastWriteTimeUtc(path) != lastWriteTimeUtc)
            {
                throw new IOException($"File '{path}' changed while its mutation-audit fingerprint was captured.");
            }

            return new ProductTargetFileFingerprint(
                length,
                lastWriteTimeUtc,
                Convert.ToHexString(hash.GetHashAndReset()));
        }

        private sealed class ProductTargetAuditBudget
        {
            private int entries;
            private int directories;
            private int files;
            private long bytes;

            public void CountEntry()
            {
                entries++;
                EnsureWithinLimit(entries, MaximumAuditEntries);
            }

            public void CountDirectory()
            {
                directories++;
                EnsureWithinLimit(directories, MaximumAuditDirectories);
            }

            public void CountFile(long length)
            {
                if (length < 0 || files >= MaximumAuditFiles || length > MaximumAuditBytes - bytes)
                {
                    throw new ProductTargetAuditBoundsExceededException();
                }

                files++;
                bytes += length;
            }

            private static void EnsureWithinLimit(int value, int maximum)
            {
                if (value > maximum)
                {
                    throw new ProductTargetAuditBoundsExceededException();
                }
            }
        }

        private sealed class ProductTargetAuditBoundsExceededException : Exception
        {
        }

        private sealed class ProductTargetAuditUnavailableException : InvalidOperationException, IAgentToolFailure
        {
            public ProductTargetAuditUnavailableException(string alias)
                : base(
                    $"Governed product target '{NormalizeAlias(alias)}' exceeds the bounded pre-execution mutation audit ({MaximumAuditRoots} roots, {MaximumAuditEntries} entries, {MaximumAuditDirectories} directories, {MaximumAuditFiles} files, or {MaximumAuditBytes / (1024 * 1024)} MiB total). Narrow the grounded product target before retrying; the command was not launched.")
            {
            }

            public string ErrorCode => "ProductTargetAuditUnavailable";

            public string SafeMessage => Message;

            public bool IsSafeToExpose => true;

            public bool CanRetryWithCorrectedInput => true;

            private static string NormalizeAlias(string alias)
                => AgentWorkspaceToolAccessMetadata.NormalizeExternalTargetAlias(alias)
                   ?? "external-target/unresolved";
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
            IPhysicalFileSystemPathPolicy PhysicalPathPolicy,
            IReadOnlyDictionary<string, ProductTargetFileFingerprint> Files)
        {
            public bool HasChanged(ProductTargetAuditBudget budget)
            {
                var afterFiles = CaptureFileFingerprints(RootPath, PhysicalPathPolicy, budget);
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
