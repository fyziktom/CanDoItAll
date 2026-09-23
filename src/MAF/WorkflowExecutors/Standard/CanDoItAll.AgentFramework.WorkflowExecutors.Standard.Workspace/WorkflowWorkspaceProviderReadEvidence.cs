using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.FileSystem;

namespace CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Workspace;

internal enum WorkflowWorkspaceReadPathOrigin { Workspace, RegisteredAlias, ExplicitAbsolute }

internal sealed record WorkflowWorkspaceReadScope(string WorkspaceRoot, WorkspaceScopeDescriptor Scope,
    PhysicalFileSystemCaseSensitivity CaseSensitivity);

internal sealed record WorkflowWorkspaceReadTarget(string FullPath, string ReadRoot, WorkflowWorkspaceReadPathOrigin Origin,
    bool AllowMissing, WorkflowSourceFileContentIdentity? ContentIdentity = null);

internal sealed record WorkflowWorkspaceReadPart(WorkflowWorkspaceReadScope? Scope, WorkflowWorkspaceReadTarget? Target);

public static class WorkflowWorkspaceProviderReadEvidence {
    public static WorkflowDisclosureOwnerId Owner { get; } = new("workflow.workspace-read");
    internal const int SchemaVersion = 1;

    public static bool RequiresEvidence(WorkflowNode node) => node.Settings.ExecutorId == WorkflowExecutorIds.SourceIngestion ||
        node.Settings.ExecutorId == WorkflowExecutorIds.StorageFile && RequiresFileEvidence(
            WorkflowExecutorJson.Deserialize<WorkflowStorageFileExecutorSettings>(string.IsNullOrWhiteSpace(node.Settings.ExecutorSettingsJson)
                ? BuiltInWorkflowExecutorDescriptors.StorageFile.DefaultSettingsJson : node.Settings.ExecutorSettingsJson));

    internal static bool RequiresFileEvidence(WorkflowStorageFileExecutorSettings settings) => settings.Operation is
        WorkflowStorageFileOperation.List or WorkflowStorageFileOperation.ListDirectory or WorkflowStorageFileOperation.Tree or
        WorkflowStorageFileOperation.Exists or WorkflowStorageFileOperation.Stat or WorkflowStorageFileOperation.ReadText or
        WorkflowStorageFileOperation.Hash or WorkflowStorageFileOperation.SearchText or WorkflowStorageFileOperation.DiffText ||
        settings.Operation == WorkflowStorageFileOperation.Delete && settings.DryRun;

    public static async ValueTask RequireCurrentAsync(WorkflowCompletedNodeRead read, WorkflowNode node,
        WorkspaceExecutionScope currentScope, IPhysicalFileSystemPathPolicyFactory physicalPolicies,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(currentScope);
        if (!RequiresEvidence(node) || read.Evidence.Count == 0) {
            throw Denied();
        }
        var parts = read.Evidence.Select(item => {
            if (item.Owner != Owner || item.SchemaVersion != SchemaVersion || item.Occurrence != read.Proof.Occurrence ||
                    item.NodeId != node.Id || item.VersionId != read.Proof.VersionId) {
                throw Denied();
            }
            return JsonSerializer.Deserialize<WorkflowWorkspaceReadPart>(item.PayloadJson,
                WorkflowProviderDisclosureContent.JsonOptions) ?? throw Denied();
        }).ToArray();
        var original = parts[0].Scope ?? throw Denied();
        var workspace = physicalPolicies.Create(currentScope.WorkspaceRoot);
        if (parts[0].Target is not null || original.Scope != currentScope.Scope || !Enum.IsDefined(original.CaseSensitivity) ||
                string.IsNullOrWhiteSpace(original.WorkspaceRoot) || !Path.IsPathFullyQualified(original.WorkspaceRoot) ||
                !workspace.PathComparer.Equals(original.WorkspaceRoot, currentScope.WorkspaceRoot) ||
                original.CaseSensitivity != currentScope.RootCaseSensitivity ||
                node.Settings.ExecutorId == WorkflowExecutorIds.StorageFile && parts.Length == 1) {
            throw Denied();
        }
        var allowsAbsolute = node.Settings.ExecutorId == WorkflowExecutorIds.SourceIngestion &&
            WorkflowExecutorJson.Deserialize<WorkflowSourceIngestionExecutorSettings>(string.IsNullOrWhiteSpace(node.Settings.ExecutorSettingsJson)
                ? BuiltInWorkflowExecutorDescriptors.SourceIngestion.DefaultSettingsJson : node.Settings.ExecutorSettingsJson).AllowAbsoluteInputPaths;
        foreach (var part in parts.Skip(1)) {
            cancellationToken.ThrowIfCancellationRequested();
            var target = part.Target ?? throw Denied();
            if (part.Scope is not null || !Enum.IsDefined(target.Origin) ||
                    string.IsNullOrWhiteSpace(target.ReadRoot) || string.IsNullOrWhiteSpace(target.FullPath) ||
                    !Path.IsPathFullyQualified(target.ReadRoot) || !Path.IsPathFullyQualified(target.FullPath) ||
                    target.Origin == WorkflowWorkspaceReadPathOrigin.Workspace && !workspace.IsWithinRoot(target.ReadRoot) ||
                    target.Origin == WorkflowWorkspaceReadPathOrigin.ExplicitAbsolute && !allowsAbsolute ||
                    node.Settings.ExecutorId == WorkflowExecutorIds.SourceIngestion && target.ContentIdentity is null) {
                throw Denied();
            }
            var root = physicalPolicies.Create(target.ReadRoot);
            root.EnsureSafePath(target.FullPath, target.AllowMissing);
            if (target.ContentIdentity is { } identity) {
                if (node.Settings.ExecutorId != WorkflowExecutorIds.SourceIngestion || target.AllowMissing || identity.Length < 0 ||
                        identity.LastWriteTimeUtc.Kind != DateTimeKind.Utc || string.IsNullOrWhiteSpace(identity.Sha256) || identity.Sha256.Length != 64 ||
                        identity.Sha256.Any(character => !Uri.IsHexDigit(character))) {
                    throw Denied();
                }
                await new WorkflowSourceFileContentIdentityResolver().EnsureUnchangedAsync(
                    new(target.FullPath, "retained Workflow source", Path.GetFileName(target.FullPath)), identity, cancellationToken);
            }
        }
    }

