using System.Text.Json;
using System.Text.Json.Nodes;
using CanDoItAll.Tests.Maintenance;

namespace CanDoItAll.Tests.Integration;

public sealed class ProjectsMaintenanceCapacityAccountingTests {
    [Fact]
    public async Task Worker_confirmation_streams_do_not_double_count_or_convert_missing_evidence_to_empty() {
        var run = Guid.NewGuid();
        var first = new CapacityWorkerAccounting(run, 1);
        var second = new CapacityWorkerAccounting(run, 2);
        var now = DateTimeOffset.UtcNow;
        foreach (var (worker, accounting) in new[] { (1, first), (2, second) }) {
            accounting.Accept(Frame(worker, 1, MaintenanceFrameKind.Ready));
            accounting.Accept(Frame(worker, 2, MaintenanceFrameKind.Start));
            accounting.Accept(Frame(worker, 3, MaintenanceFrameKind.CycleObservation,
                new(4, 40, MaintenanceStop.Budget, MaintenanceEligibility.Unknown, 2001, null, null, null, now)));
        }
        Assert.Equal((8L, 9L, 1L), CapacityWorkerAccounting.Reconcile(first.ConfirmedRows + second.ConfirmedRows, 10, 1));
        Assert.Throws<InvalidDataException>(() => first.Accept(Frame(1, 3, MaintenanceFrameKind.CycleObservation,
            new(4, 40, MaintenanceStop.Empty, MaintenanceEligibility.NoneObserved, 1, 0, null, null, now))));
        Assert.Throws<InvalidDataException>(() => first.Accept(Frame(1, 5, MaintenanceFrameKind.Stopped)));
        Assert.Throws<InvalidDataException>(() => first.Accept(Frame(2, 4, MaintenanceFrameKind.Stopped)));
        Assert.Throws<InvalidDataException>(() => CapacityWorkerAccounting.Reconcile(10, 10, 1));

        var metrics = new MaintenanceMetricSequence();
        Assert.Null(metrics.Begin(0));
        Assert.Throws<InvalidDataException>(() => metrics.Complete());
        metrics.SetBytes(0);
        metrics.SetCycle(1, MaintenanceStop.Contention, MaintenanceEligibility.Unknown);
        metrics.SetDuration(1, MaintenanceStop.Contention, MaintenanceEligibility.Unknown);
        var observed = metrics.Complete()!;
        Assert.Equal(0, observed.DeletedReceipts);
        Assert.Equal(MaintenanceEligibility.Unknown, observed.Eligibility);
        Assert.Null(observed.EligibleObserved);
        Assert.Null(metrics.Complete());
        Assert.Null(metrics.Begin(0));
        metrics.SetBytes(0);
        metrics.SetCycle(1, MaintenanceStop.Empty, MaintenanceEligibility.NoneObserved);
        metrics.SetDuration(1, MaintenanceStop.Empty, MaintenanceEligibility.NoneObserved);
        Assert.Throws<InvalidDataException>(() => metrics.Complete());
        metrics.SetOptional(MaintenanceHostProtocol.Eligible, 0);
        Assert.Equal(0, metrics.Complete()!.EligibleObserved);

        var ready = Frame(1, 1, MaintenanceFrameKind.Ready);
        var cycle = Frame(1, 4, MaintenanceFrameKind.CycleObservation,
            new(0, 0, MaintenanceStop.Contention, MaintenanceEligibility.Unknown, 1, null, null, null, now));
        foreach (var invalid in new[] {
            ready with { Profile = null }, ready with { Namespace = Guid.Empty },
            Frame(1, 2, MaintenanceFrameKind.Start) with { Profile = run },
            Frame(1, 4, MaintenanceFrameKind.HealthTransition) with { Health = (MaintenanceHealth)99 },
            cycle with { Cycle = cycle.Cycle! with { ObservedEligibilityAgeSeconds = double.NaN } },
            cycle with { Cycle = cycle.Cycle! with { SinceProgressSeconds = -1 } },
            cycle with { ProcessCpuMilliseconds = double.PositiveInfinity },
            Frame(1, 4, MaintenanceFrameKind.Failure),
            Frame(1, 4, MaintenanceFrameKind.Failure) with { FailureType = "Exception", SqlState = "not-sql" },
            Frame(1, 4, MaintenanceFrameKind.Stop),
            cycle with { ProcessCpuMilliseconds = null },
            ready with { BackendProcesses = [123] },
            Frame(1, 4, MaintenanceFrameKind.Stopped) with { BackendProcesses = null },
            Frame(1, 4, MaintenanceFrameKind.Stopped) with { BackendProcesses = [] },
            Frame(1, 4, MaintenanceFrameKind.Stopped) with { BackendProcesses = [123, 123] },
            Frame(1, 4, MaintenanceFrameKind.Stopped) with { BackendProcesses = [0] },
            Frame(1, 4, MaintenanceFrameKind.Stopped) with { BackendProcesses = Enumerable.Range(1, 17).ToArray() }
        }) {
            Assert.Throws<InvalidDataException>(() => MaintenanceHostProtocol.ValidateObservation(invalid));
        }
        Assert.Throws<InvalidDataException>(() => MaintenanceHostProtocol.ValidateControl(Frame(1, 1, MaintenanceFrameKind.Start)));
        var beforeStart = new CapacityWorkerAccounting(run, 1);
        beforeStart.Accept(ready);
        Assert.Throws<InvalidDataException>(() => beforeStart.Accept(Frame(1, 2, MaintenanceFrameKind.Stopped)));
        var validControl = Frame(1, 1, MaintenanceFrameKind.Start) with { ProcessCpuMilliseconds = null };
        MaintenanceHostProtocol.ValidateControl(validControl);
        var encoded = MaintenanceHostProtocol.Encode(cycle);
        Assert.Equal(cycle, await MaintenanceHostProtocol.ReadAsync(new StringReader(encoded + "\n"), CancellationToken.None));
        var missing = JsonNode.Parse(encoded)!.AsObject();
        missing["cycle"]!.AsObject().Remove("deletedReceipts");
        await Assert.ThrowsAsync<JsonException>(() => MaintenanceHostProtocol.ReadAsync(new StringReader(missing.ToJsonString() + "\n"), CancellationToken.None));
        var duplicate = encoded.Insert(1, "\"sequence\":4,");
        await Assert.ThrowsAsync<JsonException>(() => MaintenanceHostProtocol.ReadAsync(new StringReader(duplicate + "\n"), CancellationToken.None));
        var stopped = Frame(1, 4, MaintenanceFrameKind.Stopped) with { BackendProcesses = Enumerable.Range(1, 16).ToArray() };
        var terminal = await MaintenanceHostProtocol.ReadAsync(new StringReader(MaintenanceHostProtocol.Encode(stopped) + "\n"), CancellationToken.None);
        Assert.NotNull(terminal);
        Assert.Equal(stopped.BackendProcesses, terminal.BackendProcesses);
        first.Accept(terminal);
        Assert.Equal(MaintenanceFrameKind.Stopped, first.Snapshot()[^1].Kind);
        var excessive = JsonNode.Parse(MaintenanceHostProtocol.Encode(stopped))!.AsObject();
        excessive["backendProcesses"]!.AsArray().Add(17);
        await Assert.ThrowsAsync<JsonException>(() => MaintenanceHostProtocol.ReadAsync(new StringReader(excessive.ToJsonString() + "\n"), CancellationToken.None));

        MaintenanceHostFrame Frame(int worker, int sequence, MaintenanceFrameKind kind, MaintenanceCycleObservation? cycle = null)
            => new(MaintenanceHostProtocol.Version, run, worker, sequence, kind, now,
                Profile: kind == MaintenanceFrameKind.Ready ? run : null, Namespace: kind == MaintenanceFrameKind.Ready ? run : null,
                Maintenance: kind == MaintenanceFrameKind.Ready ? run : null, Cycle: cycle, ProcessCpuMilliseconds: 0,
                BackendProcesses: kind == MaintenanceFrameKind.Stopped ? [123] : null);
    }

