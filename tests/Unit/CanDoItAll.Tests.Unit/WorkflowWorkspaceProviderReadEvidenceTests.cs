using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Workspace;
using CanDoItAll.Infrastructure.FileSystem;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Tests.Support;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed partial class WorkflowWorkspaceProviderReadEvidenceTests : IDisposable {
    private readonly string root = TestFileSystem.CreateTemporaryRoot(nameof(WorkflowWorkspaceProviderReadEvidenceTests));
    private readonly WorkspaceScopeDescriptor scope = WorkspaceScopeDescriptor.Organization(Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData(WorkflowStorageFileOperation.ReadText)]
    [InlineData(WorkflowStorageFileOperation.ListDirectory)]
    [InlineData(WorkflowStorageFileOperation.SearchText)]
    [InlineData(WorkflowStorageFileOperation.DiffText)]
    [InlineData(WorkflowStorageFileOperation.Exists)]
    public async Task Actual_file_invoker_preserves_public_result_and_retains_roundtrippable_original_targets(WorkflowStorageFileOperation operation) {
        Write("original.txt", "original evidence");
        Write("other.txt", "other evidence");
        var (files, paths) = Services();
        var settings = new WorkflowStorageFileExecutorSettings {
            Operation = operation, Path = operation is WorkflowStorageFileOperation.ListDirectory or WorkflowStorageFileOperation.SearchText ? "." : "original.txt",
            DestinationPath = "other.txt", Query = "evidence"
        };
        var executor = new WorkspaceFileWorkflowExecutor(files, paths);
        var (node, read, payload) = await InvokeAsync(executor, settings, "{}");

        var retained = JsonSerializer.Deserialize<WorkflowCompletedNodeRead>(JsonSerializer.Serialize(read,
            WorkflowProviderDisclosureContent.JsonOptions), WorkflowProviderDisclosureContent.JsonOptions)!;
        await WorkflowWorkspaceProviderReadEvidence.RequireCurrentAsync(retained, node, paths.ExecutionScope, TestWorkspaceServices.PhysicalPathPolicyFactory);

        Assert.Equal(read.Proof, retained.Proof);
        Assert.True(retained.Evidence.Count >= 2);
        Assert.DoesNotContain("providerReadEvidence", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("workspaceRoot", payload, StringComparison.OrdinalIgnoreCase);
        Assert.True(files.ExecutionScope.SharesIdentityWith(paths.ExecutionScope));
        Assert.Equal(files.ExecutionScope.RootCaseSensitivity, paths.ExecutionScope.RootCaseSensitivity);
        Assert.Equal(Path.GetFullPath(root), paths.ExecutionScope.WorkspaceRoot);
        Assert.Equal(scope, paths.ExecutionScope.Scope);
        if (operation == WorkflowStorageFileOperation.ReadText) {
            Assert.Equal("original evidence", WorkflowExecutorJson.Deserialize<WorkspaceTextFileReadResult>(payload).Content);
        }
    }

    [Theory]
    [InlineData(InvalidEvidence.MissingTargets)]
    [InlineData(InvalidEvidence.MissingScope)]
    [InlineData(InvalidEvidence.DifferentOccurrence)]
    [InlineData(InvalidEvidence.DifferentVersion)]
    [InlineData(InvalidEvidence.UnsupportedSchema)]
    [InlineData(InvalidEvidence.AbsoluteNotAdmitted)]
    public async Task Incomplete_or_mismatched_owner_values_cannot_authorize_cached_content(InvalidEvidence corruption) {
        Write("original.txt", "original evidence");
        var (files, paths) = Services();
        var (node, read, _) = await InvokeAsync(new WorkspaceFileWorkflowExecutor(files, paths),
            new WorkflowStorageFileExecutorSettings { Operation = WorkflowStorageFileOperation.ReadText, Path = "original.txt" }, "{}");
        var evidence = read.Evidence.ToArray();
        var target = JsonSerializer.Deserialize<WorkflowWorkspaceReadPart>(evidence[1].PayloadJson, WorkflowProviderDisclosureContent.JsonOptions)!;
        evidence = corruption switch {
            InvalidEvidence.MissingTargets => [evidence[0]],
            InvalidEvidence.MissingScope => evidence.Skip(1).ToArray(),
            InvalidEvidence.DifferentOccurrence => evidence.Select(item => item with { Occurrence = WorkflowExecutionOccurrence.Start(WorkflowRunId.New()) }).ToArray(),
            InvalidEvidence.DifferentVersion => evidence.Select(item => item with { VersionId = WorkflowVersionId.New() }).ToArray(),
            InvalidEvidence.UnsupportedSchema => evidence.Select(item => item with { SchemaVersion = 200 }).ToArray(),
            InvalidEvidence.AbsoluteNotAdmitted => [evidence[0], evidence[1] with { PayloadJson = JsonSerializer.Serialize(target with {
                Target = target.Target! with { Origin = WorkflowWorkspaceReadPathOrigin.ExplicitAbsolute }
            }, WorkflowProviderDisclosureContent.JsonOptions) }],
            _ => throw new ArgumentOutOfRangeException(nameof(corruption))
        };
        await Assert.ThrowsAnyAsync<InvalidOperationException>(() => WorkflowWorkspaceProviderReadEvidence.RequireCurrentAsync(
            read with { Evidence = evidence }, node, paths.ExecutionScope, TestWorkspaceServices.PhysicalPathPolicyFactory).AsTask());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Original_scope_cannot_be_rebound_to_another_root_or_profile(bool changeProfile) {
        Write("original.txt", "original evidence");
        var (files, paths) = Services();
        var (node, read, _) = await InvokeAsync(new WorkspaceFileWorkflowExecutor(files, paths),
            new WorkflowStorageFileExecutorSettings { Operation = WorkflowStorageFileOperation.ReadText, Path = "original.txt" }, "{}");
        var replacementRoot = changeProfile ? root : Path.Combine(root, "replacement");
        Directory.CreateDirectory(replacementRoot);
        var replacement = TestWorkspaceServices.CreatePathResolutionService(replacementRoot,
            changeProfile ? WorkspaceScopeDescriptor.Organization(Guid.NewGuid().ToString("N")) : scope);
        await Assert.ThrowsAsync<InvalidOperationException>(() => WorkflowWorkspaceProviderReadEvidence.RequireCurrentAsync(
            read, node, replacement.ExecutionScope, TestWorkspaceServices.PhysicalPathPolicyFactory).AsTask());
    }

    [Fact]
    public async Task Source_ingestion_preserves_exact_content_identity_even_when_length_and_timestamp_are_restored() {
        var file = Write("original.txt", "original evidence");
        var (_, paths) = Services();
        var executor = new SourceIngestionWorkflowExecutor(paths, new NoConversion(), TestExternalTargetPathRegistry.Create(),
            TestWorkspaceServices.PhysicalPathPolicyFactory);
        var (node, read, payload) = await InvokeAsync(executor, new WorkflowSourceIngestionExecutorSettings { AllowedExtensions = [".txt"] }, Sources("original.txt"));
        Assert.Contains("original evidence", payload, StringComparison.Ordinal);
        await WorkflowWorkspaceProviderReadEvidence.RequireCurrentAsync(read, node, paths.ExecutionScope, TestWorkspaceServices.PhysicalPathPolicyFactory);
        var stamp = File.GetLastWriteTimeUtc(file);
        File.WriteAllText(file, "replaced evidence");
        File.SetLastWriteTimeUtc(file, stamp);
        await Assert.ThrowsAsync<InvalidOperationException>(() => WorkflowWorkspaceProviderReadEvidence.RequireCurrentAsync(
            read, node, paths.ExecutionScope, TestWorkspaceServices.PhysicalPathPolicyFactory).AsTask());
    }

    [Fact]
    public async Task Retained_physical_target_is_rechecked_for_a_reparse_replacement() {
        var file = Write("original.txt", "original evidence");
        var replacement = Write("replacement.txt", "replacement evidence");
        var (files, paths) = Services();
        var (node, read, _) = await InvokeAsync(new WorkspaceFileWorkflowExecutor(files, paths),
            new WorkflowStorageFileExecutorSettings { Operation = WorkflowStorageFileOperation.ReadText, Path = "original.txt" }, "{}");
        await WorkflowWorkspaceProviderReadEvidence.RequireCurrentAsync(read, node, paths.ExecutionScope, TestWorkspaceServices.PhysicalPathPolicyFactory);
        File.Delete(file);
        File.CreateSymbolicLink(file, replacement);
        try {
            await Assert.ThrowsAsync<PhysicalPathValidationException>(() => WorkflowWorkspaceProviderReadEvidence.RequireCurrentAsync(
                read, node, paths.ExecutionScope, TestWorkspaceServices.PhysicalPathPolicyFactory).AsTask());
        } finally {
            File.Delete(file);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Ingested_file_evidence_requires_its_complete_original_content_identity(bool missingHash) {
        Write("original.txt", "original evidence");
        var (_, paths) = Services();
        var executor = new SourceIngestionWorkflowExecutor(paths, new NoConversion(), TestExternalTargetPathRegistry.Create(),
            TestWorkspaceServices.PhysicalPathPolicyFactory);
        var (node, read, _) = await InvokeAsync(executor, new WorkflowSourceIngestionExecutorSettings { AllowedExtensions = [".txt"] }, Sources("original.txt"));
        var original = read.Evidence[1];
        var part = JsonSerializer.Deserialize<WorkflowWorkspaceReadPart>(original.PayloadJson, WorkflowProviderDisclosureContent.JsonOptions)!;
        Assert.NotNull(part.Target!.ContentIdentity);
        var malformed = part with { Target = part.Target with {
            ContentIdentity = missingHash ? part.Target.ContentIdentity.Value with { Sha256 = null! } : null
        } };
        await Assert.ThrowsAsync<InvalidOperationException>(() => WorkflowWorkspaceProviderReadEvidence.RequireCurrentAsync(read with {
            Evidence = [read.Evidence[0], original with { PayloadJson = JsonSerializer.Serialize(malformed, WorkflowProviderDisclosureContent.JsonOptions) }]
        }, node, paths.ExecutionScope, TestWorkspaceServices.PhysicalPathPolicyFactory).AsTask());
    }

    [Fact]
    public void An_unsupported_scope_port_has_no_permissive_default() {
        IWorkspacePathResolutionService unsupported = new UnsupportedPaths();
        Assert.Throws<NotSupportedException>(() => unsupported.ExecutionScope);
        Assert.Throws<NotSupportedException>(() => unsupported.ResolvePath("original.txt"));
        Assert.Equal("legacy.txt", unsupported.ResolveFilePath("legacy.txt", false).RelativePath);
    }

    [Fact]
    public async Task Write_only_operations_keep_their_existing_unmarked_result() {
        var (files, _) = Services();
        var executor = new WorkspaceFileWorkflowExecutor(files);
        var (_, read, payload) = await InvokeAsync(executor,
            new WorkflowStorageFileExecutorSettings { Operation = WorkflowStorageFileOperation.WriteText, Path = "written.txt", Content = "written" }, "{}");
        Assert.Empty(read.Evidence);
        Assert.Null(read.Manifest);
        Assert.True(WorkflowExecutorJson.Deserialize<WorkspaceFileMutationResult>(payload).Succeeded);
        Assert.Equal("written", File.ReadAllText(Path.Combine(root, "written.txt")));
    }

    private (WorkspaceFileService Files, WorkspacePathResolutionService Paths) Services()
        => (TestWorkspaceServices.CreateFileService(root, scope), TestWorkspaceServices.CreatePathResolutionService(root, scope));

    private string Write(string relative, string content) {
        var path = Path.Combine(root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }

    private static string Sources(string path) => JsonSerializer.Serialize(new { sources = new[] {
        new { key = "original", label = "Original", kind = "filePath", value = path, isEnabled = true }
    } });

    private static async Task<(WorkflowNode Node, WorkflowCompletedNodeRead Read, string Payload)> InvokeAsync<TSettings>(
        IWorkflowExecutor executor, TSettings settings, string payload) {
        var shape = new WorkflowValueShape(WorkflowValueShapeKind.Object, "{}", "Evidence");
        var node = new WorkflowNode(new("read"), WorkflowNodeKind.Executor, "Original file read", [],
            new(null, null, null, null, "", shape, shape) { ExecutorId = executor.Descriptor.Id, ExecutorSettingsJson = WorkflowExecutorJson.Serialize(settings) });
        var definition = new WorkflowDefinition(WorkflowId.New(), WorkflowVersionId.New(), "Original file read", "", WorkflowLifecycleStatus.Draft,
            new(node.Id, [node], []), new(WorkflowRuntimeBackendKind.InProcess, true, false, false, false), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        var occurrence = WorkflowExecutionOccurrence.Start(WorkflowRunId.New()).Advance(definition.VersionId, node.Id);
        using var audit = WorkflowExecutorExecutionAuditScope.Push(occurrence.RunId);
        var input = new WorkflowNodeInput(payload);
        var invoker = new WorkflowExecutorInvoker(new WorkflowExecutorCatalog([executor]), [executor]);
        var output = await invoker.ExecuteAsync(definition, node, input, WorkflowExecutorInvocationContext.Empty with {
            ExecutionOccurrence = occurrence, CompilerContractVersion = WorkflowProviderDisclosureProtocol.Current
        });
        var proof = new WorkflowNodeCompletionProof(Guid.NewGuid(), occurrence, definition.Id, definition.VersionId, node.Id, executor.Descriptor.Id,
            WorkflowProviderDisclosureContent.Definition(definition), WorkflowProviderDisclosureContent.Settings(node), WorkflowExecutionContentHash.Compute(payload),
            WorkflowExecutionContentHash.Compute(output.PayloadJson), WorkflowProviderDisclosureContent.Source(null), WorkflowProviderDisclosureProtocol.Current);
        return (node, new(proof, WorkflowProviderDisclosureContent.Manifest(output.ProviderReadEvidence), output.ProviderReadEvidence), output.PayloadJson);
    }

    public void Dispose() => TestFileSystem.DeleteDirectoryWithRetry(root);

    public enum InvalidEvidence { MissingTargets, MissingScope, DifferentOccurrence, DifferentVersion, UnsupportedSchema, AbsoluteNotAdmitted }

    private sealed class UnsupportedPaths : IWorkspacePathResolutionService {
        public WorkspaceResolvedPath ResolveFilePath(string path, bool allowMissing) => new(path, path, true);
        public WorkspaceResolvedPath ResolveDirectoryPath(string path, bool allowMissing) => new(path, path, true);
    }

    private sealed class NoConversion : IWorkspaceDocumentMarkdownConverter {
        public Task<WorkspaceDocumentMarkdownConversionResult> ConvertToMarkdownAsync(WorkspaceDocumentMarkdownConversionRequest request,
            CancellationToken cancellationToken = default) => throw new InvalidOperationException("Text fixture must not invoke document conversion.");
    }
}
