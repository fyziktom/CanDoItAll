using System.Text.Json;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

internal sealed class WorkspaceFileReceiptWriter
{
    private const string BoundaryDescription = "Workspace file service with support for mapped external-target/<drive>/... aliases. No host process execution.";

    private static readonly JsonSerializerOptions ReceiptSerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly string workspaceRoot;
    private readonly WorkspaceScopeDescriptor workspaceScope;

    public WorkspaceFileReceiptWriter(string workspaceRoot, WorkspaceScopeDescriptor? workspaceScope = null)
    {
        this.workspaceRoot = Path.GetFullPath(workspaceRoot);
        this.workspaceScope = workspaceScope ?? WorkspaceScopeDescriptor.Sandbox;
    }

    public WorkspaceToolReceipt CreateReceipt(
        string operation,
        bool mutatesWorkspace,
        string outcome,
        string message,
        string receiptRelativePath,
        IReadOnlyList<string> targetPaths,
        IReadOnlyList<WorkspaceArtifactReference> artifactReferences,
        DateTimeOffset startedAtUtc)
    {
        var receipt = new WorkspaceToolReceipt(
            Operation: operation,
            MutatesWorkspace: mutatesWorkspace,
            Boundary: BoundaryDescription,
            Outcome: outcome,
            Message: message,
            ReceiptRelativePath: receiptRelativePath,
            TargetPaths: targetPaths,
            ArtifactReferences: artifactReferences,
            StartedAtUtc: startedAtUtc,
            CompletedAtUtc: DateTimeOffset.UtcNow)
        {
            ExecutionRunId = WorkspaceExecutionAuditContext.Current?.ExecutionRunId
        };

        WorkspaceExecutionAuditTrailWriter.PersistReceipt(
            workspaceRoot,
            workspaceScope,
            receipt,
            toolFamily: "workspace-file",
            toolName: operation,
            riskClass: mutatesWorkspace ? "MutatingWorkspace" : "ReadOnlyWorkspace",
            approvalMode: "NotRequired",
            isolationGuarantee: BoundaryDescription,
            requestSummary: string.Join(", ", targetPaths.Where(item => !string.IsNullOrWhiteSpace(item))),
            workingDirectory: ".",
            exitSummary: $"{outcome}: {message}");

        return receipt;
    }

    public WorkspaceToolReceipt WriteMutationReceipt(
        string operation,
        string message,
        IReadOnlyList<string> targetPaths,
        IReadOnlyList<WorkspaceArtifactReference> targetArtifacts,
        DateTimeOffset startedAtUtc)
    {
        var receiptRelativePath = BuildReceiptRelativePath(operation, targetPaths);
        var receiptArtifact = new WorkspaceArtifactReference(
            Zone: "tool-receipt",
            RelativePath: receiptRelativePath,
            DisplayName: Path.GetFileName(receiptRelativePath),
            ContentType: "application/json",
            Summary: $"{operation} receipt");
        var artifactReferences = targetArtifacts
            .Concat([receiptArtifact])
            .ToList();
        var receipt = CreateReceipt(
            operation,
            mutatesWorkspace: true,
            outcome: "Succeeded",
            message: message,
            receiptRelativePath: receiptRelativePath,
            targetPaths: targetPaths,
            artifactReferences: artifactReferences,
            startedAtUtc: startedAtUtc);

        var receiptFullPath = Path.Combine(workspaceRoot, receiptRelativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(receiptFullPath)!);
        File.WriteAllText(receiptFullPath, JsonSerializer.Serialize(receipt, ReceiptSerializerOptions));
        return receipt;
    }

    public IReadOnlyList<WorkspaceArtifactReference> BuildTargetArtifactReferences(IEnumerable<string> targetPaths, string operation)
    {
        var references = new List<WorkspaceArtifactReference>();
        foreach (var targetPath in targetPaths.Where(path => !string.IsNullOrWhiteSpace(path) && path != ".").Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!TryClassifyArtifactZone(targetPath, out var zone))
            {
                continue;
            }

            references.Add(new WorkspaceArtifactReference(
                Zone: zone,
                RelativePath: targetPath,
                DisplayName: Path.GetFileName(targetPath),
                ContentType: GuessContentType(targetPath),
                Summary: $"{operation} target"));
        }

        return references;
    }

    private string BuildReceiptRelativePath(string operation, IReadOnlyList<string> targetPaths)
    {
        var dateSegment = DateTime.UtcNow.ToString("yyyyMMdd");
        var targetSegment = targetPaths.Count == 0
            ? "workspace"
            : string.Join("-", targetPaths.Take(2).Select(Slugify));
        var fileName = $"{DateTime.UtcNow:HHmmssfff}-{Slugify(operation)}-{targetSegment}.json";
        return workspaceScope.CombineArtifactPath("tool-receipts", dateSegment, fileName);
    }

    private static string Slugify(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "item";
        }

        var characters = value
            .Trim()
            .Select(character => char.IsLetterOrDigit(character) ? char.ToLowerInvariant(character) : '-')
            .ToArray();
        var collapsed = new string(characters)
            .Trim('-');
        while (collapsed.Contains("--", StringComparison.Ordinal))
        {
            collapsed = collapsed.Replace("--", "-", StringComparison.Ordinal);
        }

        return string.IsNullOrWhiteSpace(collapsed) ? "item" : collapsed;
    }

    private bool TryClassifyArtifactZone(string relativePath, out string zone)
    {
        var normalized = WorkspacePathPolicy.NormalizeRelativePath(relativePath);
        if (IsWithinScopedRoot(normalized, workspaceScope.CombineArtifactPath("converted-documents")))
        {
            zone = "converted-document";
            return true;
        }

        if (IsWithinScopedRoot(normalized, workspaceScope.CombineArtifactPath("process-runs"))
            || IsWithinScopedRoot(normalized, workspaceScope.CombineArtifactPath("tool-receipts")))
        {
            zone = "tool-receipt";
            return true;
        }

        if (IsWithinScopedRoot(normalized, workspaceScope.ArtifactRootRelativePath))
        {
            zone = "generated-output";
            return true;
        }

        zone = "generated-output";
        return true;
    }

    private static string GuessContentType(string relativePath)
    {
        return Path.GetExtension(relativePath).ToLowerInvariant() switch
        {
            ".cs" => "text/plain",
            ".csproj" => "text/xml",
            ".json" => "application/json",
            ".md" => "text/markdown",
            ".txt" => "text/plain",
            ".xml" => "text/xml",
            _ => "application/octet-stream"
        };
    }

    private static bool IsWithinScopedRoot(string relativePath, string rootRelativePath)
    {
        return string.Equals(relativePath, rootRelativePath, StringComparison.OrdinalIgnoreCase)
               || relativePath.StartsWith(rootRelativePath + "/", StringComparison.OrdinalIgnoreCase);
    }
}
