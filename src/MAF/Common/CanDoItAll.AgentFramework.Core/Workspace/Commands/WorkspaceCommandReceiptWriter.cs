using System.Text.Json;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

internal sealed class WorkspaceCommandReceiptWriter
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly string workspaceRoot;
    private readonly WorkspaceScopeDescriptor workspaceScope;
    private readonly IReadOnlyList<IWorkspaceCommandReceiptLifecycleFactExtractor> lifecycleFactExtractors;

    public WorkspaceCommandReceiptWriter(
        string workspaceRoot,
        WorkspaceScopeDescriptor? workspaceScope = null,
        IEnumerable<IWorkspaceCommandReceiptLifecycleFactExtractor>? lifecycleFactExtractors = null)
    {
        this.workspaceRoot = Path.GetFullPath(workspaceRoot);
        this.workspaceScope = workspaceScope ?? WorkspaceScopeDescriptor.Sandbox;
        this.lifecycleFactExtractors = lifecycleFactExtractors?.ToArray() ?? [];
    }

    public WorkspaceToolReceipt PersistProcessReceipt(
        string toolName,
        string recipeId,
        ToolExecutionDecision decision,
        string workingDirectory,
        IReadOnlyList<string> arguments,
        IReadOnlyList<string> targetPaths,
        bool mutatesWorkspace,
        string message,
        WorkspaceProcessExecutionResult processResult)
    {
        var artifactDirectory = ResolveArtifactDirectory(recipeId, processResult.StartedAtUtc);
        Directory.CreateDirectory(artifactDirectory.FullPath);

        var stdoutRelativePath = WorkspacePathPolicy.NormalizeRelativePath(Path.Combine(artifactDirectory.RelativePath, "stdout.txt"));
        var stderrRelativePath = WorkspacePathPolicy.NormalizeRelativePath(Path.Combine(artifactDirectory.RelativePath, "stderr.txt"));
        var requestRelativePath = WorkspacePathPolicy.NormalizeRelativePath(Path.Combine(artifactDirectory.RelativePath, "request.json"));
        var receiptRelativePath = WorkspacePathPolicy.NormalizeRelativePath(Path.Combine(artifactDirectory.RelativePath, "receipt.json"));

        File.WriteAllText(Path.Combine(workspaceRoot, stdoutRelativePath.Replace('/', Path.DirectorySeparatorChar)), processResult.Stdout ?? string.Empty);
        File.WriteAllText(Path.Combine(workspaceRoot, stderrRelativePath.Replace('/', Path.DirectorySeparatorChar)), processResult.Stderr ?? string.Empty);
        File.WriteAllText(
            Path.Combine(workspaceRoot, requestRelativePath.Replace('/', Path.DirectorySeparatorChar)),
            JsonSerializer.Serialize(
                new
                {
                    toolName,
                    recipeId,
                    decision,
                    workingDirectory,
                    arguments,
                    targetPaths
                },
                SerializerOptions));

        var artifactReferences = new List<WorkspaceArtifactReference>
        {
            new("tool-receipt", receiptRelativePath, $"{recipeId} receipt", "application/json", "Durable process receipt."),
            new("tool-receipt", requestRelativePath, $"{recipeId} request", "application/json", "Captured recipe request payload."),
            new("generated-output", stdoutRelativePath, $"{recipeId} stdout", "text/plain", "Captured stdout preview."),
            new("generated-output", stderrRelativePath, $"{recipeId} stderr", "text/plain", "Captured stderr preview.")
        };

        foreach (var targetPath in targetPaths.Where(item => !string.IsNullOrWhiteSpace(item)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var zone = TryClassifyArtifactZone(targetPath, out var classifiedZone)
                ? classifiedZone
                : "generated-output";
            artifactReferences.Add(new WorkspaceArtifactReference(
                Zone: zone,
                RelativePath: targetPath,
                DisplayName: Path.GetFileName(targetPath),
                ContentType: "application/octet-stream",
                Summary: "Workspace path touched or targeted by the recipe."));
        }

        var receiptPayload = new
        {
            toolName,
            recipeId,
            decision,
            boundary = processResult.Boundary,
            workingDirectory,
            argumentsSummary = BuildArgumentsSummary(arguments),
            processResult.ExitCode,
            processResult.TimedOut,
            processResult.StdoutTruncated,
            processResult.StderrTruncated,
            processResult.FailureMessage,
            processResult.StartedAtUtc,
            processResult.CompletedAtUtc,
            artifactReferences
        };

        File.WriteAllText(
            Path.Combine(workspaceRoot, receiptRelativePath.Replace('/', Path.DirectorySeparatorChar)),
            JsonSerializer.Serialize(receiptPayload, SerializerOptions));

        var outcome = processResult.TimedOut
            ? "TimedOut"
            : processResult.Started
                ? processResult.ExitCode == 0 ? "Succeeded" : "Failed"
                : "Failed";

        var auditedRequestSummary = BuildAuditedProcessRequestSummary(
            toolName,
            arguments,
            targetPaths,
            processResult.Stdout,
            processResult.Stderr);

        return CreateAuditedReceipt(
            operation: toolName,
            mutatesWorkspace: mutatesWorkspace,
            boundary: BuildBoundarySummary(processResult.Boundary),
            outcome: outcome,
            message: message,
            receiptRelativePath: receiptRelativePath,
            targetPaths: targetPaths,
            artifactReferences: artifactReferences,
            startedAtUtc: processResult.StartedAtUtc,
            completedAtUtc: processResult.CompletedAtUtc,
            toolFamily: "workspace-process",
            riskClass: decision.RiskClass,
            approvalMode: decision.ApprovalRequired ? "Required" : "NotRequired",
            isolationGuarantee: BuildBoundarySummary(processResult.Boundary),
            requestSummary: auditedRequestSummary,
            workingDirectory: workingDirectory,
            exitSummary: processResult.Started
                ? $"{outcome} (exit {processResult.ExitCode})"
                : $"Failed ({processResult.FailureMessage})");
    }

    public WorkspaceToolReceipt PersistDescriptorReceipt(
        string toolName,
        string recipeId,
        string riskClass,
        bool approvalRequired,
        string workingDirectory,
        IReadOnlyList<string> arguments,
        IReadOnlyList<string> targetPaths,
        string message,
        ExecutionBoundaryDescriptor boundary,
        DateTimeOffset startedAtUtc,
        DateTimeOffset completedAtUtc,
        object extraPayload)
    {
        var artifactDirectory = ResolveArtifactDirectory(recipeId, startedAtUtc);
        Directory.CreateDirectory(artifactDirectory.FullPath);

        var descriptorRelativePath = WorkspacePathPolicy.NormalizeRelativePath(Path.Combine(artifactDirectory.RelativePath, "descriptor.json"));
        var receiptRelativePath = WorkspacePathPolicy.NormalizeRelativePath(Path.Combine(artifactDirectory.RelativePath, "receipt.json"));

        File.WriteAllText(
            Path.Combine(workspaceRoot, descriptorRelativePath.Replace('/', Path.DirectorySeparatorChar)),
            JsonSerializer.Serialize(extraPayload, SerializerOptions));

        var artifactReferences = new List<WorkspaceArtifactReference>
        {
            new("tool-receipt", receiptRelativePath, $"{recipeId} receipt", "application/json", "Durable launch receipt."),
            new("tool-receipt", descriptorRelativePath, $"{recipeId} descriptor", "application/json", "Reviewed launch descriptor.")
        };

        File.WriteAllText(
            Path.Combine(workspaceRoot, receiptRelativePath.Replace('/', Path.DirectorySeparatorChar)),
            JsonSerializer.Serialize(
                new
                {
                    toolName,
                    recipeId,
                    riskClass,
                    approvalRequired,
                    workingDirectory,
                    argumentsSummary = BuildArgumentsSummary(arguments),
                    targetPaths,
                    boundary,
                    startedAtUtc,
                    completedAtUtc,
                    artifactReferences
                },
                SerializerOptions));

        return CreateAuditedReceipt(
            operation: toolName,
            mutatesWorkspace: false,
            boundary: BuildBoundarySummary(boundary),
            outcome: "Prepared",
            message: message,
            receiptRelativePath: receiptRelativePath,
            targetPaths: targetPaths,
            artifactReferences: artifactReferences,
            startedAtUtc: startedAtUtc,
            completedAtUtc: completedAtUtc,
            toolFamily: "workspace-process",
            riskClass: riskClass,
            approvalMode: approvalRequired ? "Required" : "NotRequired",
            isolationGuarantee: BuildBoundarySummary(boundary),
            requestSummary: BuildArgumentsSummary(arguments),
            workingDirectory: workingDirectory,
            exitSummary: "Prepared");
    }

    public WorkspaceToolReceipt CreateAuditedReceipt(
        string operation,
        bool mutatesWorkspace,
        string boundary,
        string outcome,
        string message,
        string receiptRelativePath,
        IReadOnlyList<string> targetPaths,
        IReadOnlyList<WorkspaceArtifactReference> artifactReferences,
        DateTimeOffset startedAtUtc,
        DateTimeOffset completedAtUtc,
        string toolFamily,
        string riskClass,
        string approvalMode,
        string isolationGuarantee,
        string requestSummary,
        string workingDirectory,
        string exitSummary)
    {
        var receipt = new WorkspaceToolReceipt(
            Operation: operation,
            MutatesWorkspace: mutatesWorkspace,
            Boundary: boundary,
            Outcome: outcome,
            Message: message,
            ReceiptRelativePath: receiptRelativePath,
            TargetPaths: targetPaths,
            ArtifactReferences: artifactReferences,
            StartedAtUtc: startedAtUtc,
            CompletedAtUtc: completedAtUtc)
        {
            ExecutionRunId = WorkspaceExecutionAuditContext.Current?.ExecutionRunId
        };

        WorkspaceExecutionAuditTrailWriter.PersistReceipt(
            workspaceRoot,
            workspaceScope,
            receipt,
            toolFamily,
            operation,
            riskClass,
            approvalMode,
            isolationGuarantee,
            requestSummary,
            string.IsNullOrWhiteSpace(workingDirectory) ? "." : workingDirectory,
            exitSummary);

        return receipt;
    }

    public static string BuildBoundarySummary(ExecutionBoundaryDescriptor boundary)
        => $"{boundary.Mode} via {boundary.HostLabel} (host-enforced: {boundary.IsEnforcedByHost.ToString().ToLowerInvariant()})";

    public static string BuildArgumentsSummary(IReadOnlyList<string> arguments)
        => string.Join(" ", arguments.Select(QuoteArgumentIfNeeded));

    private string BuildAuditedProcessRequestSummary(
        string toolName,
        IReadOnlyList<string> arguments,
        IReadOnlyList<string> targetPaths,
        string? stdout,
        string? stderr)
    {
        var summary = BuildArgumentsSummary(arguments);
        var context = new WorkspaceCommandReceiptLifecycleFactContext(
            toolName,
            arguments,
            targetPaths,
            stdout,
            stderr);
        var lifecycleFacts = lifecycleFactExtractors
            .SelectMany(extractor => extractor.Extract(context))
            .Select(fact => fact.Format())
            .Where(fact => !string.IsNullOrWhiteSpace(fact))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(6)
            .ToArray();

        if (lifecycleFacts.Length == 0)
        {
            return summary;
        }

        return string.IsNullOrWhiteSpace(summary)
            ? string.Join("; ", lifecycleFacts)
            : $"{summary}; {string.Join("; ", lifecycleFacts)}";
    }

    private ArtifactDirectory ResolveArtifactDirectory(string recipeId, DateTimeOffset startedAtUtc)
    {
        var stamp = startedAtUtc.UtcDateTime.ToString("yyyyMMdd-HHmmssfff");
        var safeRecipeId = string.Concat(recipeId.Select(character => char.IsLetterOrDigit(character) ? character : '-')).Trim('-');
        var processRunId = WorkspaceExecutionAuditContext.Current?.ProcessRunId;
        var relativePath = Guid.TryParse(processRunId, out var processRunGuid)
            ? workspaceScope.CombineArtifactPath("process-runs", processRunGuid.ToString("D"), "tool-runs", $"{stamp}-{safeRecipeId}")
            : workspaceScope.CombineArtifactPath("tool-runs", startedAtUtc.UtcDateTime.ToString("yyyyMMdd"), $"{stamp}-{safeRecipeId}");
        var fullPath = Path.GetFullPath(Path.Combine(workspaceRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        return new ArtifactDirectory(fullPath, relativePath);
    }

    private bool TryClassifyArtifactZone(string targetPath, out string zone)
    {
        var normalized = WorkspacePathPolicy.NormalizeRelativePath(targetPath);
        if (IsWithinScopedRoot(normalized, workspaceScope.CombineArtifactPath("process-runs")) ||
            IsWithinScopedRoot(normalized, workspaceScope.CombineArtifactPath("tool-runs")))
        {
            zone = "tool-receipt";
            return true;
        }

        if (IsWithinScopedRoot(normalized, workspaceScope.CombineArtifactPath("converted-documents")))
        {
            zone = "converted-document";
            return true;
        }

        if (IsWithinScopedRoot(normalized, workspaceScope.ArtifactRootRelativePath)
            || IsWithinScopedRoot(normalized, workspaceScope.OutputRootRelativePath))
        {
            zone = "generated-output";
            return true;
        }

        zone = string.Empty;
        return false;
    }

    private static string QuoteArgumentIfNeeded(string argument)
    {
        return argument.Contains(' ', StringComparison.Ordinal)
            ? $"\"{argument}\""
            : argument;
    }

    private sealed record ArtifactDirectory(string FullPath, string RelativePath);

    private static bool IsWithinScopedRoot(string relativePath, string rootRelativePath)
    {
        return string.Equals(relativePath, rootRelativePath, StringComparison.OrdinalIgnoreCase)
               || relativePath.StartsWith(rootRelativePath + "/", StringComparison.OrdinalIgnoreCase);
    }
}
