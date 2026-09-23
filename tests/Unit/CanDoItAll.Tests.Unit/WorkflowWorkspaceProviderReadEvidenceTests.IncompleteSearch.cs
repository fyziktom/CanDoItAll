using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Workspace;
using CanDoItAll.Infrastructure.FileSystem;
using CanDoItAll.Infrastructure.Storage;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed partial class WorkflowWorkspaceProviderReadEvidenceTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Inaccessible_single_file_search_preserves_incomplete_success_and_exact_owner_root(bool useAlias) {
        const string rawFailure = "Private native stream-open failure.";
        var original = Write(Path.Combine("sources", NativeLiteralName), "needle original");
        var registry = new ExternalTargetPathRegistry();
        var requested = original;
        if (useAlias) {
            Assert.True(registry.TryCreateAlias(original, out requested));
        }
        var paths = TestWorkspaceServices.CreatePathResolutionService(root, scope, registry);
        var fileOwner = TestWorkspaceServices.CreateFileService(root, scope, registry);
        var opened = new List<string>();
        var queries = new WorkspaceFileQueryService(
            TestWorkspaceServices.CreatePathPolicy(root, scope, registry),
            new WorkspaceFileReceiptWriter(root, scope), new WorkspaceTextContentGuard(path => {
                opened.Add(path);
                throw new IOException(rawFailure);
            }));
        var files = new IncompleteSearchOwnerPort(queries, fileOwner.ExecutionScope);

        var (node, read, payload) = await InvokeAsync(new WorkspaceFileWorkflowExecutor(files, paths),
            new WorkflowStorageFileExecutorSettings { Operation = WorkflowStorageFileOperation.SearchText, Path = requested, Query = "needle" }, "{}");

        Assert.Equal(original, Assert.Single(opened));
        var ownerResult = Assert.IsType<WorkspaceTextSearchResult>(files.LastResult);
        Assert.True(ownerResult.Succeeded);
        Assert.True(ownerResult.IsTruncated);
        Assert.Empty(ownerResult.Matches);
        Assert.Equal(original, ownerResult.ReadSelection!.GetRootPath());
        Assert.Equal(!useAlias, ownerResult.ReadSelection.IsWorkspacePath);
        Assert.Empty(ownerResult.ReadSelection.GetSelectedPaths());
        Assert.Equal("Succeeded", ownerResult.Receipt.Outcome);
        Assert.Contains("search is incomplete", ownerResult.Message, StringComparison.Ordinal);
        Assert.Contains("before concluding that no match exists", ownerResult.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(rawFailure, ownerResult.Message, StringComparison.Ordinal);

        var publicResult = WorkflowExecutorJson.Deserialize<WorkspaceTextSearchResult>(payload);
        Assert.True(publicResult.Succeeded);
        Assert.True(publicResult.IsTruncated);
        Assert.Empty(publicResult.Matches);
        Assert.Equal(ownerResult.Message, publicResult.Message);
        Assert.Null(publicResult.ReadSelection);
        Assert.DoesNotContain(rawFailure, payload, StringComparison.Ordinal);
        Assert.DoesNotContain("readSelection", payload, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(read.Evidence.Select(item => JsonSerializer.Deserialize<WorkflowWorkspaceReadPart>(item.PayloadJson,
            WorkflowProviderDisclosureContent.JsonOptions)!), item => item.Target is { AllowMissing: false } target &&
                target.FullPath == original && target.ReadRoot == original && target.Origin == (useAlias
                    ? WorkflowWorkspaceReadPathOrigin.RegisteredAlias : WorkflowWorkspaceReadPathOrigin.Workspace));
        var restored = JsonSerializer.Deserialize<WorkflowCompletedNodeRead>(JsonSerializer.Serialize(read,
            WorkflowProviderDisclosureContent.JsonOptions), WorkflowProviderDisclosureContent.JsonOptions)!;
        await WorkflowWorkspaceProviderReadEvidence.RequireCurrentAsync(restored, node, paths.ExecutionScope,
            TestWorkspaceServices.PhysicalPathPolicyFactory);
        Assert.Single(opened);
        File.Delete(original);
        await Assert.ThrowsAsync<PhysicalPathValidationException>(() => WorkflowWorkspaceProviderReadEvidence.RequireCurrentAsync(
            restored, node, paths.ExecutionScope, TestWorkspaceServices.PhysicalPathPolicyFactory).AsTask());
        Assert.Single(opened);
    }

    private sealed class IncompleteSearchOwnerPort(WorkspaceFileQueryService queries, WorkspaceExecutionScope scope) : IWorkspaceFileService {
        public WorkspaceExecutionScope ExecutionScope => scope;
        public WorkspaceTextSearchResult? LastResult { get; private set; }
        public WorkspaceTextSearchResult SearchText(string query, string? relativePath = null, int maxResults = 20)
            => LastResult = queries.SearchText(query, relativePath, maxResults);
        public WorkspaceFileListResult ListDirectory(string? relativePath = null, int maxResults = 100) => throw new NotSupportedException();
        public WorkspaceFileListResult ListFiles(string? relativePath = null, string searchPattern = "*", int maxResults = 100) => throw new NotSupportedException();
        public WorkspaceFileListResult ListFiles(string path, string searchPattern, int maxResults, string authorityRootPath) => throw new NotSupportedException();
        public WorkspaceTextFileReadResult ReadTextFile(string path, int maxCharacters = 12000) => throw new NotSupportedException();
        public WorkspaceTextFileReadResult ReadTextFile(string path, int maxCharacters, string authorityRootPath) => throw new NotSupportedException();
        public WorkspacePathStatResult StatPath(string path) => throw new NotSupportedException();
        public WorkspacePathStatResult StatPath(string path, string authorityRootPath) => throw new NotSupportedException();
        public WorkspacePathHashResult HashPath(string path, int maxFiles = 200, long maxBytes = 10485760) => throw new NotSupportedException();
        public WorkspaceFileMutationResult CreateDirectory(string path) => throw new NotSupportedException();
        public WorkspaceFileMutationResult WriteTextFile(string path, string content, bool overwrite = true) => throw new NotSupportedException();
        public WorkspaceFileMutationResult WriteTextFile(string path, string content, bool overwrite, string authorityRootPath) => throw new NotSupportedException();
        public WorkspaceFileMutationResult AppendTextFile(string path, string content) => throw new NotSupportedException();
        public WorkspaceFileMutationResult AppendTextFile(string path, string content, string authorityRootPath) => throw new NotSupportedException();
        public WorkspaceFileMutationResult CopyPath(string sourcePath, string destinationPath, bool overwrite = false) => throw new NotSupportedException();
        public WorkspaceFileMutationResult CopyPath(string sourcePath, string destinationPath, bool overwrite, string destinationAuthorityRootPath) => throw new NotSupportedException();
        public WorkspaceFileMutationResult MovePath(string sourcePath, string destinationPath, bool overwrite = false) => throw new NotSupportedException();
        public WorkspaceFileMutationResult MovePath(string sourcePath, string destinationPath, bool overwrite, string destinationAuthorityRootPath) => throw new NotSupportedException();
        public WorkspaceFileMutationResult DeletePath(string path, bool recursive = false) => throw new NotSupportedException();
        public WorkspaceArchiveMutationResult ZipPath(string sourcePath, string destinationPath, bool overwrite = false, int maxFiles = 200, long maxBytes = 10485760) => throw new NotSupportedException();
        public WorkspaceArchiveMutationResult UnzipArchive(string sourcePath, string destinationPath, bool overwrite = false, int maxFiles = 200, long maxBytes = 10485760) => throw new NotSupportedException();
        public WorkspaceArchiveMutationResult UnzipArchive(string sourcePath, string destinationPath, bool overwrite, int maxFiles, long maxBytes, string destinationAuthorityRootPath) => throw new NotSupportedException();
        public WorkspaceTextDiffResult DiffTextFiles(string leftPath, string rightPath, int maxLines = 160) => throw new NotSupportedException();
    }
}
