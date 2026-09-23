using System.Collections.Immutable;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Tooling;
using CanDoItAll.Infrastructure.FileSystem;
using Microsoft.Extensions.AI;

namespace CanDoItAll.AgentFramework.Maf;

internal sealed partial class WorkspaceToolResultDisclosure {
    internal static bool IsCollectionTool(string toolName) => toolName is
        ToolContractCatalog.WorkspaceListDirectory or ToolContractCatalog.WorkspaceListFiles or ToolContractCatalog.WorkspaceSearch;

    internal static AIFunction CreateCollectionTool(Delegate method, string name, string description) {
        if (!IsCollectionTool(name)) {
            throw new ArgumentException("The function is not a supported workspace collection read.", nameof(name));
        }
        var serializerOptions = AIJsonUtilities.DefaultOptions;
        return AIFunctionFactory.Create(method, new AIFunctionFactoryOptions {
            Name = name,
            Description = description,
            SerializerOptions = serializerOptions,
            MarshalResult = (result, type, _) => {
                var serialized = JsonSerializer.SerializeToElement(result, type ?? throw new InvalidDataException(
                    "The workspace collection function has no declared result type."), serializerOptions);
                CollectionCapture.Record(name, result, serialized);
                return ValueTask.FromResult<object?>(serialized);
            }
        });
    }

    private WorkspaceToolResultSelection? CaptureCollection(Capture capture, CollectionCapture? collection,
        JsonElement serialized, ref WorkspaceToolResultEvidenceState state) {
        var observed = collection?.Result;
        var original = capture.Paths.SingleOrDefault(item => item.RequestPath == capture.CollectionRequestPath);
        if (observed is null || original is null || observed.SerializedResult != serialized.GetRawText() ||
                observed.RootPath != original.RelativePath || observed.DisplayPaths.IsDefault) {
            state = WorkspaceToolResultEvidenceState.UnsupportedResult;
            return null;
        }
        var selected = observed.Selection;
        if (observed.Succeeded
                ? selected is null || selected.Count != observed.DisplayPaths.Length ||
                  selected.GetRootPath() != original.FullPath || selected.IsWorkspacePath != original.IsWorkspacePath
                : selected is not null || !observed.DisplayPaths.IsEmpty) {
            state = WorkspaceToolResultEvidenceState.UnsupportedResult;
            return null;
        }
        var fullPaths = selected?.GetSelectedPaths() ?? [];
        if (fullPaths.Any(value => string.IsNullOrWhiteSpace(value) || !Path.IsPathFullyQualified(value))) {
            state = WorkspaceToolResultEvidenceState.UnsupportedPath;
            return null;
        }
        if (capture.Paths.Count + fullPaths.Length > WorkspaceToolResultEvidence.MaximumPaths ||
                fullPaths.Any(value => value.Length > WorkspaceToolResultEvidence.MaximumPathCharacters)) {
            state = WorkspaceToolResultEvidenceState.PathLimitExceeded;
            return null;
        }
        var result = new WorkspaceToolResultSelection(original, observed.Succeeded, fullPaths);
        if (fullPaths.Any(value => !IsSelectedPathInScope(original, value, capture.ExecutionWorkspaceScope))) {
            state = WorkspaceToolResultEvidenceState.UncapturedWorkspaceScope;
        }
        try {
            RequireCollectionCurrent(result, capture.Guard, capture.ExecutionWorkspaceScope);
        } catch (AgentToolAdmissionException) {
            if (state == WorkspaceToolResultEvidenceState.Complete) {
                state = WorkspaceToolResultEvidenceState.PathChangedDuringOperation;
            }
        }
        return result;
    }

    private void RequireCollectionCurrent(WorkspaceToolResultSelection? selection, WorkspaceRuntimeFileAccessGuard guard,
        WorkspaceScopeDescriptor? executionScope) {
        if (selection is null) {
            return;
        }
        try {
            var current = Resolve(selection.Root.RequestPath, guard, executionScope);
            var physical = pathPolicyFactory.Create(selection.Root.FullPath);
            if (selection.Root.IsWorkspacePath != current.IsWorkspacePath ||
                    selection.Root.RelativePath != current.RelativePath ||
                    !physical.PathComparer.Equals(selection.Root.FullPath, current.FullPath)) {
                throw Denied();
            }
            physical.EnsureSafePath(selection.Root.FullPath, allowMissingLeaf: !selection.RequiresExistingRoot);
            foreach (var fullPath in selection.FullPaths) {
                if (!IsSelectedPathInScope(selection.Root, fullPath, executionScope)) {
                    throw Denied();
                }
                physical.EnsureSafePath(fullPath, allowMissingLeaf: false);
            }
        } catch (Exception exception) when (exception is WorkspacePathResolutionException or
                WorkspaceToolAccessDeniedException or PhysicalPathValidationException) {
            throw Denied();
        }
    }

    private bool IsSelectedPathInScope(WorkspaceToolResultPath original, string fullPath,
        WorkspaceScopeDescriptor? executionScope) {
        if (!original.IsWorkspacePath) {
            return true;
        }
        var relative = Path.GetRelativePath(workspace.Scope.WorkspaceRoot, fullPath).Replace(Path.DirectorySeparatorChar, '/');
        return IsWithinCapturedScope(relative, workspace.Scope.Scope) || IsCurrentExecutionArtifact(relative, executionScope);
    }

    private sealed class CollectionCapture : IDisposable {
        private static readonly AsyncLocal<CollectionCapture?> Current = new();
        private readonly CollectionCapture? previous;
        private readonly string toolName;
        internal CollectionObservation? Result { get; private set; }

        internal CollectionCapture(string toolName) {
            this.toolName = toolName;
            previous = Current.Value;
            Current.Value = this;
        }

        internal static void Record(string toolName, object? result, JsonElement serialized) {
            var current = Current.Value;
            if (current is null) {
                return;
            }
            if (current.toolName != toolName || current.Result is not null) {
                throw new InvalidDataException("The workspace collection result does not match the active invocation.");
            }
            current.Result = result switch {
                WorkspaceFileListResult list when toolName is ToolContractCatalog.WorkspaceListDirectory or ToolContractCatalog.WorkspaceListFiles =>
                    new(list.Succeeded, list.RootPath, list.Entries.Select(item => item.RelativePath).ToImmutableArray(),
                        list.ReadSelection, serialized.GetRawText()),
                WorkspaceTextSearchResult search when toolName == ToolContractCatalog.WorkspaceSearch =>
                    new(search.Succeeded, search.RootPath, search.Matches.Select(item => item.RelativePath).ToImmutableArray(),
                        search.ReadSelection, serialized.GetRawText()),
                _ => throw new InvalidDataException("The workspace collection function did not return a typed owner result.")
            };
        }

        public void Dispose() {
            if (Current.Value != this) {
                throw new InvalidOperationException("The workspace collection invocation scope is not current.");
            }
            Current.Value = previous;
        }
    }

    private sealed record CollectionObservation(bool Succeeded, string RootPath, ImmutableArray<string> DisplayPaths,
        WorkspaceFileReadSelection? Selection, string SerializedResult);
}

internal sealed record WorkspaceToolResultSelection(WorkspaceToolResultPath Root, bool RequiresExistingRoot,
    ImmutableArray<string> FullPaths);
