using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Workspace;
using CanDoItAll.Infrastructure.FileSystem;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed partial class WorkflowWorkspaceProviderReadEvidenceTests {
    [Theory]
    [InlineData(WorkflowStorageFileOperation.List, "sources/**", "sources")]
    [InlineData(WorkflowStorageFileOperation.Tree, "sources/**", "sources")]
    [InlineData(WorkflowStorageFileOperation.List, "sources/**/*.txt", "sources")]
    [InlineData(WorkflowStorageFileOperation.Tree, "sources/**/*.txt", "sources")]
    [InlineData(WorkflowStorageFileOperation.List, "sources**", "sources")]
    [InlineData(WorkflowStorageFileOperation.Tree, "sources**", "sources")]
    [InlineData(WorkflowStorageFileOperation.List, "**/*.txt", "")]
    [InlineData(WorkflowStorageFileOperation.Tree, "**/*.txt", "")]
    [InlineData(WorkflowStorageFileOperation.List, "", "")]
    [InlineData(WorkflowStorageFileOperation.Tree, "", "")]
    [InlineData(WorkflowStorageFileOperation.List, " sources/** ", "sources")]
    [InlineData(WorkflowStorageFileOperation.Tree, " sources/** ", "sources")]
    public async Task Actual_list_shorthand_retains_original_settings_and_revalidates_exact_owner_targets(
        WorkflowStorageFileOperation operation, string requestedPath, string relativeRoot) {
        var original = Write(Path.Combine("sources", NativeLiteralName), "original content");
        var decoy = Write(Path.Combine("sources", "literal", "name.txt"), "decoy content");
        Write(Path.Combine("sources", "other.md"), "other content");
        var (files, paths) = Services();
        var settings = new WorkflowStorageFileExecutorSettings { Operation = operation, Path = requestedPath };
        var expected = files.ListFiles(requestedPath, settings.SearchPattern);
        Assert.True(expected.Succeeded, expected.Message);

        var (node, read, payload) = await InvokeAsync(new WorkspaceFileWorkflowExecutor(files, paths), settings, "{}");
        var result = WorkflowExecutorJson.Deserialize<WorkspaceFileListResult>(payload);
        Assert.True(result.Succeeded, result.Message);
        Assert.Equal(expected.RootPath, result.RootPath);
        Assert.Equal(expected.SearchPattern, result.SearchPattern);
        Assert.Equal(expected.Entries, result.Entries);
        Assert.Equal(WorkflowExecutorJson.Serialize(settings), node.Settings.ExecutorSettingsJson);
        Assert.Equal(requestedPath, WorkflowExecutorJson.Deserialize<WorkflowStorageFileExecutorSettings>(node.Settings.ExecutorSettingsJson!).Path);
        Assert.Equal(WorkflowProviderDisclosureContent.Settings(node), read.Proof.SettingsHash);
        Assert.DoesNotContain("readSelection", payload, StringComparison.OrdinalIgnoreCase);

        var retained = JsonSerializer.Deserialize<WorkflowCompletedNodeRead>(JsonSerializer.Serialize(read,
            WorkflowProviderDisclosureContent.JsonOptions), WorkflowProviderDisclosureContent.JsonOptions)!;
        var targets = retained.Evidence.Select(item => JsonSerializer.Deserialize<WorkflowWorkspaceReadPart>(item.PayloadJson,
            WorkflowProviderDisclosureContent.JsonOptions)!.Target).OfType<WorkflowWorkspaceReadTarget>().ToArray();
        var expectedRoot = Path.Combine(root, relativeRoot);
        Assert.Contains(targets, target => target.FullPath == expectedRoot && target.ReadRoot == expectedRoot && !target.AllowMissing);
        Assert.Contains(targets, target => target.FullPath == original && target.ReadRoot == expectedRoot && !target.AllowMissing);
        await WorkflowWorkspaceProviderReadEvidence.RequireCurrentAsync(retained, node, paths.ExecutionScope,
            TestWorkspaceServices.PhysicalPathPolicyFactory);
        File.Delete(original);
        Assert.True(File.Exists(decoy));
        await Assert.ThrowsAsync<PhysicalPathValidationException>(() => WorkflowWorkspaceProviderReadEvidence.RequireCurrentAsync(
            retained, node, paths.ExecutionScope, TestWorkspaceServices.PhysicalPathPolicyFactory).AsTask());
    }

    [Theory]
    [InlineData(WorkflowStorageFileOperation.List)]
    [InlineData(WorkflowStorageFileOperation.Tree)]
    public async Task An_explicit_search_pattern_does_not_turn_a_literal_missing_root_into_shorthand(
        WorkflowStorageFileOperation operation) {
        var original = Write(Path.Combine("sources", "original.txt"), "original content");
        var (files, paths) = Services();
        var settings = new WorkflowStorageFileExecutorSettings {
            Operation = operation, Path = "sources/**", SearchPattern = "*.txt"
        };
        Assert.False(files.ListFiles(settings.Path, settings.SearchPattern).Succeeded);
        var failure = await Assert.ThrowsAsync<WorkflowExecutorInvocationException>(() => InvokeAsync(
            new WorkspaceFileWorkflowExecutor(files, paths), settings, "{}"));
        Assert.Equal(WorkflowExecutorIds.StorageFile, failure.ExecutorId);
        Assert.True(File.Exists(original));
    }
}
