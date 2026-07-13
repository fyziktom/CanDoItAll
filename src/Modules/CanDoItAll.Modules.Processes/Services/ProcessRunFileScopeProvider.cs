using System.Security.Cryptography;
using System.Text;
using CanDoItAll.FileTools.FileBrowser;
using CanDoItAll.FileTools.Integration;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Modules.Processes;

internal readonly record struct ProcessRunFileScopeKey(Guid RunId, string RootFingerprint)
{
    private const string Prefix = "run:v1";

    public static ProcessRunFileScopeKey Create(Guid runId, string directoryPath)
    {
        if (runId == Guid.Empty)
        {
            throw new ArgumentException("A process run identifier is required.", nameof(runId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);
        string fingerprint = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(
            directoryPath.Trim().ToUpperInvariant())));
        return new ProcessRunFileScopeKey(runId, fingerprint);
    }

    public FileToolsSemanticScopeId ToScopeId()
        => new($"{Prefix}:{RunId:N}:{RootFingerprint}");

    public static bool TryParse(FileToolsSemanticScopeId scopeId, out ProcessRunFileScopeKey key)
    {
        key = default;
        string[] parts = scopeId.Value.Split(':', StringSplitOptions.TrimEntries);
        if (parts.Length != 4 ||
            !string.Equals(parts[0], "run", StringComparison.Ordinal) ||
            !string.Equals(parts[1], "v1", StringComparison.Ordinal) ||
            !Guid.TryParseExact(parts[2], "N", out Guid runId) ||
            runId == Guid.Empty ||
            !IsSha256(parts[3]))
        {
            return false;
        }

        key = new ProcessRunFileScopeKey(runId, parts[3].ToLowerInvariant());
        return true;
    }

    private static bool IsSha256(string value)
    {
        if (value.Length != 64)
        {
            return false;
        }

        foreach (char character in value)
        {
            if (!Uri.IsHexDigit(character))
            {
                return false;
            }
        }

        return true;
    }
}

