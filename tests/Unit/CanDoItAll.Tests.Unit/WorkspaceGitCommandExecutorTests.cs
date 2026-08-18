using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Git;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class WorkspaceGitCommandExecutorTests
{
    [Fact]
    public async Task ExecuteAsync_maps_typed_Git_spec_to_canonical_process_host()
    {
        var repositoryRoot = Path.GetTempPath();
        var host = new RecordingProcessHost(CreateResult(stdout: "git version test"));
        var executor = new WorkspaceGitCommandExecutor(host);
        var spec = new GitCommandSpec(
            new GitRepositoryPath(repositoryRoot),
            [new GitCommandArgument("status"), new GitCommandArgument("sensitive-value", IsSensitive: true)])
        {
            Timeout = TimeSpan.FromSeconds(7)
        };

        var result = await executor.ExecuteAsync(spec);

        Assert.True(result.Succeeded);
        Assert.NotNull(host.Request);
        Assert.Equal("workspace_git", host.Request.ToolName);
        Assert.Equal(["status", "sensitive-value"], host.Request.Arguments);
        Assert.Equal(7, host.Request.TimeoutSeconds);
        Assert.DoesNotContain("sensitive-value", result.SanitizedCommand, StringComparison.Ordinal);
        Assert.Contains("***", result.SanitizedCommand, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_maps_timeout_and_caller_cancellation()
    {
        var spec = new GitCommandSpec(
            new GitRepositoryPath(Path.GetTempPath()),
            [new GitCommandArgument("status")]);
        var timeoutExecutor = new WorkspaceGitCommandExecutor(
            new RecordingProcessHost(CreateResult(
                terminationReason: WorkspaceProcessTerminationReason.TimedOut,
                timedOut: true)));
        var cancellationExecutor = new WorkspaceGitCommandExecutor(
            new RecordingProcessHost(CreateResult(
                terminationReason: WorkspaceProcessTerminationReason.CallerCanceled)));

        await Assert.ThrowsAsync<TimeoutException>(() => timeoutExecutor.ExecuteAsync(spec));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            cancellationExecutor.ExecuteAsync(spec, new CancellationToken(canceled: true)));
    }

    [Fact]
    public void Git_foundation_has_no_independent_Process_implementation()
    {
        var root = FindRepositoryRoot();
        var gitRoot = Path.Combine(root, "src/Foundation/CanDoItAll.Git");
        var sources = Directory
            .EnumerateFiles(gitRoot, "*.cs", SearchOption.AllDirectories)
            .Select(File.ReadAllText)
            .ToArray();

        Assert.DoesNotContain(sources, source => source.Contains("ProcessStartInfo", StringComparison.Ordinal));
        Assert.DoesNotContain(sources, source => source.Contains("new Process", StringComparison.Ordinal));
        Assert.True(File.Exists(Path.Combine(
            root,
            "src/MAF/Common/CanDoItAll.AgentFramework.Core/Workspace/Commands/WorkspaceGitCommandExecutor.cs")));
    }

    private static WorkspaceProcessExecutionResult CreateResult(
        string stdout = "",
        WorkspaceProcessTerminationReason terminationReason = WorkspaceProcessTerminationReason.Completed,
        bool timedOut = false)
    {
        var now = DateTimeOffset.UtcNow;
        return new WorkspaceProcessExecutionResult(
            true,
            terminationReason == WorkspaceProcessTerminationReason.Completed ? 0 : -1,
            stdout,
            string.Empty,
            false,
            false,
            now,
            now.AddMilliseconds(10),
            timedOut,
            RecordingProcessHost.Boundary,
            string.Empty,
            terminationReason,
            false);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "CanDoItAll.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root was not found.");
    }

    private sealed class RecordingProcessHost(WorkspaceProcessExecutionResult result) : IWorkspaceProcessHost
    {
        public static readonly ExecutionBoundaryDescriptor Boundary = new(
            "Test",
            "Test",
            "Test",
            "Test",
            "Test",
            false,
            "Test");

        public WorkspaceProcessExecutionRequest? Request { get; private set; }

        public ExecutionBoundaryDescriptor DescribeBoundary() => Boundary;

        public Task<WorkspaceProcessExecutionResult> ExecuteAsync(
            WorkspaceProcessExecutionRequest request,
            CancellationToken cancellationToken = default)
        {
            Request = request;
            return Task.FromResult(result);
        }
    }
}
