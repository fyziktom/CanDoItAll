using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Tooling;
using CanDoItAll.Infrastructure;
using CanDoItAll.Infrastructure.FileSystem;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.AI;

namespace CanDoItAll.AgentFramework.Maf;

internal sealed partial class WorkspaceToolResultDisclosure(IAgentWorkspaceToolResultSource? source,
    AgentRuntimeToolProviderContext context, WorkspaceRuntimeServices workspace,
    IPhysicalFileSystemPathPolicyFactory pathPolicyFactory,
    Func<IAgentWorkspaceToolResultReadLease, string, AgentWorkspaceToolAccessSettings?> resolveCurrentAccess) {
    private readonly WorkspacePathResolutionService paths = new(workspace.Scope.WorkspaceRoot, pathPolicyFactory,
        workspace.Scope.Scope, workspace.ExternalTargetPathRegistry);
    private readonly StringComparer pathComparer = pathPolicyFactory.Create(workspace.Scope.WorkspaceRoot).PathComparer;

    internal AITool Wrap(AIFunction function) {
        var wrapped = new DisclosureFunction(function, this);
        return function is ApprovalRequiredAIFunction ? new ApprovalRequiredAIFunction(wrapped) : wrapped;
    }

    internal async ValueTask<IAsyncDisposable?> AuthorizeAsync(AgentToolResultDisclosure disclosure,
        CancellationToken cancellationToken) {
        if (disclosure.Evidence is null) {
            throw Unavailable();
        }
        WorkspaceToolResultEvidence evidence;
        try {
            evidence = WorkspaceToolResultEvidence.Read(disclosure.Evidence);
        } catch (Exception exception) when (exception is InvalidDataException or JsonException or ArgumentException) {
            throw Unavailable();
        }
        if (evidence.State != WorkspaceToolResultEvidenceState.Complete) {
            throw Unavailable();
        }
        if (evidence.ToolName != disclosure.Payload.ToolName || evidence.Scope != workspace.Scope.Scope ||
                !pathComparer.Equals(evidence.WorkspaceRoot, workspace.Scope.WorkspaceRoot) ||
                evidence.ExecutionWorkspaceScope != CurrentExecutionScope()) {
            throw Denied();
        }
        var held = await RequireSource().AcquireReadAsync(context, workspace.Scope.Scope, evidence.Source, cancellationToken);
        try {
            var access = resolveCurrentAccess(held, evidence.ToolName) ?? throw Denied();
            var guard = new WorkspaceRuntimeFileAccessGuard(workspace.Scope.WorkspaceRoot, pathPolicyFactory,
                workspace.Scope.Scope, access);
            foreach (var original in evidence.Paths) {
                WorkspaceToolResultPath current;
                try {
                    current = Resolve(original.RequestPath, guard, evidence.ExecutionWorkspaceScope);
                } catch (Exception exception) when (exception is WorkspacePathResolutionException or WorkspaceToolAccessDeniedException) {
                    throw Denied();
                }
                if (!SamePath(original, current)) {
                    throw Denied();
                }
            }
            RequireCollectionCurrent(evidence.Selection, guard, evidence.ExecutionWorkspaceScope);
            held.RequireCurrent();
            return held;
        } catch {
            await held.DisposeAsync();
            throw;
        }
    }

    private async Task<Capture> BeginAsync(string toolName, AIFunctionArguments arguments, CancellationToken cancellationToken) {
        var executionScope = CurrentExecutionScope();
        var originalSource = await RequireSource().CaptureAsync(context, workspace.Scope.Scope, cancellationToken);
        var parsed = ToolInvocationPathArgumentResolver.Resolve(toolName, arguments);
        var state = parsed.IsComplete ? WorkspaceToolResultEvidenceState.Complete : WorkspaceToolResultEvidenceState.UnsupportedPath;
        var guard = new WorkspaceRuntimeFileAccessGuard(workspace.Scope.WorkspaceRoot, pathPolicyFactory,
            workspace.Scope.Scope, context.WorkspaceToolAccess ?? AgentWorkspaceToolAccessMetadata.Read(context.Agent.ConfigurationJson));
        var collectionRequest = IsCollectionTool(toolName)
            ? NormalizeCollectionRequest(toolName, arguments,
                parsed.Values.FirstOrDefault(item => string.Equals(item.Name, "relativePath", StringComparison.OrdinalIgnoreCase))?.Value ?? ".",
                guard, ref state)
            : null;
        var captured = new List<WorkspaceToolResultPath>();
        foreach (var path in parsed.Values.Where(item => collectionRequest is null ||
                     !string.Equals(item.Name, "relativePath", StringComparison.OrdinalIgnoreCase))
                 .Select(item => item.Value).Distinct(StringComparer.Ordinal)) {
            CapturePath(path, guard, executionScope, captured, ref state);
        }
        if (collectionRequest is not null) {
            CapturePath(collectionRequest, guard, executionScope, captured, ref state);
        }
        return new(toolName, originalSource, state, captured, guard, executionScope, collectionRequest);
    }

    private async Task CompleteAsync(Capture capture, object? result, JsonSerializerOptions serializerOptions,
        CollectionCapture? collection, CancellationToken cancellationToken) {
        var state = capture.State;
        foreach (var original in capture.Paths.ToArray()) {
            try {
                if (!SamePath(original, Resolve(original.RequestPath, capture.Guard, capture.ExecutionWorkspaceScope))) {
                    state = WorkspaceToolResultEvidenceState.PathChangedDuringOperation;
                }
            } catch (Exception exception) when (exception is WorkspacePathResolutionException or WorkspaceToolAccessDeniedException) {
                state = WorkspaceToolResultEvidenceState.PathChangedDuringOperation;
            }
        }
        var returned = result is JsonElement element ? element : JsonSerializer.SerializeToElement(result, serializerOptions);
        WorkspaceToolResultSelection? selection = null;
        try {
            if (IsCollectionTool(capture.ToolName)) {
                selection = CaptureCollection(capture, collection, returned, ref state);
            } else {
                foreach (var path in ReadResultPaths(capture.ToolName, returned, serializerOptions)) {
                    CapturePath(path, capture.Guard, capture.ExecutionWorkspaceScope, capture.Paths, ref state);
                }
            }
        } catch (Exception exception) when (exception is JsonException or InvalidDataException) {
            state = WorkspaceToolResultEvidenceState.UnsupportedResult;
        }
        var completedSource = await RequireSource().CompleteAsync(capture.Source, cancellationToken);
        AgentToolInvocationEffectScope.RecordDisclosureEvidence(WorkspaceToolResultEvidence.Write(new(capture.ToolName,
            workspace.Scope.WorkspaceRoot, workspace.Scope.Scope, completedSource, state, capture.Paths.ToImmutableArray(),
            capture.ExecutionWorkspaceScope, selection)));
    }

    private void CapturePath(string path, WorkspaceRuntimeFileAccessGuard guard, WorkspaceScopeDescriptor? executionScope,
        List<WorkspaceToolResultPath> captured, ref WorkspaceToolResultEvidenceState state) {
        if (string.IsNullOrWhiteSpace(path)) {
            return;
        }
        if (captured.Count >= WorkspaceToolResultEvidence.MaximumPaths || path.Length > WorkspaceToolResultEvidence.MaximumPathCharacters) {
            state = WorkspaceToolResultEvidenceState.PathLimitExceeded;
            return;
        }
        var normalized = path.Replace('\\', '/');
        if (!IsWithinCapturedScope(normalized, workspace.Scope.Scope) && !IsCurrentExecutionArtifact(normalized, executionScope)) {
            state = WorkspaceToolResultEvidenceState.UncapturedWorkspaceScope;
            return;
        }
        try {
            var resolved = Resolve(path, guard, executionScope);
            if (resolved.RelativePath.Length > WorkspaceToolResultEvidence.MaximumPathCharacters ||
                    resolved.FullPath.Length > WorkspaceToolResultEvidence.MaximumPathCharacters) {
                state = WorkspaceToolResultEvidenceState.PathLimitExceeded;
                return;
            }
            if (resolved.IsWorkspacePath && !IsWithinCapturedScope(resolved.RelativePath, workspace.Scope.Scope) &&
                    !IsCurrentExecutionArtifact(resolved.RelativePath, executionScope)) {
                state = WorkspaceToolResultEvidenceState.UncapturedWorkspaceScope;
            }
            if (!captured.Contains(resolved)) {
                captured.Add(resolved);
            }
        } catch (Exception exception) when (exception is WorkspacePathResolutionException or WorkspaceToolAccessDeniedException) {
            state = WorkspaceToolResultEvidenceState.UnsupportedPath;
        }
    }

    private WorkspaceToolResultPath Resolve(string path, WorkspaceRuntimeFileAccessGuard guard,
        WorkspaceScopeDescriptor? executionScope) {
        var prepared = guard.PrepareFileReadPath(path) ?? ".";
        if (IsCurrentExecutionArtifact(prepared, executionScope) &&
                WorkspaceProcessRunArtifactPath.TryResolveRunId(prepared, out var runId, out var suffix)) {
            prepared = WorkspaceScopeDescriptor.Sandbox.CombineArtifactPath("process-runs", runId, suffix);
        }
        var resolved = paths.ResolvePath(prepared);
        return new(path, resolved.RelativePath.Replace('\\', '/'), Path.GetFullPath(resolved.FullPath), resolved.IsWorkspacePath);
    }

    private WorkspaceScopeDescriptor? CurrentExecutionScope() {
        var session = context.AdmittedToolSession ?? throw Unavailable();
        return WorkspaceExecutionAuditContext.RequireMatchingExecutionWorkspaceScope(session.ExecutionRunId,
            context.Agent.Id, workspace.Scope.Scope);
    }

    private static bool IsCurrentExecutionArtifact(string path, WorkspaceScopeDescriptor? executionScope) {
        if (executionScope is null || !IsWithinCapturedScope(path, executionScope) ||
                !WorkspaceProcessRunArtifactPath.TryResolveRunId(path, out var runId, out _) ||
                !Guid.TryParse(runId, out var parsedRunId) ||
                !Guid.TryParse(WorkspaceExecutionAuditContext.Current?.ProcessRunId, out var currentRunId)) {
            return false;
        }
        return parsedRunId == currentRunId;
    }

    private bool SamePath(WorkspaceToolResultPath original, WorkspaceToolResultPath current)
        => original.IsWorkspacePath == current.IsWorkspacePath && pathComparer.Equals(original.FullPath, current.FullPath) &&
           string.Equals(original.RelativePath, current.RelativePath, StringComparison.Ordinal);

    internal static bool IsWithinCapturedScope(string path, WorkspaceScopeDescriptor scope) {
        foreach (var root in WorkspaceScopeDescriptor.ManagedRootNames) {
            var prefix = root + "/scopes/";
            if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) {
                continue;
            }
            var owned = root + "/" + scope.PartitionRelativePath;
            return !scope.IsDefaultSandbox && (string.Equals(path, owned, StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith(owned + "/", StringComparison.OrdinalIgnoreCase));
        }
        return true;
    }

    internal static IReadOnlyList<string> ReadResultPaths(string toolName, JsonElement result, JsonSerializerOptions serializerOptions) {
        if (IsCollectionTool(toolName)) {
            throw new InvalidDataException("Workspace collections require their live owner selection before JSON marshalling.");
        }
        if (toolName == ToolContractCatalog.WorkspaceAnalyzeImages) {
            var images = result.Deserialize<WorkspaceImagesAnalysisResult>(serializerOptions)
                ?? throw new InvalidDataException("The image-analysis result is absent.");
            if (images.Paths is null || images.Images is null) {
                throw new InvalidDataException("The image-analysis result has no source paths.");
            }
            return images.Paths.Concat(images.Images.Select(image => image.Path)).Distinct(StringComparer.Ordinal).ToArray();
        }
        var receipt = result.Deserialize<ReceiptResult>(serializerOptions)?.Receipt
            ?? throw new InvalidDataException("The owned workspace result has no receipt.");
        if (receipt.TargetPaths is null || receipt.ArtifactReferences is null) {
            throw new InvalidDataException("The owned workspace receipt has no target boundary.");
        }
        var paths = receipt.TargetPaths.Concat(receipt.ArtifactReferences.Select(artifact => artifact.RelativePath));
        if (!string.IsNullOrWhiteSpace(receipt.ReceiptRelativePath)) {
            paths = paths.Append(receipt.ReceiptRelativePath);
        }
        return paths.Where(path => !string.IsNullOrWhiteSpace(path)).Distinct(StringComparer.Ordinal).ToArray();
    }

    private IAgentWorkspaceToolResultSource RequireSource() => source ?? throw Unavailable();
    private static AgentToolAdmissionException Unavailable() => new("workspace.result-authority-unavailable",
        "The saved workspace result lacks supported original source and path evidence. Recovery cannot disclose or repeat it.");
    private static AgentToolAdmissionException Denied() => new("workspace.result-disclosure-denied",
        "Current workspace tool and path authority does not permit the saved result.");

    private sealed record ReceiptResult(WorkspaceToolReceipt? Receipt);
    private sealed record Capture(string ToolName, AgentToolProtocolEnvelope Source, WorkspaceToolResultEvidenceState State,
        List<WorkspaceToolResultPath> Paths, WorkspaceRuntimeFileAccessGuard Guard, WorkspaceScopeDescriptor? ExecutionWorkspaceScope,
        string? CollectionRequestPath);

    private sealed class DisclosureFunction(AIFunction inner, WorkspaceToolResultDisclosure owner) : DelegatingAIFunction(inner) {
        protected override async ValueTask<object?> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken) {
            Capture capture;
            try {
                capture = await owner.BeginAsync(Name, arguments, cancellationToken);
            } catch (AgentToolAdmissionException exception) {
                var denied = new AgentToolFailureResult(false, exception.Code,
                    "Current source authority did not permit this workspace invocation.", false) {
                    EffectState = AgentToolEffectState.NotCommitted
                };
                AgentToolInvocationEffectScope.RecordPreDispatchFailure(new(denied.ErrorCode, denied.Message));
                return denied;
            }
            using var collection = IsCollectionTool(Name) ? new CollectionCapture(Name) : null;
            var result = await base.InvokeCoreAsync(arguments, cancellationToken);
            await owner.CompleteAsync(capture, result, JsonSerializerOptions, collection, cancellationToken);
            return result;
        }
    }
}

