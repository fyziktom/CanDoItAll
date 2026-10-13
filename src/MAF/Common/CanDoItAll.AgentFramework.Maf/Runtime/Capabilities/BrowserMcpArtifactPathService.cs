using System.Security.Cryptography;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Maf;

internal static class BrowserMcpArtifactPathService {
    private const string ArtifactsRoot = "artifacts";
    private const string ScopedArtifactsRoot = "artifacts/scopes";
    private const string PlaywrightMcpRoot = ".playwright-mcp";
    private const int MaximumArtifactBytes = 32 * 1024 * 1024;

    public static WorkspaceScopeDescriptor ResolveArtifactScope(WorkspaceScopeDescriptor contextWorkspaceScope) =>
        WorkspaceExecutionAuditContext.Current?.ExecutionWorkspaceScope ?? contextWorkspaceScope;

    public static void EnsureWritableArtifactDirectories(string workspaceRoot, WorkspaceScopeDescriptor scope, string? fileName) {
        if (ResolveSource(workspaceRoot, scope, fileName) is { } source) {
            Directory.CreateDirectory(Path.GetDirectoryName(source)!);
        }
    }

    public static BrowserMcpArtifactObservation ObserveBeforeInvocation(string workspaceRoot,
        WorkspaceScopeDescriptor scope, string? fileName) {
        var source = ResolveSource(workspaceRoot, scope, fileName);
        var bytes = source is null ? null : ReadBounded(source);
        return new(source, scope, bytes is null ? null : Convert.ToHexString(SHA256.HashData(bytes)));
    }

    public static BrowserMcpArtifactImportResult TryImportAfterInvocation(string workspaceRoot,
        WorkspaceScopeDescriptor scope, string? fileName, string? processRunId, BrowserMcpArtifactObservation before) {
        var source = ResolveSource(workspaceRoot, scope, fileName);
        if (source is null || source != before.SourcePath || scope != before.Scope || ReadBounded(source) is not { Length: > 0 } bytes) {
            return new([]);
        }
        var digest = Convert.ToHexString(SHA256.HashData(bytes));
        if (digest == before.ContentDigest) {
            return new([]);
        }
        var hasProcessRunId = Guid.TryParse(processRunId, out var parsedProcessRunId);
        var runRoot = string.IsNullOrWhiteSpace(processRunId) ? "browser" :
            hasProcessRunId ? $"process-runs/{parsedProcessRunId:D}/browser" :
            IsSinglePathSegment(processRunId) ? $"process-runs/{processRunId}/browser" :
            throw new InvalidDataException("The browser artifact run identity is invalid.");
        var artifactSuffix = $"{runRoot}/{Guid.NewGuid():N}/{Path.GetFileName(source)}";
        var relative = $"{scope.ArtifactRootRelativePath}/{artifactSuffix}";
        var destination = ResolveWithinRoot(workspaceRoot, relative)
            ?? throw new InvalidDataException("The browser artifact destination is outside the workspace.");
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        destination = ResolveWithinRoot(workspaceRoot, relative)
            ?? throw new InvalidDataException("The browser artifact destination changed during import.");
        using (var stream = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.Read,
            bufferSize: 81920, FileOptions.WriteThrough)) {
            stream.Write(bytes);
            stream.Flush(flushToDisk: true);
        }
        return new([relative]) {
            ToolReferences = hasProcessRunId
                ? [$"{ArtifactsRoot}/{artifactSuffix}"]
                : [relative]
        };
    }

    private static byte[]? ReadBounded(string path) {
        if (!File.Exists(path)) {
            return null;
        }
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length > MaximumArtifactBytes) {
            throw new InvalidDataException("The browser artifact exceeds the bounded import size.");
        }
        var bytes = new byte[checked((int)stream.Length)];
        stream.ReadExactly(bytes);
        if (stream.ReadByte() != -1) {
            throw new IOException("The browser artifact changed while it was being read.");
        }
        return bytes;
    }

    private static string? ResolveSource(string workspaceRoot, WorkspaceScopeDescriptor scope, string? fileName) {
        if (string.IsNullOrWhiteSpace(fileName)) {
            return null;
        }
        var relative = WorkspaceScopeDescriptor.NormalizeRelativePath(fileName);
        if ((!Under(relative, ArtifactsRoot) && !Under(relative, PlaywrightMcpRoot)) ||
            Under(relative, ScopedArtifactsRoot) && !Under(relative, scope.ArtifactRootRelativePath)) {
            return null;
        }
        return ResolveWithinRoot(workspaceRoot, relative);
    }

    private static string? ResolveWithinRoot(string workspaceRoot, string relative) {
        if (Path.IsPathRooted(relative) || relative.Split('/').Any(segment => segment is ".." or "." || segment.Contains(':'))) {
            return null;
        }
        var root = Path.GetFullPath(workspaceRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var path = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (!path.StartsWith(root + Path.DirectorySeparatorChar, comparison)) {
            return null;
        }
        for (var part = path; part is not null && !part.Equals(root, comparison); part = Path.GetDirectoryName(part)) {
            if ((File.Exists(part) || Directory.Exists(part)) && (File.GetAttributes(part) & FileAttributes.ReparsePoint) != 0) {
                return null;
            }
        }
        return path;
    }

    private static bool Under(string path, string root) => path.StartsWith(root.TrimEnd('/') + "/", StringComparison.OrdinalIgnoreCase);
    private static bool IsSinglePathSegment(string value) => value is not "." and not ".." &&
        value.IndexOfAny(['/', '\\', ':']) < 0 && value.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;
}

internal sealed record BrowserMcpArtifactObservation(string? SourcePath, WorkspaceScopeDescriptor Scope, string? ContentDigest);

internal sealed record BrowserMcpArtifactImportResult(IReadOnlyList<string> ImportedRelativePaths, string? Failure = null) {
    public bool Imported => ImportedRelativePaths.Count > 0;
    public IReadOnlyList<string> ToolReferences { get; init; } = ImportedRelativePaths;
}
