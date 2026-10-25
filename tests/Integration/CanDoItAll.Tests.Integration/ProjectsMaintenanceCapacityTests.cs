using System.Diagnostics;
using System.Globalization;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Text.Json;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Search;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Tests.Maintenance;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace CanDoItAll.Tests.Integration;

public sealed class ProjectsMaintenanceCapacityTests(ITestOutputHelper output) {
    private const string WorkloadVersion = "protected-projects-maintenance-v2";
    private const string ApprovedCapacityEnvironment = "CANDOITALL_TESTS_MAINTENANCE_CAPACITY_APPROVED_BYTES";

    [Theory]
    [InlineData(60, 1)]
    [InlineData(60, 2)]
    [InlineData(5, 1)]
    [InlineData(5, 2)]
    [Trait("Category", "LongRunning")]
    [Trait("Category", "LiveProcess")]
    [Trait("Category", "HostPlatform")]
    public async Task Measure_sustained_arrivals_with_scheduled_maintenance_and_storage_reuse(int cadence, int workerCount) {
        var approved = ReadApprovedCapacity();
        using var rowDeadline = new CancellationTokenSource(TimeSpan.FromMinutes(65));
        foreach (var mixed in new[] { false, true }) {
            using var cellDeadline = CancellationTokenSource.CreateLinkedTokenSource(rowDeadline.Token);
            cellDeadline.CancelAfter(TimeSpan.FromMinutes(30));
            await CellAsync(cadence, workerCount, mixed, approved, cellDeadline.Token);
        }
    }

