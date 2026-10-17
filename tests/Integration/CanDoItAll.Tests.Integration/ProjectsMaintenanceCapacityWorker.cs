using System.Diagnostics;
using System.Globalization;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Tests.Maintenance;

namespace CanDoItAll.Tests.Integration;

internal sealed class CapacityWorkerAccounting(Guid run, int worker) {
    private readonly object gate = new();
    private readonly List<MaintenanceHostFrame> frames = [];
    private long sequence;
    private bool started;
    private bool stopped;
    internal long ConfirmedRows { get; private set; }
    internal long ConfirmedBytes { get; private set; }

    internal void Accept(MaintenanceHostFrame frame) {
        lock (gate) {
            MaintenanceHostProtocol.ValidateObservation(frame);
            if (frame.RunId != run || frame.Worker != worker || frame.Sequence != sequence + 1 || stopped ||
                sequence == 0 && frame.Kind != MaintenanceFrameKind.Ready || sequence != 0 && frame.Kind == MaintenanceFrameKind.Ready ||
                frame.Kind == MaintenanceFrameKind.Start && started ||
                sequence != 0 && !started && frame.Kind != MaintenanceFrameKind.Start) {
                throw new InvalidDataException("The worker stream has lost, repeated or contradicted its owned sequence.");
            }
            sequence = frame.Sequence;
            started |= frame.Kind == MaintenanceFrameKind.Start;
            stopped = frame.Kind == MaintenanceFrameKind.Stopped;
            if (frame.Cycle is { } cycle) {
                ConfirmedRows = checked(ConfirmedRows + cycle.DeletedReceipts);
                ConfirmedBytes = checked(ConfirmedBytes + cycle.DeletedPayloadBytes);
            }
            frames.Add(frame);
        }
    }

    internal MaintenanceHostFrame[] Snapshot() {
        lock (gate) {
            return frames.ToArray();
        }
    }

    internal static (long Confirmed, long Removed, long UnconfirmedDifference) Reconcile(long confirmed, long before, long after) {
        if (confirmed < 0 || before < 0 || after < 0 || after > before || confirmed > before - after) {
            throw new InvalidDataException("Confirmed maintenance counts exceed independently observed removal.");
        }
        return (confirmed, before - after, before - after - confirmed);
    }
}

internal sealed class ProjectsMaintenanceCapacityWorker : IAsyncDisposable {
    private readonly TestApplication application;
    private readonly ProjectsMaintenanceCapacityResources resources;
    private readonly Guid run;
    private readonly int ordinal;
    private readonly int cadence;
    private readonly Guid profile;
    private readonly Process child;
    private readonly string assembly;
    private readonly TaskCompletionSource<MaintenanceHostFrame> ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource started = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private Task? output;
    private Task<int>? stderr;
    private bool launched;
    private bool stopSent;
    private bool disposed;
    private bool backendExitVerified;
    internal CapacityWorkerAccounting Accounting { get; }

