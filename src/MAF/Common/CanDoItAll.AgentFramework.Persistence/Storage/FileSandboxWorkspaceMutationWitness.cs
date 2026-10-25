using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.Infrastructure;
using CanDoItAll.Infrastructure.FileSystem;

namespace CanDoItAll.AgentFramework.Persistence;

internal sealed class FileSandboxWorkspaceMutationWitness {
    private static readonly JsonSerializerOptions SerializerOptions = new FileSandboxWorkspaceJsonStore().SerializerOptions;
    private readonly ImmutableArray<FileObservation> files;
    private readonly ImmutableArray<DirectoryObservation> directories;
    private readonly ImmutableArray<string> pendingPublicationTargets;
    private readonly ImmutableHashSet<string> reconciledProjectionPaths;
    private readonly IPhysicalFileSystemPathPolicyFactory pathPolicies;
    private readonly string managedRoot;

    private FileSandboxWorkspaceMutationWitness(ImmutableArray<FileObservation> files, ImmutableArray<DirectoryObservation> directories,
        ImmutableArray<string> pendingPublicationTargets, ImmutableHashSet<string> reconciledProjectionPaths,
        IPhysicalFileSystemPathPolicyFactory pathPolicies, string managedRoot) {
        this.files = files;
        this.directories = directories;
        this.pendingPublicationTargets = pendingPublicationTargets;
        this.reconciledProjectionPaths = reconciledProjectionPaths;
        this.pathPolicies = pathPolicies;
        this.managedRoot = managedRoot;
    }

    internal static Task<FileSandboxWorkspaceMutationWitness> CaptureAsync(FileSandboxWorkspaceStorageLayout layout,
        IEnumerable<string> paths, IEnumerable<string> directories, IEnumerable<string> pendingJournals,
        IPhysicalFileSystemPathPolicyFactory pathPolicies,
        CancellationToken cancellationToken, IEnumerable<string>? pendingPublicationTargets = null) =>
        CaptureCoreAsync(layout, paths, directories, pendingJournals, pathPolicies, cancellationToken, pendingPublicationTargets, []);

    internal static Task<FileSandboxWorkspaceMutationWitness> CaptureAgentDeletionSourceAsync(FileSandboxWorkspaceStorageLayout layout,
        IEnumerable<string> paths, IEnumerable<string> pendingJournals,
        IPhysicalFileSystemPathPolicyFactory pathPolicies, CancellationToken cancellationToken) =>
        CaptureCoreAsync(layout, paths, [layout.ExecutionStorageRoot], pendingJournals, pathPolicies, cancellationToken, null,
            [layout.ExecutionIndexPath, layout.ExecutionChatIndexPath, layout.ExecutionProcessIndexPath, layout.ExecutionUsageIndexPath]);

    private static async Task<FileSandboxWorkspaceMutationWitness> CaptureCoreAsync(FileSandboxWorkspaceStorageLayout layout,
        IEnumerable<string> paths, IEnumerable<string> directories, IEnumerable<string> pendingJournals,
        IPhysicalFileSystemPathPolicyFactory pathPolicies, CancellationToken cancellationToken,
        IEnumerable<string>? pendingPublicationTargets, IEnumerable<string> reconciledProjectionPaths) {
        var policy = pathPolicies.Create(layout.RootPath);
        var projectionPaths = reconciledProjectionPaths.Select(policy.ResolveContainedPath).ToImmutableHashSet(policy.PathComparer);
        var pendingPaths = pendingJournals.Select(policy.ResolveContainedPath).ToHashSet(policy.PathComparer);
        var observedPaths = paths.Select(policy.ResolveContainedPath).Distinct(policy.PathComparer).ToImmutableArray();
        var pendingTargets = observedPaths.Concat(pendingPublicationTargets ?? []).Select(policy.ResolveContainedPath)
            .Distinct(policy.PathComparer).ToImmutableArray();
        RequireNoPendingPublications(pendingTargets, policy, cancellationToken);
        var membership = directories.Select(policy.ResolveContainedPath).Distinct(policy.PathComparer)
            .Order(policy.PathComparer).Select(path => ObserveDirectory(path, projectionPaths, policy, cancellationToken)).ToImmutableArray();
        var observations = ImmutableArray.CreateBuilder<FileObservation>();
        foreach (var path in observedPaths.Concat(membership.SelectMany(item => item.Files)).Distinct(policy.PathComparer)
                .Where(path => !projectionPaths.Contains(path)).Order(policy.PathComparer)) {
            var kind = pendingPaths.Contains(path) ? ObservationKind.PendingRecovery
                : policy.PathComparer.Equals(path, layout.WorkspaceIndexPath) ? ObservationKind.WorkspaceIndex
                : ObservationKind.Payload;
            observations.Add(await ObserveAsync(path, kind, policy, cancellationToken).ConfigureAwait(false));
        }
        return new(observations.OrderBy(item => item.Kind).ToImmutableArray(), membership, pendingTargets, projectionPaths, pathPolicies, layout.RootPath);
    }

