using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Manager;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace CanDoItAll.Tests.Unit.Infrastructure;

public sealed class TailwindWatchSupervisorServiceTests
{
    private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task Watch_runs_the_npm_watch_script_with_open_input_and_reports_ready_after_the_first_build()
    {
        using var workspace = new TailwindWorkspace(withCli: true);
        var watch = new ScriptedLease();
        var coordinator = new ScriptedCoordinator(watch);
        var planner = new RecordingNpmPlanner();
        var service = CreateService(workspace, coordinator, planner);

        await service.StartAsync(CancellationToken.None);
        try
        {
            watch.WriteStandardError("≈ tailwindcss v4.3.3\n\nDone in 113ms\n");

            var status = await WaitForStatusAsync(service, snapshot => snapshot.State == TailwindWatchState.Ready);

            var request = Assert.Single(coordinator.Requests);
            Assert.Equal(ManagerProcessPurpose.TailwindWatch, request.Purpose);
            Assert.True(request.HoldStandardInputOpen);
            Assert.Equal(workspace.TailwindPath, request.WorkingDirectory);
            Assert.Equal(["npm-cli.js", "run", "watch"], request.Arguments);
            Assert.Equal([workspace.TailwindPath], planner.WorkingDirectories);
            Assert.Equal("npm run watch", status.WatchCommand);
            Assert.Equal(1, status.BuildCount);
            Assert.NotNull(status.LastBuildUtc);
            Assert.True(status.OutputExists);
            Assert.Contains("113ms", status.Summary, StringComparison.Ordinal);
            Assert.False(watch.HasExited);
        }
        finally
        {
            await service.StopAsync(CancellationToken.None);
        }

        Assert.True(watch.Terminated);
        Assert.Equal(TailwindWatchState.Stopped, service.GetStatus().State);
        Assert.Equal(
            [ManagerProcessPurpose.TailwindWatch, ManagerProcessPurpose.TailwindDependencyInstall],
            coordinator.ReclaimedPurposes);
    }

