using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;
using CanDoItAll.AgentFramework.Usage;
using CanDoItAll.Infrastructure;
using CanDoItAll.Infrastructure.FileSystem;
using CanDoItAll.Tests.Support;
using Xunit.Abstractions;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class FileProviderUsageWindowTests(ITestOutputHelper output) {
    private static readonly DateTimeOffset Now = new(2026, 9, 27, 12, 0, 0, TimeSpan.Zero);
    private static readonly ProviderUsageWindow Window = new(Now.AddDays(-7), Now);

    [Theory]
    [InlineData(20)]
    [InlineData(400)]
    public async Task Fresh_and_repeated_reads_are_bounded_by_selected_payloads(int oldRecords) {
        using var fixture = new Files();
        var agentId = (await fixture.Store.LoadCatalogAsync()).Agents.First().Id;
        var recentRun = Run(Now.AddYears(-2), agentId);
        var runs = Enumerable.Range(0, oldRecords).Select(i => Run(Now.AddDays(-400 - i), agentId)).Append(recentRun).ToArray();
        var observations = runs.Take(oldRecords).Select(run => Observation(run.CreatedAtUtc, run.Id))
            .Append(Observation(Window.FromUtc, recentRun.Id))
            .Append(Observation(Now.AddDays(-1)))
            .Append(Observation(Now))
            .Append(Observation(Window.FromUtc.AddTicks(-1))).ToArray();
        await fixture.Store.SaveExecutionAsync(SandboxWorkspaceExecutionState.Empty with { ExecutionRuns = runs, ProviderUsageObservations = observations });
        fixture.Reads.Clear();
        var timer = Stopwatch.StartNew();
        var baseline = await fixture.Store.LoadProviderUsageEvidenceAsync();
        output.WriteLine("old={0}; legacy: records={1}, json={2}, bytes={3}, elapsedMs={4:F2}", oldRecords,
            baseline.ProviderUsageObservations.Count, fixture.Reads.Count, fixture.Reads.Sum(read => read.LengthBytes), timer.Elapsed.TotalMilliseconds);
        Assert.Equal(oldRecords + 4, baseline.ProviderUsageObservations.Count);
        Assert.Equal(ProviderUsageSourceState.Indexing, (await fixture.Store.LoadProviderUsageWindowAsync(Window)).State);
        timer.Restart();
        await fixture.IndexAsync();
        output.WriteLine("old={0}; bootstrap elapsedMs={1:F2}", oldRecords, timer.Elapsed.TotalMilliseconds);
        for (var pass = 0; pass < 2; pass++) {
            fixture.Reads.Clear();
            var freshStore = fixture.NewStore();
            var allocated = GC.GetTotalAllocatedBytes();
            timer.Restart();
            var result = await freshStore.LoadProviderUsageWindowAsync(Window);
            var elapsed = timer.Elapsed.TotalMilliseconds;
            allocated = GC.GetTotalAllocatedBytes() - allocated;
            Assert.Equal(ProviderUsageSourceState.Complete, result.State);
            Assert.Equal(2, result.Evidence.ProviderUsageObservations.Count);
            Assert.Equal(recentRun.Id, Assert.Single(result.Evidence.ExecutionRuns).Id);
            Assert.NotNull(result.CoverageVerifiedAtUtc);
            Assert.Equal(4, fixture.Reads.Count(read => read.PayloadType == typeof(ProviderUsageObservation)));
            Assert.Single(fixture.Reads, read => read.PayloadType == typeof(ExecutionRunRecord));
            Assert.DoesNotContain(fixture.Reads, read => read.PayloadType == typeof(AgentUsageProjection));
            output.WriteLine("old={0}; pass={1}: usagePayloads=4, runPayloads=1, json={2}, bytes={3}, elapsedMs={4:F2}, allocatedBytes={5}",
                oldRecords, pass, fixture.Reads.Count, fixture.Reads.Sum(read => read.LengthBytes), elapsed, allocated);
        }
    }

    [Fact]
    public async Task Maintenance_resumes_and_canonical_changes_preserve_coverage_and_current_outcomes() {
        using var fixture = new Files();
        var run = Run(Now.AddYears(-2), (await fixture.Store.LoadCatalogAsync()).Agents.First().Id);
        var old = Observation(Now.AddYears(-2), run.Id);
        var recent = Observation(Window.FromUtc);
        await fixture.Store.SaveExecutionAsync(SandboxWorkspaceExecutionState.Empty with { ExecutionRuns = [run], ProviderUsageObservations = [old, recent] });
        var first = await new FileProviderUsageIndexMaintenance(fixture.Root).ProcessAsync(1);
        Assert.False(first.Complete);
        Assert.Equal(ProviderUsageSourceState.Indexing, (await fixture.NewStore().LoadProviderUsageWindowAsync(Window)).State);
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new FileProviderUsageIndexMaintenance(fixture.Root).ProcessAsync(1, cancellationToken: cancelled.Token));
        var added = Observation(Now.AddDays(-1), run.Id);
        await fixture.Store.UpdateExecutionAsync(state => state with { ProviderUsageObservations = state.ProviderUsageObservations.Append(added).ToArray() });
        await fixture.IndexAsync();
        var corrected = old with { CreatedAtUtc = Now.AddDays(-2) };
        await fixture.Store.SaveExecutionAsync(SandboxWorkspaceExecutionState.Empty with {
            ExecutionRuns = [run with { Outcome = RunOutcome.Failed }], ProviderUsageObservations = [corrected, recent, added]
        });
        var result = await fixture.NewStore().LoadProviderUsageWindowAsync(Window);
        Assert.Equal(3, result.Evidence.ProviderUsageObservations.Count);
        Assert.Equal(RunOutcome.Failed, Assert.Single(result.Evidence.ExecutionRuns).Outcome);
        await fixture.Store.SaveExecutionAsync(SandboxWorkspaceExecutionState.Empty with { ProviderUsageObservations = [recent] });
        Assert.Equal(recent.Id, Assert.Single((await fixture.NewStore().LoadProviderUsageWindowAsync(Window)).Evidence.ProviderUsageObservations).Id);
        var otherScope = new FileSandboxWorkspaceStore(fixture.Root, new(WorkspaceScopeKind.Project, Guid.NewGuid().ToString("N")));
        Assert.Empty((await otherScope.LoadProviderUsageWindowAsync(Window)).Evidence.ProviderUsageObservations);
        Assert.False((await otherScope.LoadProviderUsageWindowAsync(Window)).State == ProviderUsageSourceState.Complete);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Overview_guard_rejects_legacy_initialization_without_reading_historical_payloads(bool singleFile) {
        using var fixture = new Files();
        var layout = new FileSandboxWorkspaceStorageLayout(fixture.Root);
        var legacy = new FileSandboxWorkspaceJsonStore();
        var observation = Observation(Now.AddYears(-2));
        if (singleFile) {
            await legacy.WriteJsonAtomicallyAsync(layout.CatalogPath,
                SandboxWorkspaceDocument.Empty with { ProviderUsageObservations = [observation] }, default);
        } else {
            await legacy.WriteJsonAtomicallyAsync(layout.LegacyExecutionPath,
                SandboxWorkspaceExecutionState.Empty with { ProviderUsageObservations = [observation] }, default);
        }
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Store.EnsureReadyAsync());
        Assert.Empty(fixture.Reads);
        await Assert.ThrowsAsync<InvalidOperationException>(() => new FileProviderUsageIndexMaintenance(fixture.Root).ProcessAsync());
        Assert.Equal(ProviderUsageSourceState.Indexing, (await fixture.Store.LoadProviderUsageWindowAsync(Window)).State);
        Assert.DoesNotContain(fixture.Reads, read => read.PayloadType == typeof(SandboxWorkspaceExecutionState));
        await fixture.Store.LoadExecutionAsync();
        await fixture.Store.EnsureReadyAsync();
        await fixture.IndexAsync();
        var archive = await fixture.Store.LoadProviderUsageWindowAsync(new(Now.AddYears(-3), Now));
        Assert.Equal(observation.Id, Assert.Single(archive.Evidence.ProviderUsageObservations).Id);
    }

    [Fact]
    public async Task Overview_guard_accepts_a_new_workspace_before_and_after_catalog_initialization() {
        using var fixture = new Files();
        await fixture.Store.EnsureReadyAsync();
        await fixture.NewStore().LoadCatalogAsync();
        await fixture.Store.EnsureReadyAsync();
        Assert.DoesNotContain(fixture.Reads, read => read.PayloadType == typeof(ProviderUsageObservation));
    }

    [Fact]
    public async Task Overview_summary_rejects_missing_or_old_runtime_index_without_reading_run_payloads() {
        using var fixture = new Files();
        var agent = (await fixture.Store.LoadCatalogAsync()).Agents.First();
        await fixture.Store.SaveExecutionAsync(SandboxWorkspaceExecutionState.Empty with { ExecutionRuns = [Run(Now.AddYears(-2), agent.Id)] });
        var layout = new FileSandboxWorkspaceStorageLayout(fixture.Root);
        var json = JsonNode.Parse(await File.ReadAllTextAsync(layout.ExecutionIndexPath))!.AsObject();
        json.Remove(JsonNamingPolicy.CamelCase.ConvertName(nameof(ExecutionStorageIndex.ActiveRunCount)));
        await new FileSandboxWorkspaceJsonStore().WriteJsonAtomicallyAsync(layout.ExecutionIndexPath, json, default);
        fixture.Reads.Clear();
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Store.EnsureReadyAsync(requireSummaryIndex: true));
        File.Delete(layout.ExecutionIndexPath);
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Store.EnsureReadyAsync(requireSummaryIndex: true));
        await fixture.Store.EnsureReadyAsync();
        Assert.DoesNotContain(fixture.Reads, read => read.PayloadType == typeof(ExecutionRunRecord) || read.PayloadType == typeof(ProviderUsageObservation));
        await fixture.Store.LoadExecutionSummaryAsync();
        await fixture.Store.EnsureReadyAsync(requireSummaryIndex: true);
    }

    [Fact]
    public async Task Overview_guard_waits_for_initialization_lock_and_remains_cancellable() {
        using var fixture = new Files();
        await fixture.Store.SaveExecutionAsync(SandboxWorkspaceExecutionState.Empty);
        var layout = new FileSandboxWorkspaceStorageLayout(fixture.Root);
        var writer = new DurableFileWriter(new PhysicalFileSystemPathPolicyFactory());
        await using (await writer.AcquireCoordinationAsync(fixture.Root, layout.WorkspaceLockPath,
            TimeSpan.FromSeconds(10), requirePrivateUnixMode: false, default)) {
            using var cancellation = new CancellationTokenSource();
            var waiting = fixture.NewStore().EnsureReadyAsync(cancellationToken: cancellation.Token);
            Assert.False(waiting.IsCompleted);
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting);
        }
        await fixture.Store.EnsureReadyAsync();
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("corrupt")]
    [InlineData("version")]
    [InlineData("bucket")]
    public async Task Invalid_index_is_explicit_and_rebuildable(string damage) {
        using var fixture = new Files();
        var observation = Observation(Now.AddDays(-1));
        await fixture.Store.SaveExecutionAsync(SandboxWorkspaceExecutionState.Empty with { ProviderUsageObservations = [observation] });
        await fixture.IndexAsync();
        var json = new FileSandboxWorkspaceJsonStore();
        var index = new FileProviderUsageIndex(new(fixture.Root), json);
        var header = (await index.ReadHeaderAsync(default))!;
        switch (damage) {
            case "missing":
                File.Delete(index.HeaderPath);
                break;
            case "corrupt":
                await File.WriteAllTextAsync(index.HeaderPath, "{broken");
                break;
            case "version":
                await index.WriteHeaderAsync(header with { Version = 0 }, default);
                break;
            case "bucket":
                File.Delete(index.BucketPath(header, DateOnly.FromDateTime(observation.CreatedAtUtc.UtcDateTime)));
                break;
        }
        fixture.Reads.Clear();
        var result = await fixture.NewStore().LoadProviderUsageWindowAsync(Window);
        Assert.Equal(ProviderUsageSourceState.Indexing, result.State);
        Assert.DoesNotContain(fixture.Reads, read => read.PayloadType == typeof(ProviderUsageObservation));
        await fixture.IndexAsync(rebuild: true);
        Assert.Single((await fixture.NewStore().LoadProviderUsageWindowAsync(Window)).Evidence.ProviderUsageObservations);
    }

    [Fact]
    public async Task Maintenance_and_concurrent_canonical_writes_share_the_workspace_lock() {
        using var fixture = new Files();
        await fixture.Store.SaveExecutionAsync(SandboxWorkspaceExecutionState.Empty with {
            ProviderUsageObservations = Enumerable.Range(0, 12).Select(_ => Observation(Now.AddYears(-2))).ToArray()
        });
        var recent = Observation(Now.AddDays(-1));
        var indexing = fixture.IndexAsync();
        var writing = fixture.NewStore().UpdateExecutionAsync(state => state with {
            ProviderUsageObservations = state.ProviderUsageObservations.Append(recent).ToArray()
        });
        await Task.WhenAll(indexing, writing);
        var result = await fixture.NewStore().LoadProviderUsageWindowAsync(Window);
        Assert.Equal(ProviderUsageSourceState.Complete, result.State);
        Assert.Equal(recent.Id, Assert.Single(result.Evidence.ProviderUsageObservations).Id);
    }

    [Fact]
    public async Task Interrupted_index_publication_never_certifies_omitted_evidence_and_legacy_ids_are_stable() {
        using var fixture = new Files();
        await fixture.Store.SaveExecutionAsync(SandboxWorkspaceExecutionState.Empty);
        await fixture.IndexAsync();
        var layout = new FileSandboxWorkspaceStorageLayout(fixture.Root);
        var json = new FileSandboxWorkspaceJsonStore();
        var index = new FileProviderUsageIndex(layout, json);
        var observation = Observation(Now.AddDays(-1)) with { Id = Guid.Empty };
        var path = Path.Combine(layout.OrphanUsageRoot, Guid.NewGuid().ToString("N") + ".json");
        await index.IncludeAsync(path, observation, default);
        Assert.Empty((await fixture.NewStore().LoadProviderUsageWindowAsync(Window)).Evidence.ProviderUsageObservations);
        await json.WriteJsonAtomicallyAsync(path, observation, default);
        var first = await fixture.NewStore().LoadProviderUsageWindowAsync(Window);
        var wider = await fixture.NewStore().LoadProviderUsageWindowAsync(new(Now.AddMonths(-1), Now));
        Assert.StartsWith("legacy:", Assert.Single(first.ObservationIdentities));
        Assert.Equal(first.ObservationIdentities, wider.ObservationIdentities);
        var header = (await index.ReadHeaderAsync(default))!;
        await json.WriteJsonAtomicallyAsync(index.BucketPath(header, DateOnly.FromDateTime(observation.CreatedAtUtc.UtcDateTime)), new FileUsageIndexBucket([]), default);
        Assert.Equal(ProviderUsageSourceState.Indexing, (await fixture.NewStore().LoadProviderUsageWindowAsync(Window)).State);
        await fixture.IndexAsync(rebuild: true);
        Assert.Equal(first.ObservationIdentities, (await fixture.NewStore().LoadProviderUsageWindowAsync(Window)).ObservationIdentities);
    }

    [Fact]
    public async Task Cancellation_keeps_the_workspace_lock_until_all_started_payload_reads_have_drained() {
        using var fixture = new Files();
        await fixture.Store.SaveExecutionAsync(SandboxWorkspaceExecutionState.Empty with {
            ProviderUsageObservations = Enumerable.Range(0, 8).Select(_ => Observation(Now.AddDays(-1))).ToArray()
        });
        await fixture.IndexAsync();
        using var release = new ManualResetEventSlim();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var store = new FileSandboxWorkspaceStore(fixture.Root, WorkspaceScopeDescriptor.Sandbox,
            null, null, null, new FileSandboxWorkspaceJsonReadDiagnostics(read => {
                if (read.PayloadType == typeof(ProviderUsageObservation)) {
                    started.TrySetResult();
                    if (!release.Wait(TimeSpan.FromSeconds(30))) {
                        throw new TimeoutException("The test did not release the pending payload read.");
                    }
                }
            }));
        using var cancellation = new CancellationTokenSource();
        var reading = store.LoadProviderUsageWindowAsync(Window, cancellation.Token);
        Task? waiting = null;
        try {
            await started.Task.WaitAsync(TimeSpan.FromSeconds(30));
            cancellation.Cancel();
            waiting = fixture.NewStore().EnsureReadyAsync();
            Assert.False(reading.IsCompleted);
            Assert.False(waiting.IsCompleted);
        } finally {
            release.Set();
        }
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => reading);
        await waiting!.WaitAsync(TimeSpan.FromSeconds(30));
        Assert.Equal(8, (await fixture.NewStore().LoadProviderUsageWindowAsync(Window)).Evidence.ProviderUsageObservations.Count);
    }

    private static ProviderUsageObservation Observation(DateTimeOffset at, Guid? run = null) => new(Guid.NewGuid(), at,
        "fixture", ProviderKind.OpenAi, "model", ProviderTransportKind.Responses, ProviderUsageSourcePhases.AgentRuntime,
        ProviderUsageObservationStatus.Observed, 10, 2, 4, 1, 14, 0) { ExecutionRunId = run };

    private static ExecutionRunRecord Run(DateTimeOffset at, Guid agentId) => new(Guid.NewGuid(), agentId, null, "Fixture",
        "test", "test", "test", "", "test", "test", "{}", "", "", "fixture", "model", ExecutionState.Completed,
        RunOutcome.Succeeded, at, at, at, at, "", null, []);

    private sealed class Files : IDisposable {
        public string Root { get; } = TestFileSystem.CreateTemporaryRoot("usage-window");
        public List<FileSandboxWorkspacePhysicalJsonRead> Reads { get; } = [];
        public FileSandboxWorkspaceStore Store { get; }
        public Files() => Store = NewStore();
        public FileSandboxWorkspaceStore NewStore() => new(Root, WorkspaceScopeDescriptor.Sandbox,
            null, null, null, new FileSandboxWorkspaceJsonReadDiagnostics(Reads.Add));
        public async Task IndexAsync(bool rebuild = false) {
            var maintenance = new FileProviderUsageIndexMaintenance(Root);
            while (!(await maintenance.ProcessAsync(100, rebuild)).Complete) {
                rebuild = false;
            }
        }
        public void Dispose() => TestFileSystem.DeleteDirectoryWithRetry(Root);
    }
}