    private static InvalidOperationException Denied() => new(
        "The retained Workflow file read lacks its original workspace, physical target or supported current path authority.");
}

internal sealed class WorkflowWorkspaceReadCapture {
    private readonly WorkflowNodeInvocationBinding invocation;
    private readonly WorkspaceExecutionScope scope;
    private readonly List<WorkflowWorkspaceReadTarget> targets = [];
    private readonly Dictionary<string, WorkspaceResolvedPath> requested = new(StringComparer.Ordinal);

    private WorkflowWorkspaceReadCapture(WorkflowNodeInvocationBinding invocation, WorkspaceExecutionScope scope) {
        this.invocation = invocation;
        this.scope = scope;
    }

    internal static WorkflowWorkspaceReadCapture? Begin(WorkflowExecutorExecutionContext context, WorkflowNodeInput input,
        Func<WorkspaceExecutionScope> readScope) {
        var actual = WorkflowExecutorExecutionAuditScope.CurrentInvocation;
        if (context.ExecutionOccurrence is null || actual?.CompilerVersion == WorkflowProviderDisclosureProtocol.Legacy) {
            return null;
        }
        if (actual is null || actual.CompilerVersion != WorkflowProviderDisclosureProtocol.Current ||
                actual.WorkflowId != context.Definition.Id || actual.VersionId != context.Definition.VersionId ||
                actual.NodeId != context.Node.Id || actual.Occurrence != context.ExecutionOccurrence ||
                actual.InputHash != WorkflowExecutionContentHash.Compute(input.PayloadJson)) {
            throw new InvalidOperationException("The protected Workflow file read has no actual immutable invocation binding.");
        }
        var scope = readScope();
        if (!Path.IsPathFullyQualified(scope.WorkspaceRoot) || scope.Scope is null) {
            throw new InvalidOperationException("The Workflow file owner did not supply its actual immutable workspace scope.");
        }
        return new(actual, scope);
    }

    internal void RequireSameScope(WorkspaceExecutionScope actual) {
        if (!scope.SharesIdentityWith(actual) || scope.RootCaseSensitivity != actual.RootCaseSensitivity) {
            throw new InvalidOperationException("Workflow file and path owners belong to different original workspace scopes.");
        }
    }

    internal static string RequestedPath(WorkflowStorageFileExecutorSettings settings) {
        var path = settings.Operation is WorkflowStorageFileOperation.List or WorkflowStorageFileOperation.Tree
            ? WorkspaceFileListRequest.Normalize(settings.Path, settings.SearchPattern).RelativePath : settings.Path;
        return string.IsNullOrWhiteSpace(path) ? "." : path;
    }