    internal async Task RequireUnchangedAsync(CancellationToken cancellationToken) {
        var policy = pathPolicies.Create(managedRoot);
        RequireNoPendingPublications(pendingPublicationTargets, policy, cancellationToken);
        var currentDirectories = directories.Select(original => ObserveDirectory(original.Path, reconciledProjectionPaths, policy, cancellationToken)).ToImmutableArray();
        foreach (var original in files) {
            var current = await ObserveAsync(original.Path, original.Kind, policy, cancellationToken).ConfigureAwait(false);
            if (current != original) {
                if (original.Kind == ObservationKind.PendingRecovery) {
                    throw new SandboxWorkspacePublicationConflictException(SandboxWorkspacePublicationConflictKind.PendingRecoveryChanged);
                }
                if (original.Revision is { } expected && current.Revision is { } actual && expected != actual) {
                    throw new SandboxWorkspaceConcurrencyException(expected, actual);
                }
                throw new SandboxWorkspacePublicationConflictException(SandboxWorkspacePublicationConflictKind.OriginalBytesChanged);
            }
        }
        for (var index = 0; index < directories.Length; index++) {
            var original = directories[index];
            var current = currentDirectories[index];
            if (current.Exists != original.Exists || !current.Files.SequenceEqual(original.Files, policy.PathComparer)) {
                throw new SandboxWorkspacePublicationConflictException(SandboxWorkspacePublicationConflictKind.OriginalBytesChanged);
            }
        }
    }

    private static void RequireNoPendingPublications(ImmutableArray<string> targets, IPhysicalFileSystemPathPolicy policy,
        CancellationToken cancellationToken) {
        foreach (var group in targets.GroupBy(path => Path.GetDirectoryName(path)!, policy.PathComparer)) {
            cancellationToken.ThrowIfCancellationRequested();
            policy.EnsureSafePath(group.Key, allowMissingLeaf: true);
            if (!Directory.Exists(group.Key)) {
                continue;
            }
            var names = group.Select(Path.GetFileName).ToArray();
            foreach (var entry in Directory.EnumerateFileSystemEntries(group.Key)) {
                cancellationToken.ThrowIfCancellationRequested();
                var candidate = Path.GetFileName(entry);
                if (names.Any(target => DurableFileWriter.IsPendingPublicationFileName(candidate, target!, policy.PathComparison))) {
                    policy.EnsureSafePath(entry);
                    throw new SandboxWorkspacePublicationConflictException(SandboxWorkspacePublicationConflictKind.PendingRecoveryChanged);
                }
            }
        }
    }

    private static DirectoryObservation ObserveDirectory(string path, ImmutableHashSet<string> reconciledProjectionPaths,
        IPhysicalFileSystemPathPolicy policy, CancellationToken cancellationToken) {
        policy.EnsureSafePath(path, allowMissingLeaf: true);
        if (!Directory.Exists(path)) {
            return new(path, false, []);
        }
        var pending = new Stack<string>();
        var files = new List<string>();
        pending.Push(path);
        while (pending.TryPop(out var directory)) {
            cancellationToken.ThrowIfCancellationRequested();
            policy.EnsureSafePath(directory);
            foreach (var entry in Directory.EnumerateFileSystemEntries(directory)) {
                cancellationToken.ThrowIfCancellationRequested();
                policy.EnsureSafePath(entry);
                if (Directory.Exists(entry)) {
                    pending.Push(entry);
                } else {
                    var name = Path.GetFileName(entry);
                    var suffixSeparator = name.LastIndexOf('.');
                    var tokenSeparator = suffixSeparator > 0 ? name.LastIndexOf('.', suffixSeparator - 1) : -1;
                    if (tokenSeparator > 1 && DurableFileWriter.IsPendingPublicationFileName(name.AsSpan(),
                            name.AsSpan(1, tokenSeparator - 1), policy.PathComparison)) {
                        throw new SandboxWorkspacePublicationConflictException(SandboxWorkspacePublicationConflictKind.PendingRecoveryChanged);
                    }
                    if (string.Equals(Path.GetExtension(entry), ".json", StringComparison.OrdinalIgnoreCase) &&
                        !reconciledProjectionPaths.Contains(entry)) {
                        files.Add(entry);
                    }
                }
            }
        }
        return new(path, true, files.Order(policy.PathComparer).ToImmutableArray());
    }

    private static async Task<FileObservation> ObserveAsync(string path, ObservationKind kind, IPhysicalFileSystemPathPolicy policy,
        CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        policy.EnsureSafePath(path, allowMissingLeaf: true);
        FileStream stream;
        try {
            stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete, 4096,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
        } catch (FileNotFoundException) {
            return new(path, kind, null, null);
        } catch (DirectoryNotFoundException) {
            return new(path, kind, null, null);
        }
        await using (stream.ConfigureAwait(false)) {
            using var activity = AgentFrameworkTelemetry.ActivitySource.StartActivity("workspace.publication.read");
            activity?.SetTag("publication.observation", kind.ToString());
            try {
                string digest;
                long? revision = null;
                if (kind == ObservationKind.WorkspaceIndex) {
                    using var bytes = new MemoryStream();
                    await stream.CopyToAsync(bytes, cancellationToken).ConfigureAwait(false);
                    var payload = bytes.ToArray();
                    digest = Convert.ToHexString(SHA256.HashData(payload));
                    revision = JsonSerializer.Deserialize<WorkspaceStorageIndex>(payload, SerializerOptions)?.Revision;
                } else {
                    digest = Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false));
                }
                policy.EnsureSafePath(path);
                return new(path, kind, digest, revision);
            } finally {
                activity?.SetTag("payload.bytes", stream.Position);
            }
        }
    }

    private sealed record FileObservation(string Path, ObservationKind Kind, string? Digest, long? Revision);

    private sealed record DirectoryObservation(string Path, bool Exists, ImmutableArray<string> Files);

    private enum ObservationKind {
        PendingRecovery,
        WorkspaceIndex,
        Payload
    }
}