    [Fact]
    public void Offered_arrivals_account_for_queue_rejection_and_unconfirmed_completion() {
        var accounting = new CapacityArrivalAccounting(3);
        accounting.Offer(0);
        accounting.QueueRejected(0, false);
        accounting.Offer(1);
        accounting.Start(1);
        accounting.Complete(new(1, CapacityAttemptState.Accepted, false, 20, 3, 7, 30, null));
        accounting.Offer(2);
        accounting.Start(2);
        Assert.Throws<InvalidOperationException>(() => accounting.CompleteSnapshot());
        accounting.Complete(new(2, CapacityAttemptState.Unconfirmed, true, 100, 30, 400, 530, "CommitUnconfirmed"));
        var results = accounting.CompleteSnapshot();
        Assert.Equal(3, results.Length);
        Assert.Single(results, result => result.State == CapacityAttemptState.QueueRejected);
        Assert.Single(results, result => result.State == CapacityAttemptState.Accepted);
        Assert.Single(results, result => result.State == CapacityAttemptState.Unconfirmed);
        Assert.Equal(530, ProjectsMaintenanceCapacityLoad.Distribution(results.Where(result => result.State != CapacityAttemptState.QueueRejected)
            .Select(result => result.EndToEndMilliseconds))!.P99);
        Assert.Throws<InvalidOperationException>(() => accounting.Offer(0));
        Assert.Throws<InvalidOperationException>(() => accounting.Complete(results[1]));

        const string profileKey = "0123456789abcdef0123";
        var database = $"cditall_{profileKey}_{Guid.NewGuid():N}";
        ProjectsMaintenanceCapacityResources.RequireDatabaseName(profileKey, database);
        foreach (var invalid in new[] {
            database + "0", database[..^1], database.ToUpperInvariant(),
            database.Replace(profileKey, "1123456789abcdef0123", StringComparison.Ordinal),
            "cditall_primary_" + Guid.NewGuid().ToString("N"), database + "\n"
        }) {
            Assert.Throws<InvalidDataException>(() => ProjectsMaintenanceCapacityResources.RequireDatabaseName(profileKey, invalid));
        }
        Assert.Throws<InvalidDataException>(() => ProjectsMaintenanceCapacityResources.RequireDatabaseName(profileKey[..^1], database));
    }
}
