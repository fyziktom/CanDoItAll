using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Workspace;
using CanDoItAll.Infrastructure.FileSystem;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed partial class WorkflowWorkspaceProviderReadEvidenceTests {
    [Theory]
    [InlineData(WorkflowStorageFileOperation.List, ExactTargetChange.None)]
    [InlineData(WorkflowStorageFileOperation.List, ExactTargetChange.Remove)]
    [InlineData(WorkflowStorageFileOperation.List, ExactTargetChange.Reparse)]
    [InlineData(WorkflowStorageFileOperation.ListDirectory, ExactTargetChange.None)]
    [InlineData(WorkflowStorageFileOperation.ListDirectory, ExactTargetChange.Remove)]
    [InlineData(WorkflowStorageFileOperation.ListDirectory, ExactTargetChange.Reparse)]
    [InlineData(WorkflowStorageFileOperation.Tree, ExactTargetChange.None)]
    [InlineData(WorkflowStorageFileOperation.Tree, ExactTargetChange.Remove)]
    [InlineData(WorkflowStorageFileOperation.Tree, ExactTargetChange.Reparse)]
    [InlineData(WorkflowStorageFileOperation.SearchText, ExactTargetChange.None)]
    [InlineData(WorkflowStorageFileOperation.SearchText, ExactTargetChange.Remove)]
    [InlineData(WorkflowStorageFileOperation.SearchText, ExactTargetChange.Reparse)]
    public async Task Collection_disclosure_uses_the_exact_native_file_even_when_legacy_display_has_a_live_decoy(
        WorkflowStorageFileOperation operation, ExactTargetChange change) {
        var original = Write(Path.Combine("sources", NativeLiteralName), "needle original");
        var decoy = Write(Path.Combine("sources", "literal", "name.txt"), "needle decoy");
        Write(Path.Combine("sources", "ignored.txt"), "ignored");
        var (files, paths) = Services();
        var (node, read, payload) = await InvokeAsync(new WorkspaceFileWorkflowExecutor(files, paths),
            new WorkflowStorageFileExecutorSettings { Operation = operation, Path = "sources", Query = "needle", IncludeGlobs = ["**/*name.txt"] }, "{}");
        var targets = ReadTargets(read);
        Assert.Contains(original, targets);
        if (!OperatingSystem.IsWindows()) {
            Assert.Contains("sources/literal/name.txt", payload, StringComparison.Ordinal);
            Assert.Contains('\\', Path.GetFileName(original));
        }
        Assert.DoesNotContain("readSelection", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("fullPath", payload, StringComparison.OrdinalIgnoreCase);
        await WorkflowWorkspaceProviderReadEvidence.RequireCurrentAsync(read, node, paths.ExecutionScope, TestWorkspaceServices.PhysicalPathPolicyFactory);
        if (change != ExactTargetChange.None) {
            File.Delete(original);
            if (change == ExactTargetChange.Reparse) {
                File.CreateSymbolicLink(original, decoy);
            }
            try {
                Assert.True(File.Exists(decoy));
                await Assert.ThrowsAsync<PhysicalPathValidationException>(() => WorkflowWorkspaceProviderReadEvidence.RequireCurrentAsync(
                    read, node, paths.ExecutionScope, TestWorkspaceServices.PhysicalPathPolicyFactory).AsTask());
            } finally {
                if (change == ExactTargetChange.Reparse) {
                    File.Delete(original);
                }
            }
        }
    }

    [Theory]
    [InlineData(WorkflowStorageFileOperation.ReadText)]
    [InlineData(WorkflowStorageFileOperation.Hash)]
    [InlineData(WorkflowStorageFileOperation.DiffText)]
    [InlineData(WorkflowStorageFileOperation.Exists)]
    [InlineData(WorkflowStorageFileOperation.Stat)]
    public async Task Successful_fixed_native_path_read_requires_the_original_file_after_its_permissive_precheck(WorkflowStorageFileOperation operation) {
        var original = Write(Path.Combine("sources", NativeLiteralName), "needle original");
        var decoy = Write(Path.Combine("sources", "literal", "name.txt"), "needle decoy");
        var (files, paths) = Services();
        var (node, read, _) = await InvokeAsync(new WorkspaceFileWorkflowExecutor(files, paths),
            new WorkflowStorageFileExecutorSettings { Operation = operation, Path = original, DestinationPath = decoy }, "{}");
        Assert.Contains(read.Evidence.Select(item => JsonSerializer.Deserialize<WorkflowWorkspaceReadPart>(item.PayloadJson,
            WorkflowProviderDisclosureContent.JsonOptions)!), item => item.Target is { AllowMissing: false } target && target.FullPath == original);
        await WorkflowWorkspaceProviderReadEvidence.RequireCurrentAsync(read, node, paths.ExecutionScope, TestWorkspaceServices.PhysicalPathPolicyFactory);
        File.Delete(original);
        Assert.True(File.Exists(decoy));
        await Assert.ThrowsAsync<PhysicalPathValidationException>(() => WorkflowWorkspaceProviderReadEvidence.RequireCurrentAsync(
            read, node, paths.ExecutionScope, TestWorkspaceServices.PhysicalPathPolicyFactory).AsTask());
    }

    [Fact]
    public async Task Missing_exists_result_retains_its_original_missing_target_without_requiring_a_new_file() {
        var (files, paths) = Services();
        var (node, read, payload) = await InvokeAsync(new WorkspaceFileWorkflowExecutor(files, paths),
            new WorkflowStorageFileExecutorSettings { Operation = WorkflowStorageFileOperation.Exists, Path = "missing.txt" }, "{}");
        Assert.False(WorkflowExecutorJson.Deserialize<WorkspacePathStatResult>(payload).Exists);
        Assert.All(read.Evidence.Select(item => JsonSerializer.Deserialize<WorkflowWorkspaceReadPart>(item.PayloadJson,
            WorkflowProviderDisclosureContent.JsonOptions)!).Where(item => item.Target is not null), item => Assert.True(item.Target!.AllowMissing));
        await WorkflowWorkspaceProviderReadEvidence.RequireCurrentAsync(read, node, paths.ExecutionScope, TestWorkspaceServices.PhysicalPathPolicyFactory);
    }

    [Theory]
    [InlineData(ExactSelectionFault.Missing)]
    [InlineData(ExactSelectionFault.Count)]
    [InlineData(ExactSelectionFault.Root)]
    [InlineData(ExactSelectionFault.Origin)]
    public void Protected_collection_capture_rejects_missing_or_misaligned_live_owner_selection(ExactSelectionFault fault) {
        Write("original.txt", "needle");
        var (files, paths) = Services();
        var result = files.ListFiles();
        Assert.NotEmpty(result.Entries);
        var settings = new WorkflowStorageFileExecutorSettings { Operation = WorkflowStorageFileOperation.List, Path = "." };
        var shape = new WorkflowValueShape(WorkflowValueShapeKind.Object, "{}", "Original list");
        var executor = new WorkspaceFileWorkflowExecutor(files, paths);
        var node = new WorkflowNode(new("list"), WorkflowNodeKind.Executor, "List", [], new(null, null, null, null, "", shape, shape) {
            ExecutorId = executor.Descriptor.Id, ExecutorSettingsJson = WorkflowExecutorJson.Serialize(settings)
        });
        var definition = new WorkflowDefinition(WorkflowId.New(), WorkflowVersionId.New(), "Original list", "", WorkflowLifecycleStatus.Draft,
            new(node.Id, [node], []), new(WorkflowRuntimeBackendKind.InProcess, true, false, false, false), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        var occurrence = WorkflowExecutionOccurrence.Start(WorkflowRunId.New()).Advance(definition.VersionId, node.Id);
        var input = new WorkflowNodeInput("{}");
        using var audit = WorkflowExecutorExecutionAuditScope.Push(occurrence.RunId);
        using var invocation = WorkflowExecutorExecutionAuditScope.PushInvocation(definition, node, input, occurrence, WorkflowProviderDisclosureProtocol.Current);
        var context = new WorkflowExecutorExecutionContext(definition, node, executor.Descriptor, node.Settings.ExecutorSettingsJson!, executor.Descriptor.DefaultPolicy) {
            ExecutionOccurrence = occurrence
        };
        var capture = WorkflowWorkspaceReadCapture.Begin(context, input, () => paths.ExecutionScope)!;
        capture.CapturePath(paths, settings.Path, allowMissing: true);
        var selection = result.ReadSelection!;
        var malformed = result with { ReadSelection = fault switch {
            ExactSelectionFault.Missing => null,
            ExactSelectionFault.Count => new(root, true, []),
            ExactSelectionFault.Root => new(Path.Combine(root, "different"), selection.IsWorkspacePath, selection.GetSelectedPaths()),
            ExactSelectionFault.Origin => new(selection.GetRootPath(), !selection.IsWorkspacePath, selection.GetSelectedPaths()),
            _ => throw new ArgumentOutOfRangeException(nameof(fault))
        } };
        Assert.Throws<InvalidOperationException>(() => capture.CaptureFileResult(paths, malformed, settings));
    }

    [Fact]
    public void Private_physical_selection_is_absent_from_public_json_and_safe_result_diagnostics() {
        var original = Write("original.txt", "needle");
        var (files, _) = Services();
        var result = files.ListFiles();
        Assert.Contains(original, result.ReadSelection!.GetSelectedPaths());
        Assert.DoesNotContain(root, result.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(root, result.ReadSelection.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(root, JsonSerializer.Serialize(result.ReadSelection), StringComparison.Ordinal);
        var json = WorkflowExecutorJson.Serialize(result);
        Assert.DoesNotContain("readSelection", json, StringComparison.OrdinalIgnoreCase);
        Assert.Null(WorkflowExecutorJson.Deserialize<WorkspaceFileListResult>(json).ReadSelection);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Owner_sorting_and_truncation_preserve_each_selected_path_with_its_own_result(bool search) {
        Write(Path.Combine("sources", NativeLiteralName), "needle needle needle original");
        Write(Path.Combine("sources", "literal", "name.txt"), "needle decoy");
        var (files, _) = Services();
        if (search) {
            var result = files.SearchText("needle", "sources", maxResults: 1);
            var match = Assert.Single(result.Matches);
            var selected = Assert.Single(result.ReadSelection!.GetSelectedPaths());
            Assert.Contains("original", match.Snippet, StringComparison.Ordinal);
            Assert.Contains("original", File.ReadAllText(selected), StringComparison.Ordinal);
            Assert.True(result.IsTruncated);
        } else {
            var result = files.ListFiles("sources", maxResults: 1);
            var entry = Assert.Single(result.Entries);
            var selected = Assert.Single(result.ReadSelection!.GetSelectedPaths());
            Assert.Equal(entry.SizeBytes, new FileInfo(selected).Length);
            Assert.True(result.IsTruncated);
        }
    }

    private static string[] ReadTargets(WorkflowCompletedNodeRead read) => read.Evidence.Select(item =>
        JsonSerializer.Deserialize<WorkflowWorkspaceReadPart>(item.PayloadJson, WorkflowProviderDisclosureContent.JsonOptions)!.Target)
        .OfType<WorkflowWorkspaceReadTarget>().Select(item => item.FullPath).ToArray();

    private static string NativeLiteralName => OperatingSystem.IsWindows() ? "literal-name.txt" : "literal\\name.txt";
    public enum ExactTargetChange { None, Remove, Reparse }
    public enum ExactSelectionFault { Missing, Count, Root, Origin }
}
