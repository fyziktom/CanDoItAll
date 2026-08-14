using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Tools.Abstractions;
using CanDoItAll.Modules.AgentFramework;

namespace CanDoItAll.Tests.Unit;

public sealed class WorkspaceExternalProcessRunnerTests
{
    [Fact]
    public async Task RunAsync_maps_external_request_to_canonical_host_and_bounds_UTF8_output()
    {
        var host = new RecordingProcessHost(CreateResult(stdout: "éé", stderr: "error"));
        var runner = CreateRunner(host);
        var request = new ExternalProcessRunRequest(
            ExecutablePath: Environment.ProcessPath!,
            Arguments: ["--json"],
            WorkingDirectory: Path.GetTempPath(),
            Timeout: TimeSpan.FromMilliseconds(1200),
            StandardInput: "{\"input\":true}",
            MaxOutputBytes: 3,
            CorrelationId: "external-portability",
            AllowedExecutableNames: new HashSet<string>
            {
                Path.GetFileName(Environment.ProcessPath!)
            });

        var result = await runner.RunAsync(request, CancellationToken.None);

        Assert.NotNull(host.Request);
        Assert.Equal("external_process", host.Request.ToolName);
        Assert.Equal(request.StandardInput, host.Request.StandardInput);
        Assert.Equal(2, host.Request.TimeoutSeconds);
        Assert.DoesNotContain(
            host.Request.EnvironmentVariables.Keys,
            name => name.Contains("KEY", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("TOKEN", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("SECRET", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("é", result.Stdout);
        Assert.Equal("err", result.Stderr);
    }

    [Fact]
    public async Task RunAsync_maps_timeout_without_a_second_process_implementation()
    {
        var host = new RecordingProcessHost(CreateResult(
            terminationReason: WorkspaceProcessTerminationReason.TimedOut,
            timedOut: true));
        var runner = CreateRunner(host);

        await Assert.ThrowsAsync<TimeoutException>(() =>
            runner.RunAsync(CreateRequest(), CancellationToken.None));
    }

    [Fact]
    public async Task RunAsync_maps_caller_cancellation()
    {
        var host = new RecordingProcessHost(CreateResult(
            terminationReason: WorkspaceProcessTerminationReason.CallerCanceled));
        var runner = CreateRunner(host);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            runner.RunAsync(CreateRequest(), cancellation.Token));
    }

    [Fact]
    public async Task RunAsync_fails_when_process_tree_termination_is_unconfirmed()
    {
        var host = new RecordingProcessHost(CreateResult(
            terminationReason: WorkspaceProcessTerminationReason.TerminationFailed,
            residualProcessPossible: true));
        var runner = CreateRunner(host);

        var exception = await Assert.ThrowsAsync<ExternalProcessResidualProcessException>(() =>
            runner.RunAsync(CreateRequest(), CancellationToken.None));

        Assert.Contains("residual process", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RunAsync_authorizes_the_resolved_executable_identity_before_launch()
    {
        var host = new RecordingProcessHost(CreateResult());
        var runner = CreateRunner(host);
        var request = CreateRequest() with
        {
            AllowedExecutableNames = new HashSet<string> { "different-executable" }
        };

        await Assert.ThrowsAsync<ExternalProcessCommandPolicyException>(
            () => runner.RunAsync(request, CancellationToken.None));

        Assert.Null(host.Request);
    }

    [Fact]
    public void External_process_composition_has_one_low_level_process_implementation()
    {
        var root = FindRepositoryRoot();
        var invoker = File.ReadAllText(Path.Combine(
            root,
            "src/MAF/Tools/CanDoItAll.AgentFramework.Tools/External/ExternalProcessToolInvoker.cs"));
        var adapter = File.ReadAllText(Path.Combine(
            root,
            "src/Modules/CanDoItAll.Modules.AgentFramework/Services/WorkspaceExternalProcessRunner.cs"));
        var composition = File.ReadAllText(Path.Combine(
            root,
            "src/Modules/CanDoItAll.Modules.AgentFramework/Services/AgentFrameworkModuleServiceCollectionExtensions.cs"));
        var aliasSession = File.ReadAllText(Path.Combine(
            root,
            "src/MAF/Common/CanDoItAll.AgentFramework.Core/Workspace/Paths/WorkspacePathAliasSession.cs"));

        Assert.DoesNotContain("ProcessStartInfo", invoker, StringComparison.Ordinal);
        Assert.DoesNotContain("LocalExternalProcessRunner", invoker, StringComparison.Ordinal);
        Assert.Contains("IWorkspaceProcessHost", adapter, StringComparison.Ordinal);
        Assert.DoesNotContain("System.Diagnostics", adapter, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessStartInfo", aliasSession, StringComparison.Ordinal);
        Assert.Contains("IWorkspaceProcessHost", aliasSession, StringComparison.Ordinal);
        Assert.Contains(
            "TryAddScoped<IExternalProcessRunner, WorkspaceExternalProcessRunner>()",
            composition,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Windows_path_alias_uses_canonical_host_with_typed_arguments_and_cleanup()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var workspaceRoot = Path.Combine(Path.GetTempPath(), new string('a', 150));
        var host = new AliasRecordingProcessHost();
        var pathPolicy = TestWorkspaceServices.CreatePathPolicy(
            workspaceRoot,
            externalTargetRegistry: TestExternalTargetPathRegistry.Create());

        await using (var session = await WorkspacePathAliasSession.TryCreateAsync(
                         workspaceRoot,
                         workspaceRoot,
                         [],
                         pathPolicy,
                         host,
                         new Dictionary<string, string?>(),
                         CancellationToken.None))
        {
            Assert.NotNull(session);
            var create = Assert.Single(host.Requests);
            Assert.EndsWith("subst.exe", create.ExecutablePath, StringComparison.OrdinalIgnoreCase);
            Assert.Equal("workspace_path_alias_create", create.RecipeId);
            Assert.Equal(2, create.Arguments.Count);
            Assert.EndsWith(":", create.Arguments[0], StringComparison.Ordinal);
            Assert.Equal(Path.GetFullPath(workspaceRoot), create.Arguments[1]);
        }

        Assert.Equal(2, host.Requests.Count);
        Assert.Equal("workspace_path_alias_delete", host.Requests[1].RecipeId);
        Assert.Equal("/d", host.Requests[1].Arguments[1]);
    }

    private static ExternalProcessRunRequest CreateRequest()
        => new(
            Environment.ProcessPath!,
            [],
            Path.GetTempPath(),
            TimeSpan.FromSeconds(1),
            string.Empty,
            4096,
            "external-portability",
            new HashSet<string> { Path.GetFileName(Environment.ProcessPath!) });

    private static WorkspaceExternalProcessRunner CreateRunner(IWorkspaceProcessHost host)
        => new(host, new FixedPathResolver());

    private static WorkspaceProcessExecutionResult CreateResult(
        string stdout = "{}",
        string stderr = "",
        WorkspaceProcessTerminationReason terminationReason = WorkspaceProcessTerminationReason.Completed,
        bool timedOut = false,
        bool residualProcessPossible = false)
    {
        var now = DateTimeOffset.UtcNow;
        return new WorkspaceProcessExecutionResult(
            Started: true,
            ExitCode: terminationReason == WorkspaceProcessTerminationReason.Completed ? 0 : -1,
            Stdout: stdout,
            Stderr: stderr,
            StdoutTruncated: false,
            StderrTruncated: false,
            StartedAtUtc: now,
            CompletedAtUtc: now.AddMilliseconds(10),
            TimedOut: timedOut,
            Boundary: RecordingProcessHost.Boundary,
            FailureMessage: string.Empty,
            TerminationReason: terminationReason,
            ResidualProcessPossible: residualProcessPossible);
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

    private sealed class FixedPathResolver : IWorkspacePathResolutionService
    {
        public WorkspaceResolvedPath ResolveFilePath(string path, bool allowMissing)
            => Resolve(path);

        public WorkspaceResolvedPath ResolveDirectoryPath(string path, bool allowMissing)
            => Resolve(path);

        private static WorkspaceResolvedPath Resolve(string path)
            => new(Path.GetFullPath(path), path, IsWorkspacePath: true);
    }

    private sealed class AliasRecordingProcessHost : IWorkspaceProcessHost
    {
        public List<WorkspaceProcessExecutionRequest> Requests { get; } = [];

        public ExecutionBoundaryDescriptor DescribeBoundary() => RecordingProcessHost.Boundary;

        public Task<WorkspaceProcessExecutionResult> ExecuteAsync(
            WorkspaceProcessExecutionRequest request,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return Task.FromResult(CreateResult());
        }
    }
}
