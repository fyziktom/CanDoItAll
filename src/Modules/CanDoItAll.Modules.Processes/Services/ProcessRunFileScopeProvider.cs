using System.Security.Cryptography;
using System.Text;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Projects;
using CanDoItAll.FileTools.FileBrowser;
using CanDoItAll.FileTools.Integration;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Modules.Processes;

internal readonly record struct ProcessRunFileScopeKey(Guid RunId, string RootFingerprint)
{
    private const string Prefix = "run:v2";

    public static ProcessRunFileScopeKey Create(Guid runId, string directoryPath, string preparationFingerprint)
    {
        if (runId == Guid.Empty)
        {
            throw new ArgumentException("A process run identifier is required.", nameof(runId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(preparationFingerprint);
        string fingerprint = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(
            preparationFingerprint + "\n" + directoryPath.Trim().ToUpperInvariant())));
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
            !string.Equals(parts[1], "v2", StringComparison.Ordinal) ||
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
    IStorageCatalogService storageCatalog,
    IProcessPreparedLaunchStore preparedLaunches,
    ProjectWriteAdmissionService projectAdmissions)
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

    public async ValueTask<FileToolsStorageBinding> ResolveRootAsync(
        Guid runId,
        string directoryPath,
        Guid projectId,
        CancellationToken cancellationToken = default) {
        if (projectId == Guid.Empty || string.IsNullOrWhiteSpace(directoryPath)) {
            throw ProviderError(FileBrowserErrorCode.InvalidOperation, "An exact project and Process file root are required.");
        }
        var roots = await ResolveRootsAsync(runId, cancellationToken, projectId);
        var selected = roots.SingleOrDefault(root => string.Equals(root.Root.DirectoryPath, directoryPath, StringComparison.Ordinal));
        if (selected is null) {
            throw ProviderError(FileBrowserErrorCode.Conflict, "The Process file root does not match the saved run.");
        }
        return await ResolveBindingAsync(selected, cancellationToken);
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

        return [await ResolveBindingAsync(selected, cancellationToken)];
    }

    private async ValueTask<FileToolsStorageBinding> ResolveBindingAsync(
        ResolvedProcessRunRoot selected, CancellationToken cancellationToken) {
        var storage = await storageCatalog.EnsureBootstrapFileSystemStorageAsync(cancellationToken);
        if (storage.Id == Guid.Empty || !storage.IsEnabled || storage.ProviderKind != StorageProviderKind.FileSystem) {
            throw ProviderError(FileBrowserErrorCode.CorruptProviderResponse,
                "The managed Process storage catalog is not a usable filesystem source.");
        }
        return new FileToolsStorageBinding(storage.Id, selected.Scope.DisplayName, WorkLimits,
            new FileToolsStorageRoot(selected.WorkspaceRelativePath), FileToolsHostBrowseCacheMode.Disabled);
    }

    private async ValueTask<IReadOnlyList<ResolvedProcessRunRoot>> ResolveRootsAsync(
        Guid runId,
        CancellationToken cancellationToken,
        Guid? expectedProjectId = null)
    {
        if (runId == Guid.Empty)
        {
            throw ProviderError(FileBrowserErrorCode.InvalidOperation, "The process run identifier is invalid.");
        }

        var typedRunId = new ProcessRunId(runId);
        var state = await stateStore.LoadAsync(typedRunId, cancellationToken);
        if (state is null)
        {
            throw ProviderError(FileBrowserErrorCode.NotFound, "The process run no longer exists.");
        }

        if (state.RunId != typedRunId || state.LaunchAdmissionId is not { } admissionId) {
            throw ProviderError(FileBrowserErrorCode.Forbidden,
                "The Process run has no original file-scope evidence; reconciliation is required.");
        }
        var saved = await preparedLaunches.GetAsync(admissionId, cancellationToken);
        if (saved is null || saved.AcceptedAtUtc is null || saved.Execute is null ||
                saved.Preparation.Authority is not { } authority ||
                authority.DatabaseProfileId != projectAdmissions.DatabaseProfileId ||
                saved.Preparation.AdmissionId != admissionId ||
                saved.Preparation.InitialCommit.Mutation.State.RunId != state.RunId ||
                saved.Preparation.InitialCommit.Mutation.State.RootRunId != state.RootRunId ||
                saved.Preparation.InitialCommit.Mutation.State.PlanId != state.PlanId ||
                saved.Preparation.InitialCommit.Mutation.State.LaunchAdmissionId != admissionId ||
                saved.Preparation.InitialCommit.Mutation.State.ProjectAdmission != state.ProjectAdmission ||
                authority.ProjectAdmission != state.ProjectAdmission ||
                expectedProjectId.HasValue && state.ProjectAdmission?.ProjectId != expectedProjectId) {
            throw ProviderError(FileBrowserErrorCode.Forbidden,
                "The Process file scope does not match its original accepted run, profile and project.");
        }
        authority.Validate();
        if (state.ProjectAdmission is { } project) {
            try {
                await projectAdmissions.RequireCurrentAsync(
                    new(project.DatabaseProfileId, project.ProjectId, project.LifetimeId), cancellationToken);
            } catch (ProjectWriteAdmissionRejectedException) {
                throw ProviderError(FileBrowserErrorCode.Forbidden, "The original Process project lifetime is no longer current.");
            }
        }
        var workspaceScope = WorkspaceScopeDescriptor.Organization(authority.DatabaseProfileId.ToString("N"));

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
                ProcessRunFileScopeKey key = ProcessRunFileScopeKey.Create(runId, root.DirectoryPath, saved.PreparationFingerprint);
                return new ResolvedProcessRunRoot(
                    key,
                    root,
                    ResolveWorkspacePath(workspaceScope, root),
                    new FileToolsSemanticScope(
                        FileToolsSemanticScopeKind.ProcessRun,
                        key.ToScopeId(),
                        ResolveDisplayName(root)));
            })
            .ToArray();
    }

    private static string ResolveWorkspacePath(WorkspaceScopeDescriptor scope, ProcessRunArtifactRootResolution root) {
        (string logicalRoot, string scopedRoot) = root.Kind switch {
            ProcessRunArtifactRootKind.ManagedArtifactRunRoot =>
                (WorkspaceScopeDescriptor.ArtifactManagedRootName, scope.ArtifactRootRelativePath),
            ProcessRunArtifactRootKind.ManagedRunRoot or ProcessRunArtifactRootKind.ManagedProductOutputRoot =>
                (WorkspaceScopeDescriptor.OutputManagedRootName, scope.OutputRootRelativePath),
            _ => throw ProviderError(FileBrowserErrorCode.Forbidden, "The Process file root is outside its managed namespace.")
        };
        if (!root.DirectoryPath.StartsWith(logicalRoot + "/", StringComparison.OrdinalIgnoreCase)) {
            throw ProviderError(FileBrowserErrorCode.Forbidden, "The Process file root does not match its managed namespace.");
        }
        return scopedRoot + root.DirectoryPath[logicalRoot.Length..];
    }

    private static string BuildFingerprint(Guid runId, IReadOnlyList<ResolvedProcessRunRoot> roots)
    {
        string canonical = string.Join('\n',
            [
                "process-run-files-v2",
                runId.ToString("N"),
                .. roots.Select(root => $"{root.Key.RootFingerprint}:{root.WorkspaceRelativePath}")
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
        string WorkspaceRelativePath,
        FileToolsSemanticScope Scope);
}