    private async Task CellAsync(int cadence, int workerCount, bool mixed, long approvedBytes, CancellationToken token) {
        var resources = await ProjectsMaintenanceCapacityResources.CreateAsync(cadence, workerCount, mixed, approvedBytes, token);
        CanDoItAllTestEnvironment? environment = null;
        TestApplication? app = null;
        Exception? failure = null;
        var probe = new CapacityCommandProbe();
        try {
            environment = resources.CreateEnvironment();
            await resources.RecordEnvironmentAsync(environment, token);
            var activeProfile = resources.CreateProfile(environment);
            await resources.RecordDatabaseAsync(activeProfile, token);
            app = await TestApplication.CreateAsync(new TestHarnessOptions {
                TestEnvironment = environment, ActiveProfile = activeProfile,
                ConfigureServices = services => {
                    services.ConfigureDbContext<ProjectsDbContext>(options => options.AddInterceptors(probe), ServiceLifetime.Singleton);
                    services.ConfigureDbContext<SearchDbContext>(options => options.AddInterceptors(probe), ServiceLifetime.Singleton);
                }
            });
            await resources.RecordAsync(CapacityResourcePhase.ApplicationReady, new { Root = app.RootPath }, token);
            await MeasureCellAsync(app, resources, probe, cadence, workerCount, mixed, approvedBytes, token);
        } catch (Exception exception) {
            failure = exception;
        } finally {
            failure = await resources.CleanupAsync(app, environment, failure);
        }
        if (failure is not null) {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }

    private async Task MeasureCellAsync(TestApplication app, ProjectsMaintenanceCapacityResources resources, CapacityCommandProbe probe,
        int cadence, int workerCount, bool mixed, long approvedBytes, CancellationToken token) {
        var run = resources.Run;
        var profile = app.Services.GetRequiredService<ICanonicalRuntimeDatabase>().Profile.Profile;
        var binding = await app.Services.GetRequiredService<DatasetActivationService>().ReadActiveBindingAsync(profile.Id, token);
        var storage = new ProjectsMaintenanceCapacityStorage(app.ActiveProfile.ConnectionString);
        var load = new ProjectsMaintenanceCapacityLoad(app, mixed);
        await PhaseAsync(180, token, async phase => await load.InitializeAsync(phase));
        var small = await storage.PayloadSizeAsync(load.Small.Request.Operation.Id, token);
        var large = await storage.PayloadSizeAsync(load.Large.Request.Operation.Id, token);
        Assert.Equal(load.Small.CanonicalPayload.Length, small);
        Assert.Equal(load.Large.CanonicalPayload.Length, large);
        var seed = ProjectsMaintenanceCapacityStorage.Plan(mixed, small, large);
        var minimumPhysicalAllowance = checked(4 * ProjectsMaintenanceCapacityStorage.MaximumRawBytes + 1024L * 1024 * 1024);
        Assert.True(approvedBytes >= minimumPhysicalAllowance,
            "The fixture operator must record and approve at least 17GiB of free physical/WAL headroom before this lane seeds data.");
        await resources.RecordAsync(CapacityResourcePhase.WorkloadDeclared, new {
            Owner = nameof(ProjectsMaintenanceCapacityTests), Run = run, Root = app.RootPath, Profile = profile.Id,
            ProfileKey = app.ActiveProfile.ProfileKey, SeedNamespace = seed.Namespace, seed.TotalRows, seed.TotalBytes,
            ApprovedPhysicalBytes = approvedBytes, MaintenanceWorkers = workerCount, CadenceSeconds = cadence
        }, token);
        Write("environment", new {
            Run = run, Mixed = mixed, CadenceSeconds = cadence, Workers = workerCount, Seed = seed,
            ApprovedPhysicalBytes = approvedBytes, Runtime = RuntimeInformation.FrameworkDescription,
            OperatingSystem = RuntimeInformation.OSDescription, Environment.ProcessorCount,
            ServerGc = System.Runtime.GCSettings.IsServerGC, Configuration = IntegrationTestPaths.BuildConfiguration,
            Caller = "Synthetic LocalWorkspace; no governed source or HTTP cost", Persistence = "Actual protected writer, Search and receipt",
            Scheduling = "Actual BackgroundService in independent child processes; measuring provider hosted services are not started"
        });
        await PhaseAsync(180, token, async phase => {
            await storage.SeedAsync(seed, load.Small.Request.Operation.Id, load.Large.Request.Operation.Id, phase);
            await storage.AnalyzeAsync(phase);
        });
        var activeBefore = await storage.CountAsync(load.Namespace, token);
        var warm = await PhaseAsync(60, token, phase => load.RunAsync(30, phase));
        var controlBefore = await storage.ObserveAsync(token);
        probe.Drain();
        var control = await PhaseAsync(150, token, phase => load.RunAsync(120, phase));
        Write("foreground-control", new { Result = control, ObservedCommands = probe.Drain(), Before = controlBefore,
            After = await storage.ObserveAsync(token), ForcedVacuum = false });

        var workers = Enumerable.Range(1, workerCount).Select(ordinal => new ProjectsMaintenanceCapacityWorker(app, resources, profile.Id, run, ordinal, cadence)).ToArray();
        CapacityWindow? measured = null;
        CapacityReceiptCount? residual = null;
        CapacityStorageSnapshot? before = null;
        DateTimeOffset? anchor = null;
        Exception? primary = null;
        try {
            await Task.WhenAll(workers.Select(worker => worker.LaunchAsync(binding, token)));
            before = await storage.ObserveAsync(token);
            anchor = await storage.ScheduleAsync(seed, token);
            var wait = anchor.Value - DateTimeOffset.UtcNow;
            Assert.InRange(wait.TotalSeconds, 0, 5);
            await Task.Delay(wait, token);
            await Task.WhenAll(workers.Select(worker => worker.StartAsync(token)));
            probe.Drain();
            using var observing = CancellationTokenSource.CreateLinkedTokenSource(token);
            var observations = ObserveDuringAsync(storage, workers, observing.Token);
            var foreground = PhaseAsync(390, observing.Token, phase => load.RunAsync(360, phase));
            Exception? observationFailure = null;
            try {
                var completed = await Task.WhenAny(foreground, observations);
                if (completed == observations) {
                    await observations;
                    throw new InvalidOperationException("The periodic observation ended before the foreground workload.");
                }
                measured = await foreground;
                Write("foreground-with-maintenance", new { Result = measured, ObservedCommands = probe.Drain() });
                await WaitHealthyProcessesAsync(workers, TimeSpan.FromSeconds(120), token);
            } catch (Exception exception) {
                observationFailure = exception;
            } finally {
                observing.Cancel();
                try {
                    await Task.WhenAll(foreground, observations);
                } catch (Exception cleanup) {
                    observationFailure = observationFailure is null ? cleanup : new AggregateException(observationFailure, cleanup);
                }
            }
            if (observationFailure is not null) {
                throw observationFailure;
            }
            using var stop = CancellationTokenSource.CreateLinkedTokenSource(token);
            stop.CancelAfter(TimeSpan.FromSeconds(15));
            await Task.WhenAll(workers.Select(worker => worker.StopAsync(stop.Token)));
            residual = await storage.CountAsync(seed.Namespace, token);
            var activeAfter = await storage.CountAsync(load.Namespace, token);
            AssertAcceptedRows(activeBefore, activeAfter, warm, control, measured);
            await load.VerifyOriginalReplayAsync(token);
            Assert.Equal(activeAfter, await storage.CountAsync(load.Namespace, token));
            var confirmedRows = workers.Sum(worker => worker.Accounting.ConfirmedRows);
            var confirmedBytes = workers.Sum(worker => worker.Accounting.ConfirmedBytes);
            var rows = CapacityWorkerAccounting.Reconcile(confirmedRows, seed.TotalRows, residual.Rows);
            var bytes = CapacityWorkerAccounting.Reconcile(confirmedBytes, seed.TotalBytes, residual.Bytes);
            var streams = workers.Select(worker => worker.Accounting.Snapshot()).ToArray();
            Write("scheduled-accounting", new { Rows = rows, Bytes = bytes, Before = before, After = await storage.ObserveAsync(token),
                Residual = residual, Streams = streams, EpochReadinessAtExpiry = "Not measured; default admission does not expire in this lane" });
            Write("qualification", Qualify(seed, workerCount, cadence, anchor.Value, control, measured, streams,
                rows.UnconfirmedDifference == 0 && bytes.UnconfirmedDifference == 0));
        } catch (Exception exception) {
            primary = exception;
            Write("incomplete-cell", new { FailureType = exception.GetType().Name, Measured = measured, Residual = residual,
                PartialStreams = workers.Select(worker => worker.Accounting.Snapshot()).ToArray(), CompleteAccounting = false });
        } finally {
            foreach (var worker in workers.Reverse()) {
                try {
                    await worker.DisposeAsync();
                } catch (Exception cleanup) {
                    primary = primary is null ? cleanup : new AggregateException(primary, cleanup);
                }
            }
        }
        if (primary is not null) {
            throw primary;
        }

        await PhaseAsync(300, token, async phase => {
            Write("before-explicit-synthetic-cleanup", new { Count = await storage.CountAsync(seed.Namespace, phase), Storage = await storage.ObserveAsync(phase) });
            await storage.ClearSyntheticAsync(seed.Namespace, load.Namespace, phase);
            await VacuumObservationAsync(storage, "initial", phase);
            for (var round = 1; round <= 3; round++) {
                var refill = ProjectsMaintenanceCapacityStorage.Plan(mixed, small, large, ProjectsMaintenanceCapacityStorage.ReuseRows, 0);
                var refillBefore = await storage.ObserveAsync(phase);
                await storage.SeedAsync(refill, load.Small.Request.Operation.Id, load.Large.Request.Operation.Id, phase);
                var inserted = await storage.CountAsync(refill.Namespace, phase);
                var refillAfter = await storage.ObserveAsync(phase);
                await storage.ClearSyntheticAsync(refill.Namespace, load.Namespace, phase);
                Write("quiescent-refill", new { Round = round, Declared = refill, Inserted = inserted, Before = refillBefore, After = refillAfter,
                    AfterDelete = await storage.ObserveAsync(phase), ScheduledCapacity = false });
                await VacuumObservationAsync(storage, $"reuse-{round}", phase);
            }
        });
        if (!mixed) {
            await PhaseAsync(480, token, phase => PressureAsync(app, resources, storage, load, binding, profile.Id, cadence, workerCount, phase));
        }
        await load.VerifyOriginalReplayAsync(token);
        Write("cell-cleanup", new { Run = run, ChildProcessesExited = true, SyntheticNamespacesRemoved = true,
            DatabaseLeaseCleanup = "Provider then owned environment disposal follows; persistent cleanup manifests retain failures" });
    }

    private async Task PressureAsync(TestApplication app, ProjectsMaintenanceCapacityResources resources,
        ProjectsMaintenanceCapacityStorage storage, ProjectsMaintenanceCapacityLoad load,
        DatasetActivationBinding binding, Guid profile, int cadence, int workerCount, CancellationToken token) {
        var seed = ProjectsMaintenanceCapacityStorage.Plan(false, load.Small.CanonicalPayload.Length, load.Large.CanonicalPayload.Length, 1, 0);
        await storage.SeedAsync(seed, load.Small.Request.Operation.Id, load.Large.Request.Operation.Id, token);
        await using var receipt = await storage.LockAsync(seed.Namespace, false, token);
        var run = Guid.NewGuid();
        var workers = Enumerable.Range(1, workerCount).Select(ordinal => new ProjectsMaintenanceCapacityWorker(app, resources, profile, run, ordinal, cadence)).ToArray();
        Exception? failure = null;
        try {
            await Task.WhenAll(workers.Select(worker => worker.LaunchAsync(binding, token)));
            await Task.WhenAll(workers.Select(worker => worker.StartAsync(token)));
            await UntilAsync(() => workers.All(worker => worker.Accounting.Snapshot().Any(frame => frame.Health == MaintenanceHealth.NoProgress)), workers, token);
            await using (var state = await storage.LockAsync(load.Namespace, true, token)) {
                var lockedAt = DateTimeOffset.UtcNow;
                await receipt.DisposeAsync();
                await UntilAsync(() => workers.All(worker => worker.Accounting.Snapshot().Any(frame =>
                    frame.Cycle is { Stop: MaintenanceStop.Contention, Eligibility: MaintenanceEligibility.NoneObserved } cycle &&
                    cycle.CompletedAtUtc > lockedAt)), workers, token);
                Assert.DoesNotContain(workers.SelectMany(worker => worker.Accounting.Snapshot()), frame => frame.Health == MaintenanceHealth.Recovery);
            }
            await UntilAsync(() => workers.All(worker => worker.Accounting.Snapshot().Any(frame => frame.Health == MaintenanceHealth.Recovery)), workers, token);
            using var stop = CancellationTokenSource.CreateLinkedTokenSource(token);
            stop.CancelAfter(TimeSpan.FromSeconds(15));
            await Task.WhenAll(workers.Select(worker => worker.StopAsync(stop.Token)));
            Assert.Equal(0, (await storage.CountAsync(seed.Namespace, token)).Rows);
            Assert.Equal(1, workers.Sum(worker => worker.Accounting.ConfirmedRows));
            Write("pressure-busy-recovery", new { Streams = workers.Select(worker => worker.Accounting.Snapshot()).ToArray(),
                WarningSeconds = 120, BusyLaterBacklog = "Unknown; absence of already-retired receipts is not overall empty work",
                Recovery = "Only after non-Busy Empty observation", AdmissionExpiry = "Not measured" });
        } catch (Exception exception) {
            failure = exception;
            Write("incomplete-pressure", new { FailureType = exception.GetType().Name,
                PartialStreams = workers.Select(worker => worker.Accounting.Snapshot()).ToArray(), CompleteAccounting = false });
        } finally {
            foreach (var worker in workers.Reverse()) {
                try {
                    await worker.DisposeAsync();
                } catch (Exception cleanup) {
                    failure = failure is null ? cleanup : new AggregateException(failure, cleanup);
                }
            }
        }
        if (failure is not null) {
            throw failure;
        }
        await storage.ClearSyntheticAsync(seed.Namespace, load.Namespace, token);
    }

    private async Task ObserveDuringAsync(ProjectsMaintenanceCapacityStorage storage, ProjectsMaintenanceCapacityWorker[] workers,
        CancellationToken token) {
        try {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
            while (await timer.WaitForNextTickAsync(token)) {
                foreach (var worker in workers) {
                    worker.ThrowIfFailed();
                }
                Write("periodic-storage", new { Observation = await storage.ObserveAsync(token),
                    Counters = workers.Select(worker => new { worker.Accounting.ConfirmedRows, worker.Accounting.ConfirmedBytes }).ToArray(),
                    ExactBacklogCount = "Not scanned during foreground; confirmed counters can lag unacknowledged commits" });
            }
        } catch (OperationCanceledException) when (token.IsCancellationRequested) {
        }
    }

    private async Task VacuumObservationAsync(ProjectsMaintenanceCapacityStorage storage, string round, CancellationToken token) {
        var before = await storage.ObserveAsync(token);
        var started = Stopwatch.GetTimestamp();
        await storage.VacuumAsync(token);
        Write("quiescent-vacuum", new { Round = round, Milliseconds = Stopwatch.GetElapsedTime(started).TotalMilliseconds,
            Before = before, After = await storage.ObserveAsync(token), ForegroundAndWorkersStopped = true,
            Interpretation = "Ordinary vacuum/reuse; allocated-file shrinkage is not required" });
    }

    private static void AssertAcceptedRows(CapacityReceiptCount before, CapacityReceiptCount after, params CapacityWindow[] windows) {
        Assert.Equal(before.Rows + windows.Sum(window => window.Accepted), after.Rows);
        Assert.All(windows, window => {
            Assert.Equal(window.Offered, window.QueueRejected + window.Accepted + window.Replayed + window.Rejected + window.Unconfirmed + window.Faults);
            Assert.Equal(0, window.Replayed);
            Assert.Equal(0, window.Rejected);
            Assert.Equal(0, window.Unconfirmed);
            Assert.Equal(0, window.Faults);
        });
    }

    private static object Qualify(CapacitySeed seed, int workers, int cadence, DateTimeOffset anchor, CapacityWindow control,
        CapacityWindow measured, MaintenanceHostFrame[][] streams, bool exactConfirmation) {
        var cycles = streams.SelectMany(stream => stream).Where(frame => frame.Cycle is not null).Select(frame => frame.Cycle!)
            .OrderBy(cycle => cycle.CompletedAtUtc).ToArray();
        var groups = cycles.GroupBy(cycle => Math.Max(0, (int)((cycle.CompletedAtUtc - anchor).TotalSeconds / cadence))).OrderBy(group => group.Key).ToArray();
        long confirmed = 0;
        var unexhausted = new List<object>();
        foreach (var group in groups) {
            var seconds = Math.Clamp((int)(group.Max(cycle => cycle.CompletedAtUtc) - anchor).TotalSeconds - 5, 0, seed.Seconds);
            confirmed += group.Sum(cycle => cycle.DeletedReceipts);
            var eligibleLowerBound = seed.InitialRows + seconds * seed.ArrivalsPerSecond - confirmed;
            if (group.Key > 0 && group.Count() == workers && eligibleLowerBound > 0 && exactConfirmation &&
                group.All(cycle => cycle.Eligibility == MaintenanceEligibility.Present)) {
                unexhausted.Add(new { Group = group.Key, Rows = group.Sum(cycle => cycle.DeletedReceipts),
                    Bytes = group.Sum(cycle => cycle.DeletedPayloadBytes), EligibleLowerBound = eligibleLowerBound });
            }
        }
        var tails = control.EndToEndLatency is { } baseline && measured.EndToEndLatency is { } candidate &&
            candidate.P95 <= baseline.P95 * 1.20 + 5 && candidate.P99 <= baseline.P99 * 1.25 + 10 && candidate.P99 < 1000;
        var foreground = measured.QueueRejected == 0 && measured.Accepted == measured.Offered;
        return new {
            ForegroundRateMet = foreground, ProposedTailThresholdMet = tails,
            ConservativeClockAllowanceSeconds = 5,
            UnexhaustedGroups = unexhausted, MinimumThreeUnexhaustedGroupsObserved = unexhausted.Count >= 3,
            ExactConfirmation = exactConfirmation, Qualified = false,
            Reason = "Parent must compare two reversed-order repetitions, late backlog trend and 20% recovery headroom; this row alone cannot qualify a deployment.",
            ProductionDefaultSeconds = 60, EmptyWindowsAreCapacityMaxima = false
        };
    }

    private static async Task WaitHealthyProcessesAsync(ProjectsMaintenanceCapacityWorker[] workers, TimeSpan duration, CancellationToken token) {
        var started = Stopwatch.GetTimestamp();
        while (Stopwatch.GetElapsedTime(started) < duration) {
            foreach (var worker in workers) {
                worker.ThrowIfFailed();
            }
            await Task.Delay(TimeSpan.FromSeconds(1), token);
        }
    }

    private static async Task UntilAsync(Func<bool> condition, ProjectsMaintenanceCapacityWorker[] workers, CancellationToken token) {
        while (!condition()) {
            foreach (var worker in workers) {
                worker.ThrowIfFailed();
            }
            await Task.Delay(TimeSpan.FromMilliseconds(200), token);
        }
    }

    private static async Task PhaseAsync(int seconds, CancellationToken token, Func<CancellationToken, Task> action) {
        using var phase = CancellationTokenSource.CreateLinkedTokenSource(token);
        phase.CancelAfter(TimeSpan.FromSeconds(seconds));
        await action(phase.Token);
    }

    private static async Task<T> PhaseAsync<T>(int seconds, CancellationToken token, Func<CancellationToken, Task<T>> action) {
        using var phase = CancellationTokenSource.CreateLinkedTokenSource(token);
        phase.CancelAfter(TimeSpan.FromSeconds(seconds));
        return await action(phase.Token);
    }

    private static long ReadApprovedCapacity() {
        var text = Environment.GetEnvironmentVariable(ApprovedCapacityEnvironment);
        if (!long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var bytes) || bytes <= 0) {
            throw new InvalidOperationException($"Set {ApprovedCapacityEnvironment} only after recording the owned PostgreSQL fixture's measured free-space/headroom approval.");
        }
        return bytes;
    }

    private void Write(string kind, object record) => output.WriteLine(JsonSerializer.Serialize(new { WorkloadVersion, Kind = kind, Record = record }));
}
