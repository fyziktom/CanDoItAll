using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Workspace;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class WorkspaceFileWorkflowExecutorTypedResultTests
{
    [Fact]
    public async Task ExecuteAsync_Exists_PreservesAKnownMissingObservation()
    {
        var workspaceRoot = CreateWorkspaceRoot();
        try
        {
            var executor = new WorkspaceFileWorkflowExecutor(TestWorkspaceServices.CreateFileService(workspaceRoot));

            var result = await ExecuteAsync(
                executor,
                new WorkflowStorageFileExecutorSettings
                {
                    Operation = WorkflowStorageFileOperation.Exists,
                    Path = "missing/source.md"
                });

            var stat = WorkflowExecutorJson.Deserialize<WorkspacePathStatResult>(result.PayloadJson);
            Assert.False(stat.Succeeded);
            Assert.False(stat.Exists);
            Assert.True(stat.IsKnownMissing());
        }
        finally
        {
            DeleteDirectory(workspaceRoot);
        }
    }

    [Fact]
    public async Task ExecuteAsync_DryRunDelete_PreservesAKnownMissingObservation()
    {
        var workspaceRoot = CreateWorkspaceRoot();
        try
        {
            var executor = new WorkspaceFileWorkflowExecutor(TestWorkspaceServices.CreateFileService(workspaceRoot));

            var result = await ExecuteAsync(
                executor,
                new WorkflowStorageFileExecutorSettings
                {
                    Operation = WorkflowStorageFileOperation.Delete,
                    Path = "missing/source.md",
                    DryRun = true
                });

            Assert.Contains("\"dryRun\":true", result.PayloadJson, StringComparison.Ordinal);
            Assert.Contains("\"exists\":false", result.PayloadJson, StringComparison.Ordinal);
        }
        finally
        {
            DeleteDirectory(workspaceRoot);
        }
    }

    [Fact]
    public async Task ExecuteAsync_Exists_DoesNotTreatADeniedPathAsMissing()
    {
        var workspaceRoot = CreateWorkspaceRoot();
        try
        {
            var executor = new WorkspaceFileWorkflowExecutor(TestWorkspaceServices.CreateFileService(workspaceRoot));

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => ExecuteAsync(
                executor,
                new WorkflowStorageFileExecutorSettings
                {
                    Operation = WorkflowStorageFileOperation.Exists,
                    Path = "../outside.md"
                }));

            Assert.False(string.IsNullOrWhiteSpace(exception.Message));
        }
        finally
        {
            DeleteDirectory(workspaceRoot);
        }
    }

    [Fact]
    public async Task ExecuteAsync_FailedTypedResult_ThrowsExactMessage()
    {
        var workspaceRoot = CreateWorkspaceRoot();
        try
        {
            const string missingPath = "missing/source.md";
            var files = TestWorkspaceServices.CreateFileService(workspaceRoot);
            var failedResult = files.ReadTextFile(missingPath);
            var executor = new WorkspaceFileWorkflowExecutor(files);
            var context = new WorkflowExecutorExecutionContext(
                Definition: null!,
                Node: null!,
                Descriptor: executor.Descriptor,
                SettingsJson: WorkflowExecutorJson.Serialize(new WorkflowStorageFileExecutorSettings
                {
                    Operation = WorkflowStorageFileOperation.ReadText,
                    Path = missingPath
                }),
                Policy: WorkflowExecutorExecutionPolicy.Default);

            Assert.False(failedResult.Succeeded);
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => executor.ExecuteAsync(
                context,
                new WorkflowNodeInput("{}")).AsTask());

            Assert.Equal(failedResult.Message, exception.Message);
        }
        finally
        {
            DeleteDirectory(workspaceRoot);
        }
    }

    [Fact]
    public void WorkspaceFileResults_ImplementStronglyTypedOperationContract()
    {
        AssertOperationResult<WorkspaceFileListResult>();
        AssertOperationResult<WorkspaceTextSearchResult>();
        AssertOperationResult<WorkspaceTextFileReadResult>();
        AssertOperationResult<WorkspacePathStatResult>();
        AssertOperationResult<WorkspacePathHashResult>();
        AssertOperationResult<WorkspaceFileMutationResult>();
        AssertOperationResult<WorkspaceArchiveMutationResult>();
        AssertOperationResult<WorkspaceTextDiffResult>();
    }

    [Fact]
    public void WorkspaceFileExecutor_DoesNotInspectResultsWithReflection()
    {
        var sourcePath = Path.Combine(
            FindRepositoryRoot(),
            "src",
            "MAF",
            "WorkflowExecutors",
            "Standard",
            "CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Workspace",
            "WorkspaceFileWorkflowExecutor.cs");
        var source = File.ReadAllText(sourcePath);

        Assert.DoesNotContain("GetProperty(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("GetValue(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("System.Reflection", source, StringComparison.Ordinal);
        Assert.Contains("where T : IWorkspaceToolOperationResult", source, StringComparison.Ordinal);
    }

    private static void AssertOperationResult<T>()
        where T : IWorkspaceToolOperationResult
    {
    }

    private static async Task<WorkflowNodeExecutionResult> ExecuteAsync(
        WorkspaceFileWorkflowExecutor executor,
        WorkflowStorageFileExecutorSettings settings)
    {
        var settingsJson = WorkflowExecutorJson.Serialize(settings);
        var node = new WorkflowNode(
            new WorkflowNodeId("workspace-file"),
            WorkflowNodeKind.Executor,
            "workspace-file",
            [],
            new WorkflowNodeSettings(
                ComponentId: null,
                AgentId: null,
                SubworkflowId: null,
                ExternalRequestKind: null,
                Instructions: string.Empty,
                InputShape: executor.Descriptor.InputShape,
                ResultShape: executor.Descriptor.ResultShape)
            {
                ExecutorId = executor.Descriptor.Id,
                ExecutorSettingsJson = settingsJson
            });
        var context = new WorkflowExecutorExecutionContext(
            Definition: null!,
            Node: node,
            Descriptor: executor.Descriptor,
            SettingsJson: settingsJson,
            Policy: WorkflowExecutorExecutionPolicy.Default);

        return await executor.ExecuteAsync(context, new WorkflowNodeInput("{}"));
    }

    private static string CreateWorkspaceRoot()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            "candoitall-workspace-file-executor-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void DeleteDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CanDoItAll.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not find repository root.");
    }
}