    internal ProjectsMaintenanceCapacityWorker(TestApplication application, ProjectsMaintenanceCapacityResources resources,
        Guid profile, Guid run, int ordinal, int cadence) {
        this.application = application;
        this.resources = resources;
        this.profile = profile;
        this.run = run;
        this.ordinal = ordinal;
        this.cadence = cadence;
        Accounting = new(run, ordinal);
        assembly = Path.Combine(IntegrationTestPaths.RepositoryRoot, "tests", "Support", "CanDoItAll.ProjectsMaintenance.TestHost",
            "bin", IntegrationTestPaths.BuildConfiguration, "net10.0", "CanDoItAll.ProjectsMaintenance.TestHost.dll");
        if (!File.Exists(assembly)) {
            throw new FileNotFoundException("The maintenance child must be built in the standard configuration.", assembly);
        }
        var start = new ProcessStartInfo("dotnet") {
            WorkingDirectory = application.RootPath, UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true
        };
        foreach (var key in start.Environment.Keys.Where(key => new[] { "Database__", "ControlPlane__", "Storage__", "SecretVault__", "DataProtection__", "Kestrel__" }
                .Any(prefix => key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))).ToArray()) {
            start.Environment.Remove(key);
        }
        foreach (var pair in application.ActiveProfile.CreateEnvironmentVariables()) {
            start.Environment[pair.Key] = pair.Value;
        }
        start.Environment["Database__PasswordFile"] = string.Empty;
        start.Environment["DevelopmentManager__TuningModeEnabled"] = "false";
        start.Environment["DOTNET_ENVIRONMENT"] = "Development";
        start.Environment[MaintenanceHostProtocol.RootEnvironment] = application.RootPath;
        start.ArgumentList.Add(assembly);
        foreach (var (name, value) in new[] {
            ("--profile-id", profile.ToString("D")), ("--run-id", run.ToString("D")),
            ("--ordinal", ordinal.ToString(CultureInfo.InvariantCulture)), ("--interval-seconds", cadence.ToString(CultureInfo.InvariantCulture)),
            ("--warning-seconds", "120"), ("--lifetime-seconds", "1800")
        }) {
            start.ArgumentList.Add(name);
            start.ArgumentList.Add(value);
        }
        child = new() { StartInfo = start };
    }

    internal async Task LaunchAsync(DatasetActivationBinding binding, CancellationToken token) {
        await RecordAsync(CapacityResourcePhase.WorkerPrepared, token);
        if (!child.Start()) {
            throw new InvalidOperationException("The owned maintenance child could not start.");
        }
        launched = true;
        output = ReadAsync();
        stderr = DrainErrorAsync();
        await RecordAsync(CapacityResourcePhase.WorkerRunning, token);
        var frame = await ready.Task.WaitAsync(TimeSpan.FromSeconds(60), token);
        Assert.Equal(profile, frame.Profile);
        Assert.Equal(binding.Namespace.Value, frame.Namespace);
        Assert.Equal(binding.MaintenanceId, frame.Maintenance);
    }

    internal async Task StartAsync(CancellationToken token) {
        await SendAsync(MaintenanceFrameKind.Start, 1, token);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(10), token);
    }

    internal void ThrowIfFailed() {
        if (output is { IsFaulted: true }) {
            throw new InvalidDataException("The maintenance observation transport failed.", output.Exception);
        }
        if (launched && child.HasExited && !stopSent) {
            throw new InvalidOperationException($"The maintenance child exited before Stop with code {child.ExitCode}.");
        }
        if (Accounting.Snapshot().Any(frame => frame.Kind == MaintenanceFrameKind.Failure)) {
            throw new InvalidOperationException("The maintenance worker emitted an explicit failure observation.");
        }
    }

    internal async Task StopAsync(CancellationToken token) {
        if (!launched || stopSent) {
            return;
        }
        stopSent = true;
        if (!child.HasExited) {
            await SendAsync(MaintenanceFrameKind.Stop, 2, token);
            child.StandardInput.Close();
        }
        await child.WaitForExitAsync(token);
        if (output is not null) {
            await output;
        }
        Assert.Equal(0, child.ExitCode);
        Assert.Equal(0, await stderr!);
        var stopped = Accounting.Snapshot()[^1];
        Assert.Equal(MaintenanceFrameKind.Stopped, stopped.Kind);
        await new ProjectsMaintenanceCapacityStorage(application.ActiveProfile.ConnectionString).VerifyBackendExitAsync(stopped.BackendProcesses!, token);
        backendExitVerified = true;
        await RecordAsync(CapacityResourcePhase.WorkerExited, token);
    }

    private async Task ReadAsync() {
        try {
            while (await MaintenanceHostProtocol.ReadAsync(child.StandardOutput, CancellationToken.None) is { } frame) {
                Accounting.Accept(frame);
                if (frame.Kind == MaintenanceFrameKind.Ready) {
                    ready.TrySetResult(frame);
                } else if (frame.Kind == MaintenanceFrameKind.Start) {
                    started.TrySetResult();
                }
            }
            if (Accounting.Snapshot().LastOrDefault()?.Kind != MaintenanceFrameKind.Stopped) {
                throw new InvalidDataException("The maintenance transport ended without its terminal observation.");
            }
        } catch (Exception exception) {
            ready.TrySetException(exception);
            started.TrySetException(exception);
            throw;
        }
    }

    private async Task<int> DrainErrorAsync() {
        var buffer = new char[1024];
        var count = 0;
        int read;
        while ((read = await child.StandardError.ReadAsync(buffer.AsMemory())) != 0) {
            count = Math.Min(MaintenanceHostProtocol.MaximumFrameBytes, count + read);
        }
        return count;
    }

    private async Task SendAsync(MaintenanceFrameKind kind, long sequence, CancellationToken token) {
        var frame = new MaintenanceHostFrame(MaintenanceHostProtocol.Version, run, ordinal, sequence, kind, DateTimeOffset.UtcNow);
        MaintenanceHostProtocol.ValidateControl(frame);
        await child.StandardInput.WriteLineAsync(MaintenanceHostProtocol.Encode(frame).AsMemory(), token);
        await child.StandardInput.FlushAsync(token);
    }

    private Task RecordAsync(CapacityResourcePhase phase, CancellationToken token) => resources.RecordAsync(phase, new {
            Owner = nameof(ProjectsMaintenanceCapacityTests), Run = run, Worker = ordinal, CadenceSeconds = cadence,
            ParentProcessId = Environment.ProcessId, ChildProcessId = launched ? child.Id : (int?)null,
            StartedAtUtc = launched ? child.StartTime.ToUniversalTime() : (DateTime?)null,
            ExitCode = launched && child.HasExited ? child.ExitCode : (int?)null,
            BackendExitVerified = backendExitVerified,
            Profile = profile, ProfileKey = application.ActiveProfile.ProfileKey, Root = application.RootPath, Assembly = assembly
        }, token);

    public async ValueTask DisposeAsync() {
        if (disposed) {
            return;
        }
        disposed = true;
        Exception? failure = null;
        try {
            using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            await StopAsync(stop.Token);
        } catch (Exception exception) {
            failure = exception;
        } finally {
            try {
                if (launched && !child.HasExited) {
                    child.Kill(entireProcessTree: true);
                    using var killed = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                    await child.WaitForExitAsync(killed.Token);
                }
                if (output is not null) {
                    await output.WaitAsync(TimeSpan.FromSeconds(10));
                }
                if (stderr is not null) {
                    await stderr.WaitAsync(TimeSpan.FromSeconds(10));
                }
            } catch (Exception cleanup) {
                failure = failure is null ? cleanup : new AggregateException(failure, cleanup);
            } finally {
                try {
                    await RecordAsync(backendExitVerified ? CapacityResourcePhase.WorkerDisposed :
                        CapacityResourcePhase.WorkerDisposedWithIncompleteSessionEvidence, CancellationToken.None);
                } catch (Exception recording) {
                    failure = failure is null ? recording : new AggregateException(failure, recording);
                }
                child.Dispose();
            }
        }
        if (failure is not null) {
            throw failure;
        }
    }
}