internal enum WorkspaceToolResultEvidenceState {
    Complete,
    UnsupportedPath,
    UnsupportedResult,
    PathChangedDuringOperation,
    UncapturedWorkspaceScope,
    PathLimitExceeded
}

internal sealed record WorkspaceToolResultPath(string RequestPath, string RelativePath, string FullPath, bool IsWorkspacePath);

internal sealed record WorkspaceToolResultEvidence(string ToolName, string WorkspaceRoot, WorkspaceScopeDescriptor Scope,
    AgentToolProtocolEnvelope Source, WorkspaceToolResultEvidenceState State, ImmutableArray<WorkspaceToolResultPath> Paths,
    WorkspaceScopeDescriptor? ExecutionWorkspaceScope = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] WorkspaceToolResultSelection? Selection = null) {
    private const string Format = "configured-workspace-result-authority";
    private const int Version = 1;
    private const int CollectionVersion = 2;
    internal const int MaximumPaths = 256;
    internal const int MaximumPathCharacters = 1024;

    internal static AgentToolProtocolEnvelope Write(WorkspaceToolResultEvidence evidence) {
        Validate(evidence);
        return AgentToolProtocolEnvelope.Create(Format,
            WorkspaceToolResultDisclosure.IsCollectionTool(evidence.ToolName) ? CollectionVersion : Version,
            JsonSerializer.Serialize(evidence));
    }

    internal static WorkspaceToolResultEvidence Read(AgentToolProtocolEnvelope envelope) {
        if (envelope.Format != Format || envelope.Version is not (Version or CollectionVersion)) {
            throw new InvalidDataException("The workspace result authority format is unsupported.");
        }
        var evidence = JsonSerializer.Deserialize<WorkspaceToolResultEvidence>(envelope.PayloadJson)
            ?? throw new InvalidDataException("The workspace result authority is absent.");
        Validate(evidence);
        if (WorkspaceToolResultDisclosure.IsCollectionTool(evidence.ToolName) != (envelope.Version == CollectionVersion)) {
            throw new InvalidDataException("The workspace result authority version does not match its operation.");
        }
        return evidence;
    }

    private static void Validate(WorkspaceToolResultEvidence evidence) {
        if (string.IsNullOrWhiteSpace(evidence.ToolName) || !ToolContractCatalog.WorkspaceToolNames.Contains(evidence.ToolName) ||
                string.IsNullOrWhiteSpace(evidence.WorkspaceRoot) || !Path.IsPathFullyQualified(evidence.WorkspaceRoot) ||
                evidence.Scope is null || evidence.Source is null || !Enum.IsDefined(evidence.State) || evidence.Paths.IsDefault ||
                evidence.Paths.Length > MaximumPaths || evidence.Paths.Any(path => path is null ||
                    string.IsNullOrWhiteSpace(path.RequestPath) || string.IsNullOrWhiteSpace(path.FullPath) ||
                    path.RequestPath.Length > MaximumPathCharacters || path.RelativePath is null ||
                    path.RelativePath.Length > MaximumPathCharacters || path.FullPath.Length > MaximumPathCharacters ||
                    !Path.IsPathFullyQualified(path.FullPath))) {
            throw new InvalidDataException("The workspace result authority is invalid.");
        }
        var collection = WorkspaceToolResultDisclosure.IsCollectionTool(evidence.ToolName);
        if (!collection && evidence.Selection is not null ||
                collection && evidence.State == WorkspaceToolResultEvidenceState.Complete && evidence.Selection is null) {
            throw new InvalidDataException("The workspace collection authority is incomplete.");
        }
        if (evidence.Selection is { } selection &&
                (selection.Root is null || !evidence.Paths.Contains(selection.Root) || selection.FullPaths.IsDefault ||
                 evidence.Paths.Length + selection.FullPaths.Length > MaximumPaths ||
                 !selection.RequiresExistingRoot && selection.FullPaths.Length != 0 ||
                 selection.FullPaths.Any(value => string.IsNullOrWhiteSpace(value) ||
                     value.Length > MaximumPathCharacters || !Path.IsPathFullyQualified(value)))) {
            throw new InvalidDataException("The workspace collection authority is invalid.");
        }
    }
}
