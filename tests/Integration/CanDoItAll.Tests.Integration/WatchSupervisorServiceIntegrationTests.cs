using System.Net;
using System.Net.Http.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure;
using CanDoItAll.Manager;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace CanDoItAll.Tests.Integration;

[Trait("Category", "ManagerPortability")]
[Trait("Category", "UnixRuntimePortability")]
public sealed class WatchSupervisorServiceIntegrationTests
{
    [Fact]
    public async Task ProcessWatchLineAsync_confirms_runtime_readiness_with_matching_iteration()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Manager:AutoStartWatch"] = "false",
                ["Manager:ReadinessTimeoutSeconds"] = "1",
                ["Manager:ReadinessUrls:0"] = "http://127.0.0.1:5188/_dev/runtime"
            })
            .Build();

        var service = new WatchSupervisorService(
            NullLogger<WatchSupervisorService>.Instance,
            new FakeHttpClientFactory(new RuntimeProbeHandler(new RuntimeProbeSnapshot(true, "Ready", 1, ["http://127.0.0.1:5188"]))),
            configuration,
            new UnusedProcessCoordinator());

        await service.ProcessWatchLineAsync("Building");
        await service.ProcessWatchLineAsync("Now listening on: http://127.0.0.1:5188");

        var snapshot = await service.WaitForReadyAsync(0, TimeSpan.FromSeconds(2), CancellationToken.None);

        Assert.NotNull(snapshot);
        Assert.Equal(WatchState.Ready, snapshot!.State);
        Assert.Equal(1, snapshot.ExpectedWatchIteration);
        Assert.Equal(1, snapshot.ConfirmedWatchIteration);
    }

    [Fact]
    public async Task ProcessWatchLineAsync_keeps_multiple_listening_urls_on_the_same_startup_iteration()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Manager:AutoStartWatch"] = "false",
                ["Manager:ReadinessTimeoutSeconds"] = "1",
                ["Manager:ReadinessUrls:0"] = "https://localhost:7271/_dev/runtime"
            })
            .Build();

        var service = new WatchSupervisorService(
            NullLogger<WatchSupervisorService>.Instance,
            new FakeHttpClientFactory(new RuntimeProbeHandler(new RuntimeProbeSnapshot(true, "Ready", 1, ["https://localhost:7271", "http://localhost:5032"]))),
            configuration,
            new UnusedProcessCoordinator());

        await service.ProcessWatchLineAsync("Building");
        await service.ProcessWatchLineAsync("Now listening on: https://localhost:7271");
        await service.ProcessWatchLineAsync("Now listening on: http://localhost:5032");

        var snapshot = service.GetStatus();

        Assert.Equal(WatchState.Ready, snapshot.State);
        Assert.Equal(1, snapshot.ExpectedWatchIteration);
        Assert.Equal(1, snapshot.ConfirmedWatchIteration);
        Assert.Contains("https://localhost:7271", snapshot.ActiveUrls);
        Assert.Contains("http://localhost:5032", snapshot.ActiveUrls);
    }

    [Fact]
    public async Task ProcessWatchLineAsync_treats_stderr_progress_lines_as_non_error_logs()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Manager:AutoStartWatch"] = "false"
            })
            .Build();

        var service = new WatchSupervisorService(
            NullLogger<WatchSupervisorService>.Instance,
            new FakeHttpClientFactory(new RuntimeProbeHandler(new RuntimeProbeSnapshot(true, "Ready", 1, ["http://127.0.0.1:5188"]))),
            configuration,
            new UnusedProcessCoordinator());

        await service.ProcessWatchLineAsync("dotnet watch : Hot reload enabled. For a list of supported edits, see https://aka.ms/dotnet/hot-reload.", isError: true);

        var log = Assert.Single(service.GetLogs(1));
        Assert.False(log.IsError);
    }

    [Fact]
    public async Task ProcessWatchLineAsync_waits_for_a_listening_url_before_probing_runtime_readiness()
    {
        var handler = new RuntimeProbeHandler(new RuntimeProbeSnapshot(true, "Ready", 1, ["http://127.0.0.1:5188"]));
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Manager:AutoStartWatch"] = "false",
                ["Manager:ReadinessTimeoutSeconds"] = "1",
                ["Manager:ReadinessUrls:0"] = "http://127.0.0.1:5188/_dev/runtime"
            })
            .Build();

        var service = new WatchSupervisorService(
            NullLogger<WatchSupervisorService>.Instance,
            new FakeHttpClientFactory(handler),
            configuration,
            new UnusedProcessCoordinator());

        await service.ProcessWatchLineAsync("Building");
        await service.ProcessWatchLineAsync("dotnet watch : Waiting for changes", isError: true);

        Assert.Equal(0, handler.RequestCount);

        await service.ProcessWatchLineAsync("Now listening on: http://127.0.0.1:5188");

        Assert.Equal(1, handler.RequestCount);
        Assert.Equal(WatchState.Ready, service.GetStatus().State);
    }

    [Fact]
    public async Task Watch_shutdown_reclaims_registered_children_when_host_token_is_already_cancelled()
    {
        var coordinator = new RecordingProcessCoordinator();
        var service = new WatchSupervisorService(
            NullLogger<WatchSupervisorService>.Instance,
            new FakeHttpClientFactory(new RuntimeProbeHandler(new RuntimeProbeSnapshot(true, "Ready", 1, []))),
            CreateDisabledConfiguration(),
            coordinator);
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();

        await service.StopAsync(cancelled.Token);

        Assert.Equal([ManagerProcessPurpose.DotnetWatch], coordinator.ReclaimedPurposes);
        Assert.All(coordinator.ReclaimTokens, token => Assert.False(token.IsCancellationRequested));
    }

    [Fact]
    public async Task Tailwind_shutdown_reclaims_all_registered_children_when_host_token_is_already_cancelled()
    {
        var coordinator = new RecordingProcessCoordinator();
        var service = new TailwindWatchSupervisorService(
            NullLogger<TailwindWatchSupervisorService>.Instance,
            CreateDisabledConfiguration(),
            new PhysicalFileSystemPathPolicyFactory(),
            coordinator);
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();

        await service.StopAsync(cancelled.Token);

        Assert.Equal(
            [ManagerProcessPurpose.TailwindBuild, ManagerProcessPurpose.TailwindDependencyInstall],
            coordinator.ReclaimedPurposes);
        Assert.All(coordinator.ReclaimTokens, token => Assert.False(token.IsCancellationRequested));
    }

    [Fact]
    public async Task Tailwind_transient_build_failure_retries_the_same_fingerprint_until_publication_succeeds()
    {
        var workspaceRoot = Path.Combine(Path.GetTempPath(), $"CanDoItAll.Tailwind.Retry.{Guid.NewGuid():N}");
        var tailwindRoot = Path.Combine(workspaceRoot, "Tailwind");
        var outputPath = Path.Combine(workspaceRoot, "src", "App", "wwwroot", "css", "output.css");
        Directory.CreateDirectory(tailwindRoot);
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        Directory.CreateDirectory(Path.Combine(workspaceRoot, "src"));
        await File.WriteAllTextAsync(Path.Combine(tailwindRoot, "input.css"), "@import 'tailwindcss';");
        await File.WriteAllTextAsync(outputPath, "/* existing output */");
        try
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Manager:WorkspaceRoot"] = workspaceRoot,
                    ["Manager:TailwindWorkspacePath"] = "Tailwind",
                    ["Manager:TailwindInputPath"] = "Tailwind/input.css",
                    ["Manager:TailwindOutputPath"] = "src/App/wwwroot/css/output.css",
                    ["Manager:TailwindContentWatchPaths:0"] = "src",
                    ["Manager:TailwindInstallDependenciesIfMissing"] = "false",
                    ["Manager:TailwindWatchPollingMilliseconds"] = "250",
                    ["Manager:TailwindWatchDebounceMilliseconds"] = "50",
                    ["Manager:AutoStartWatch"] = "true",
                    ["Manager:AutoStartTailwindWatch"] = "true"
                })
                .Build();
            var coordinator = new SequenceProcessCoordinator(1, 0);
            var service = new TailwindWatchSupervisorService(
                NullLogger<TailwindWatchSupervisorService>.Instance,
                configuration,
                new PhysicalFileSystemPathPolicyFactory(),
                coordinator);

            await service.StartAsync(CancellationToken.None);
            var deadline = DateTimeOffset.UtcNow.AddSeconds(5);
            while ((coordinator.StartCount < 2 || service.GetStatus().State != TailwindWatchState.Ready) &&
                   DateTimeOffset.UtcNow < deadline)
            {
                await Task.Delay(50);
            }

            await service.StopAsync(CancellationToken.None);

            Assert.True(coordinator.StartCount >= 2);
            Assert.Equal(TailwindWatchState.Ready, service.GetStatus().State);
        }
        finally
        {
            Directory.Delete(workspaceRoot, recursive: true);
        }
    }

    private static IConfiguration CreateDisabledConfiguration()
        => new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Manager:AutoStartWatch"] = "false",
                ["Manager:AutoStartTailwindWatch"] = "false",
                ["Manager:CleanupWorkspaceProcessesOnStart"] = "true"
            })
            .Build();

    private sealed class FakeHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private sealed class RuntimeProbeHandler(RuntimeProbeSnapshot snapshot) : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(snapshot) });
        }
    }

    private sealed class UnusedProcessCoordinator : IManagerProcessCoordinator
    {
        public Task<IManagerProcessLease> StartAsync(
            ManagerProcessLaunchRequest request,
            CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("This test does not launch a process.");

        public Task<IReadOnlyList<CanDoItAll.AgentFramework.Core.WorkspaceProcessTerminationResult>> ReclaimRegisteredAsync(
            ManagerProcessPurpose purpose,
            string diagnosticCode,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<CanDoItAll.AgentFramework.Core.WorkspaceProcessTerminationResult>>([]);
    }

    private sealed class RecordingProcessCoordinator : IManagerProcessCoordinator
    {
        public List<ManagerProcessPurpose> ReclaimedPurposes { get; } = [];

        public List<CancellationToken> ReclaimTokens { get; } = [];

        public Task<IManagerProcessLease> StartAsync(
            ManagerProcessLaunchRequest request,
            CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("This test does not launch a process.");

        public Task<IReadOnlyList<WorkspaceProcessTerminationResult>> ReclaimRegisteredAsync(
            ManagerProcessPurpose purpose,
            string diagnosticCode,
            CancellationToken cancellationToken = default)
        {
            ReclaimedPurposes.Add(purpose);
            ReclaimTokens.Add(cancellationToken);
            return Task.FromResult<IReadOnlyList<WorkspaceProcessTerminationResult>>([]);
        }
    }

    private sealed class SequenceProcessCoordinator(params int[] exitCodes) : IManagerProcessCoordinator
    {
        private readonly Queue<int> remainingExitCodes = new(exitCodes);

        public int StartCount { get; private set; }

        public Task<IManagerProcessLease> StartAsync(
            ManagerProcessLaunchRequest request,
            CancellationToken cancellationToken = default)
        {
            StartCount++;
            var exitCode = remainingExitCodes.Count > 0 ? remainingExitCodes.Dequeue() : 0;
            return Task.FromResult<IManagerProcessLease>(new CompletedProcessLease(request, exitCode));
        }

        public Task<IReadOnlyList<WorkspaceProcessTerminationResult>> ReclaimRegisteredAsync(
            ManagerProcessPurpose purpose,
            string diagnosticCode,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<WorkspaceProcessTerminationResult>>([]);
    }

    private sealed class CompletedProcessLease : IManagerProcessLease
    {
        private readonly int exitCode;

        public CompletedProcessLease(ManagerProcessLaunchRequest request, int exitCode)
        {
            this.exitCode = exitCode;
            var now = DateTimeOffset.UtcNow;
            Record = new ManagerOwnedProcessRecord(
                Guid.NewGuid(),
                request.Purpose,
                new WorkspaceOwnedProcessIdentity(Environment.ProcessId, now, new string('a', 64)),
                "test-start",
                request.ExecutablePath,
                new string('b', 64),
                new string('c', 64),
                request.WorkspaceRoot,
                "test-owner",
                Environment.ProcessId,
                request.LeaseOwner,
                ManagerProcessLifecycleState.Running,
                now,
                now);
        }

        public ManagerOwnedProcessRecord Record { get; }

        public bool HasExited { get; private set; }

        public WorkspaceProcessOutputSnapshot CaptureOutput()
            => new(string.Empty, string.Empty, false, false);

        public Task<WorkspaceProcessExecutionResult> WaitForExitAsync(CancellationToken cancellationToken = default)
        {
            HasExited = true;
            var now = DateTimeOffset.UtcNow;
            return Task.FromResult(new WorkspaceProcessExecutionResult(
                true,
                exitCode,
                string.Empty,
                string.Empty,
                false,
                false,
                now,
                now,
                false,
                new ExecutionBoundaryDescriptor("test", "test", "test", "test", "test", true, "test"),
                string.Empty));
        }

        public Task<WorkspaceProcessTerminationResult> TerminateAsync(
            string diagnosticCode,
            CancellationToken cancellationToken = default)
        {
            HasExited = true;
            return Task.FromResult(new WorkspaceProcessTerminationResult(
                WorkspaceProcessTerminationStatus.Terminated,
                false,
                "terminated"));
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