    [Fact]
    public async Task Watch_reports_a_failed_rebuild_and_recovers_on_the_next_successful_build()
    {
        using var workspace = new TailwindWorkspace(withCli: true);
        var watch = new ScriptedLease();
        var service = CreateService(workspace, new ScriptedCoordinator(watch), new RecordingNpmPlanner());

        await service.StartAsync(CancellationToken.None);
        try
        {
            watch.WriteStandardError("Done in 100ms\n");
            await WaitForStatusAsync(service, snapshot => snapshot.State == TailwindWatchState.Ready);

            watch.WriteStandardError("Error:\n┌\n│ CssSyntaxError: input.css:3:1: Missing closing } at .broken\n└\nDone in 4s\n");
            var failed = await WaitForStatusAsync(
                service,
                snapshot => snapshot.State == TailwindWatchState.Faulted &&
                            snapshot.Summary.StartsWith("Tailwind build failed", StringComparison.Ordinal));

            watch.WriteStandardError("Done in 5ms\n");
            var recovered = await WaitForStatusAsync(service, snapshot => snapshot.State == TailwindWatchState.Ready);

            Assert.Contains("CssSyntaxError: input.css:3:1: Missing closing } at .broken", failed.Summary, StringComparison.Ordinal);
            Assert.Contains("keeps running", failed.Summary, StringComparison.Ordinal);
            Assert.Equal(2, recovered.BuildCount);
            Assert.False(watch.HasExited);
            Assert.Contains(service.GetLogs(), entry => entry.IsError && entry.Line.StartsWith("CssSyntaxError", StringComparison.Ordinal));
        }
        finally
        {
            await service.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task Watch_restarts_after_the_process_exits_and_reports_the_exit_cause()
    {
        using var workspace = new TailwindWorkspace(withCli: true);
        var failedWatch = new ScriptedLease();
        var replacementWatch = new ScriptedLease();
        var coordinator = new ScriptedCoordinator(failedWatch, replacementWatch);
        var restartPolicy = new TailwindWatchRestartPolicy(
            TimeSpan.FromMilliseconds(750),
            TimeSpan.FromSeconds(1),
            TimeSpan.FromMinutes(1));
        var service = CreateService(workspace, coordinator, new RecordingNpmPlanner(), restartPolicy);

        await service.StartAsync(CancellationToken.None);
        try
        {
            failedWatch.WriteStandardError("npm error Missing script: \"watch\"\nnpm error\n");
            failedWatch.Exit(1);
            var exited = await WaitForStatusAsync(
                service,
                snapshot => snapshot.Summary.StartsWith("Tailwind watch exited", StringComparison.Ordinal));

            replacementWatch.WriteStandardError("Done in 90ms\n");
            var ready = await WaitForStatusAsync(service, snapshot => snapshot.State == TailwindWatchState.Ready);

            Assert.Equal(TailwindWatchState.Faulted, exited.State);
            Assert.Contains("code 1", exited.Summary, StringComparison.Ordinal);
            Assert.Contains("npm error Missing script: \"watch\"", exited.Summary, StringComparison.Ordinal);
            Assert.Contains("Restarting in 0.8 s", exited.Summary, StringComparison.Ordinal);
            Assert.Equal(2, coordinator.Requests.Count);
            Assert.Equal(1, ready.RestartCount);
        }
        finally
        {
            await service.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task Watch_is_restarted_immediately_when_its_output_capture_is_exhausted()
    {
        using var workspace = new TailwindWorkspace(withCli: true);
        var exhaustedWatch = new ScriptedLease();
        var replacementWatch = new ScriptedLease();
        var coordinator = new ScriptedCoordinator(exhaustedWatch, replacementWatch);
        var service = CreateService(workspace, coordinator, new RecordingNpmPlanner());

        await service.StartAsync(CancellationToken.None);
        try
        {
            exhaustedWatch.WriteStandardError("Done in 100ms\n");
            exhaustedWatch.MarkOutputTruncated();
            replacementWatch.WriteStandardError("Done in 90ms\n");

            var ready = await WaitForStatusAsync(
                service,
                snapshot => snapshot.State == TailwindWatchState.Ready && snapshot.RestartCount == 1);

            Assert.True(exhaustedWatch.Terminated);
            Assert.Equal(2, coordinator.Requests.Count);
            Assert.Equal(1, ready.BuildCount);
        }
        finally
        {
            await service.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task Missing_tailwind_cli_faults_without_starting_npm_when_installation_is_disabled()
    {
        using var workspace = new TailwindWorkspace(withCli: false);
        var coordinator = new ScriptedCoordinator();
        var service = CreateService(workspace, coordinator, new RecordingNpmPlanner());

        await service.StartAsync(CancellationToken.None);
        try
        {
            var status = await WaitForStatusAsync(service, snapshot => snapshot.State == TailwindWatchState.Faulted);

            Assert.Contains("npm install", status.Summary, StringComparison.Ordinal);
            Assert.Empty(coordinator.Requests);
        }
        finally
        {
            await service.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task Missing_tailwind_cli_is_installed_before_the_watch_starts()
    {
        using var workspace = new TailwindWorkspace(withCli: false);
        var install = new ScriptedLease(onStart: () => workspace.InstallCli());
        var watch = new ScriptedLease();
        var coordinator = new ScriptedCoordinator(install, watch);
        var planner = new RecordingNpmPlanner();
        var service = CreateService(workspace, coordinator, planner, installDependencies: true);

        await service.StartAsync(CancellationToken.None);
        try
        {
            install.Exit(0);
            watch.WriteStandardError("Done in 120ms\n");

            await WaitForStatusAsync(service, snapshot => snapshot.State == TailwindWatchState.Ready);

            Assert.Equal(
                [ManagerProcessPurpose.TailwindDependencyInstall, ManagerProcessPurpose.TailwindWatch],
                coordinator.Requests.Select(request => request.Purpose));
            Assert.Equal(["npm-cli.js", "install"], coordinator.Requests[0].Arguments);
            Assert.False(coordinator.Requests[0].HoldStandardInputOpen);
        }
        finally
        {
            await service.StopAsync(CancellationToken.None);
        }
    }

    private static TailwindWatchSupervisorService CreateService(
        TailwindWorkspace workspace,
        IManagerProcessCoordinator coordinator,
        RecordingNpmPlanner planner,
        TailwindWatchRestartPolicy? restartPolicy = null,
        bool installDependencies = false)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Manager:WorkspaceRoot"] = workspace.RootPath,
                ["Manager:AutoStartWatch"] = "true",
                ["Manager:AutoStartTailwindWatch"] = "true",
                ["Manager:TailwindWorkspacePath"] = "Tailwind",
                ["Manager:TailwindOutputPath"] = "src/App/wwwroot/css/output.css",
                ["Manager:TailwindInstallDependenciesIfMissing"] = installDependencies ? "true" : "false",
                ["Manager:TailwindEchoOutputToConsole"] = "false"
            })
            .Build();
        return new TailwindWatchSupervisorService(
            NullLogger<TailwindWatchSupervisorService>.Instance,
            configuration,
            coordinator,
            planner.Plan,
            restartPolicy ?? TailwindWatchRestartPolicy.Default);
    }

    private static async Task<TailwindWatchStatusSnapshot> WaitForStatusAsync(
        TailwindWatchSupervisorService service,
        Func<TailwindWatchStatusSnapshot, bool> predicate)
    {
        var deadline = DateTimeOffset.UtcNow.Add(WaitTimeout);
        while (DateTimeOffset.UtcNow < deadline)
        {
            var status = service.GetStatus();
            if (predicate(status))
            {
                return status;
            }

            await Task.Delay(20);
        }

        var last = service.GetStatus();
        throw new TimeoutException($"Tailwind status did not reach the expected state. Last state: {last.State}; summary: {last.Summary}");
    }

    private sealed class RecordingNpmPlanner
    {
        public List<string> WorkingDirectories { get; } = [];

        public ManagerExecutablePlan Plan(string workingDirectory, IReadOnlyList<string> npmArguments)
        {
            WorkingDirectories.Add(workingDirectory);
            return new ManagerExecutablePlan("node", ["npm-cli.js", .. npmArguments]);
        }
    }

    private sealed class ScriptedCoordinator(params ScriptedLease[] leases) : IManagerProcessCoordinator
    {
        private readonly Queue<ScriptedLease> remainingLeases = new(leases);

        public List<ManagerProcessLaunchRequest> Requests { get; } = [];

        public List<ManagerProcessPurpose> ReclaimedPurposes { get; } = [];

        public Task<IManagerProcessLease> StartAsync(
            ManagerProcessLaunchRequest request,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            if (!remainingLeases.TryDequeue(out var lease))
            {
                throw new InvalidOperationException("The test scripted no further Manager processes.");
            }

            lease.Start(request);
            return Task.FromResult<IManagerProcessLease>(lease);
        }

        public Task<IReadOnlyList<WorkspaceProcessTerminationResult>> ReclaimRegisteredAsync(
            ManagerProcessPurpose purpose,
            string diagnosticCode,
            CancellationToken cancellationToken = default)
        {
            ReclaimedPurposes.Add(purpose);
            return Task.FromResult<IReadOnlyList<WorkspaceProcessTerminationResult>>([]);
        }
    }

    private sealed class ScriptedLease(Action? onStart = null) : IManagerProcessLease
    {
        private readonly object gate = new();
        private readonly TaskCompletionSource<int> exit = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private string standardError = string.Empty;
        private bool outputTruncated;
        private ManagerOwnedProcessRecord? record;

        public ManagerOwnedProcessRecord Record => record ?? throw new InvalidOperationException("The scripted lease was not started.");

        public bool HasExited => exit.Task.IsCompleted;

        public bool Terminated { get; private set; }

        public void Start(ManagerProcessLaunchRequest request)
        {
            var now = DateTimeOffset.UtcNow;
            record = new ManagerOwnedProcessRecord(
                Guid.NewGuid(),
                request.Purpose,
                new VerifiedManagerProcessTerminationAuthority(
                    new WorkspaceOwnedProcessIdentity(
                        4_242,
                        now,
                        new string('a', 64),
                        new WorkspaceOwnedProcessBoundary(
                            WorkspaceOwnedProcessBoundaryKind.UnixProcessGroup,
                            4_242,
                            Guid.Empty))),
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
            onStart?.Invoke();
        }

        public void WriteStandardError(string text)
        {
            lock (gate)
            {
                standardError += text;
            }
        }

        public void MarkOutputTruncated()
        {
            lock (gate)
            {
                outputTruncated = true;
            }
        }

        public void Exit(int exitCode)
            => exit.TrySetResult(exitCode);

        // Mirrors the process host, whose captured text is trimmed.
        public WorkspaceProcessOutputSnapshot CaptureOutput()
        {
            lock (gate)
            {
                return new WorkspaceProcessOutputSnapshot(string.Empty, standardError.Trim(), false, outputTruncated);
            }
        }

        public async Task<WorkspaceProcessExecutionResult> WaitForExitAsync(CancellationToken cancellationToken = default)
        {
            var exitCode = await exit.Task.WaitAsync(cancellationToken);
            return CreateResult(exitCode);
        }

        public Task<WorkspaceProcessTerminationResult> TerminateAsync(
            string diagnosticCode,
            CancellationToken cancellationToken = default)
        {
            Terminated = true;
            exit.TrySetResult(-1);
            return Task.FromResult(new WorkspaceProcessTerminationResult(
                WorkspaceProcessTerminationStatus.Terminated,
                false,
                "terminated"));
        }

        public ValueTask DisposeAsync()
        {
            exit.TrySetResult(-1);
            return ValueTask.CompletedTask;
        }

        private static WorkspaceProcessExecutionResult CreateResult(int exitCode)
        {
            var now = DateTimeOffset.UtcNow;
            return new WorkspaceProcessExecutionResult(
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
                string.Empty);
        }
    }

    private sealed class TailwindWorkspace : IDisposable
    {
        public TailwindWorkspace(bool withCli)
        {
            RootPath = Path.Combine(Path.GetTempPath(), "CanDoItAll.Manager.TailwindWatch", Guid.NewGuid().ToString("N"));
            TailwindPath = Path.Combine(RootPath, "Tailwind");
            Directory.CreateDirectory(TailwindPath);
            var outputPath = Path.Combine(RootPath, "src", "App", "wwwroot", "css", "output.css");
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            File.WriteAllText(outputPath, "/* tailwind output */");
            if (withCli)
            {
                InstallCli();
            }
        }

        public string RootPath { get; }

        public string TailwindPath { get; }

        public void InstallCli()
        {
            var cliPath = WorkspaceRuntimeProcessTools.ResolveTailwindCliShimPath(TailwindPath);
            Directory.CreateDirectory(Path.GetDirectoryName(cliPath)!);
            File.WriteAllText(cliPath, string.Empty);
        }

        public void Dispose()
        {
            if (Directory.Exists(RootPath))
            {
                Directory.Delete(RootPath, recursive: true);
            }
        }
    }
}