    internal void CapturePath(IWorkspacePathResolutionService paths, string value, bool allowMissing) {
        RequireSameScope(paths.ExecutionScope);
        var resolved = paths.ResolvePath(value);
        if (requested.TryGetValue(value, out var original) && original != resolved) {
            throw new InvalidOperationException("The original Workflow read path changed during the owner operation.");
        }
        requested[value] = resolved;
        Add(new(resolved.FullPath, resolved.FullPath, resolved.IsWorkspacePath
            ? WorkflowWorkspaceReadPathOrigin.Workspace : WorkflowWorkspaceReadPathOrigin.RegisteredAlias, allowMissing));
    }

    internal void RevalidatePaths(IWorkspacePathResolutionService paths) {
        RequireSameScope(paths.ExecutionScope);
        foreach (var (value, original) in requested) {
            if (paths.ResolvePath(value) != original) {
                throw new InvalidOperationException("The original Workflow read path changed during the owner operation.");
            }
        }
    }

    internal void CaptureFileResult(IWorkspacePathResolutionService paths, object result, WorkflowStorageFileExecutorSettings settings) {
        switch (result) {
            case WorkspaceFileListResult list:
                CaptureSelection(list.ReadSelection, list.Entries.Count, RequestedPath(settings));
                break;
            case WorkspaceTextSearchResult search:
                CaptureSelection(search.ReadSelection, search.Matches.Count, RequestedPath(settings));
                break;
            case WorkspaceTextFileReadResult:
                CapturePath(paths, settings.Path, allowMissing: false);
                break;
            case WorkspacePathStatResult stat:
                CapturePath(paths, settings.Path, allowMissing: !stat.Exists);
                break;
            case WorkspacePathHashResult:
                CapturePath(paths, settings.Path, allowMissing: false);
                break;
            case WorkspaceTextDiffResult:
                CapturePath(paths, settings.Path, allowMissing: false);
                CapturePath(paths, settings.DestinationPath, allowMissing: false);
                break;
            default:
                if (settings.Operation != WorkflowStorageFileOperation.Delete || !settings.DryRun) {
                    throw new InvalidOperationException("The Workflow file owner returned an unsupported protected read result.");
                }
                break;
        }
    }

    private void CaptureSelection(WorkspaceFileReadSelection? selection, int resultCount, string requestedPath) {
        var key = string.IsNullOrWhiteSpace(requestedPath) ? "." : requestedPath;
        if (selection is null || selection.GetSelectedPaths().IsDefault || selection.Count != resultCount ||
                !requested.TryGetValue(key, out var original) || selection.GetRootPath() != original.FullPath ||
                selection.IsWorkspacePath != original.IsWorkspacePath) {
            throw new InvalidOperationException("The Workflow file owner did not retain its exact selected physical targets.");
        }
        var root = selection.GetRootPath();
        var origin = selection.IsWorkspacePath ? WorkflowWorkspaceReadPathOrigin.Workspace : WorkflowWorkspaceReadPathOrigin.RegisteredAlias;
        Add(new(root, root, origin, false));
        foreach (var path in selection.GetSelectedPaths()) {
            if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path)) {
                throw new InvalidOperationException("The Workflow file owner returned an invalid selected physical target.");
            }
            Add(new(path, root, origin, false));
        }
    }

    internal void CaptureIngested(WorkflowSourceIngestionFile file, WorkflowSourceFileContentIdentity identity) {
        if (file.ReadRoot is null) {
            throw new InvalidOperationException("The ingested Workflow file has no original physical read root.");
        }
        Add(new(file.FullPath, file.ReadRoot, file.Origin, false, identity));
    }

    private void Add(WorkflowWorkspaceReadTarget target) {
        if (!targets.Contains(target)) {
            targets.Add(target);
        }
    }

    internal IReadOnlyList<WorkflowProviderReadEvidence> Complete() {
        var evidence = new List<WorkflowProviderReadEvidence> {
            Part(new(new(scope.WorkspaceRoot, scope.Scope, scope.RootCaseSensitivity), null))
        };
        evidence.AddRange(targets.Select(target => Part(new(null, target))));
        return evidence;
    }

    private WorkflowProviderReadEvidence Part(WorkflowWorkspaceReadPart part) => new(WorkflowWorkspaceProviderReadEvidence.Owner,
        WorkflowWorkspaceProviderReadEvidence.SchemaVersion, invocation.Occurrence!, invocation.VersionId, invocation.NodeId,
        JsonSerializer.Serialize(part, WorkflowProviderDisclosureContent.JsonOptions));
}
