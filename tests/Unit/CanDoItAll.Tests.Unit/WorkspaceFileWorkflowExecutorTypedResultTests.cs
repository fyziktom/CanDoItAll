using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Workspace;

namespace CanDoItAll.Tests.Unit;

public sealed class WorkspaceFileWorkflowExecutorTypedResultTests
{
    [Fact]
    public async Task ExecuteAsync_FailedTypedResult_ThrowsExactMessage()
    {
        var workspaceRoot = CreateWorkspaceRoot();
        try
        {
            const string missingPath = "missing/source.md";
            var files = new WorkspaceFileService(workspaceRoot);
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