internal sealed class ProcessRunFileScopeProvider(
    IProcessRuntimeStateStore stateStore,
    IProcessRuntimeStepAssignmentStore assignmentStore,
    IStorageCatalogService storageCatalog)
    : IProcessRunFileScopeProvider, IFileToolsStorageBindingSource
{
    private static readonly FileToolsBrowseWorkLimits WorkLimits = new(
        maximumReturnedItems: 50,
        maximumInspectedItems: 2_000,
        maximumMetadataProbes: 50,
        maximumConcurrentMetadataProbes: 1,
        maximumDuration: TimeSpan.FromSeconds(5));

    public FileToolsSemanticScopeKind ScopeKind => FileToolsSemanticScopeKind.ProcessRun;

    public async ValueTask<ProcessRunFileScopeSet> ResolveAsync(
        Guid runId,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<ResolvedProcessRunRoot> roots = await ResolveRootsAsync(runId, cancellationToken);
        FileToolsSemanticScope[] scopes = roots.Select(root => root.Scope).ToArray();
        string fingerprint = BuildFingerprint(runId, roots);
        return new ProcessRunFileScopeSet(runId, scopes, fingerprint);
    }

    async ValueTask<IReadOnlyList<FileToolsStorageBinding>> IFileToolsStorageBindingSource.ResolveAsync(
        FileToolsSemanticScope scope,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);
        if (scope.Kind != ScopeKind || !ProcessRunFileScopeKey.TryParse(scope.Id, out ProcessRunFileScopeKey key))
        {
            throw ProviderError(FileBrowserErrorCode.InvalidOperation, "The process-run file scope identifier is invalid.");
        }

        IReadOnlyList<ResolvedProcessRunRoot> roots = await ResolveRootsAsync(key.RunId, cancellationToken);
        ResolvedProcessRunRoot? selected = null;
        foreach (ResolvedProcessRunRoot root in roots)
        {
            if (root.Key == key)
            {
                if (selected is not null)
                {
                    throw ProviderError(
                        FileBrowserErrorCode.CorruptProviderResponse,
                        "The process-run file scope resolves to more than one current root.");
                }

                selected = root;
            }
        }

        if (selected is null)
        {
            throw ProviderError(FileBrowserErrorCode.Conflict, "The process-run file root is no longer current.");
        }

        StorageCatalogRecord storage = await storageCatalog.EnsureBootstrapFileSystemStorageAsync(cancellationToken);
        if (storage.ProviderKind != StorageProviderKind.FileSystem)
        {
            throw ProviderError(
                FileBrowserErrorCode.CorruptProviderResponse,
                "The managed process-run storage catalog is not a filesystem source.");
        }

        return
        [
            new FileToolsStorageBinding(
                storage.Id,
                selected.Scope.DisplayName,
                WorkLimits,
                new FileToolsStorageRoot(selected.Root.DirectoryPath),
                FileToolsHostBrowseCacheMode.Disabled)
        ];
    }

    private async ValueTask<IReadOnlyList<ResolvedProcessRunRoot>> ResolveRootsAsync(
        Guid runId,
        CancellationToken cancellationToken)
    {
        if (runId == Guid.Empty)
        {
            throw ProviderError(FileBrowserErrorCode.InvalidOperation, "The process run identifier is invalid.");
        }

        var typedRunId = new ProcessRunId(runId);
        if (await stateStore.LoadAsync(typedRunId, cancellationToken) is null)
        {
            throw ProviderError(FileBrowserErrorCode.NotFound, "The process run no longer exists.");
        }

        IReadOnlyList<ProcessRuntimeStepAssignment> assignments = await assignmentStore
            .LoadByRunAsync(typedRunId, cancellationToken);
        IReadOnlyList<IReadOnlyDictionary<string, string>> launchVariableSets = assignments
            .Select(assignment => assignment.LaunchVariables)
            .ToArray();
        IReadOnlyList<ProcessRunArtifactRootResolution> roots;
        try
        {
            roots = ProcessRunArtifactRootPolicy.ResolveCurrentRunRoots(runId, launchVariableSets);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            throw ProviderError(FileBrowserErrorCode.InvalidOperation, exception.Message);
        }

        return roots
            .Select(root =>
            {
                ProcessRunFileScopeKey key = ProcessRunFileScopeKey.Create(runId, root.DirectoryPath);
                return new ResolvedProcessRunRoot(
                    key,
                    root,
                    new FileToolsSemanticScope(
                        FileToolsSemanticScopeKind.ProcessRun,
                        key.ToScopeId(),
                        ResolveDisplayName(root)));
            })
            .ToArray();
    }

    private static string BuildFingerprint(Guid runId, IReadOnlyList<ResolvedProcessRunRoot> roots)
    {
        string canonical = string.Join('\n',
            [
                "process-run-files-v1",
                runId.ToString("N"),
                .. roots.Select(root => $"{root.Root.Kind}:{root.Root.DirectoryPath}")
            ]);
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    private static string ResolveDisplayName(ProcessRunArtifactRootResolution root)
    {
        if (root.Kind != ProcessRunArtifactRootKind.ManagedProductOutputRoot)
        {
            return root.Kind == ProcessRunArtifactRootKind.ManagedArtifactRunRoot
                ? "Run artifacts"
                : "Run files";
        }

        string productName = root.DirectoryPath
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .LastOrDefault() ?? string.Empty;
        return string.IsNullOrWhiteSpace(productName)
            ? "Product output"
            : $"Product output · {productName}";
    }

    private static FileBrowserProviderException ProviderError(FileBrowserErrorCode code, string message)
        => new(new FileBrowserError(code, message));

    private sealed record ResolvedProcessRunRoot(
        ProcessRunFileScopeKey Key,
        ProcessRunArtifactRootResolution Root,
        FileToolsSemanticScope Scope);
}
