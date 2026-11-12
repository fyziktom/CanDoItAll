using System.Diagnostics;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;
using CanDoItAll.Infrastructure;

namespace CanDoItAll.Tests.Integration.Runtime;

[Trait("Category", "HostPlatform")]
public sealed class HistoricalPendingPublicationScanTests {
    [Fact]
    public async Task Flat_sessions_beyond_the_former_entry_limit_remain_readable_without_writes() {
        await using var fixture = await AgentToolAdmissionJournalFixture.CreateAsync();
        var layout = new FileSandboxWorkspaceStorageLayout(fixture.WorkspaceRoot, fixture.StorageScope);
        var originalCount = Directory.EnumerateFiles(layout.ExecutionSessionsRoot, "*.json").Count();
        await PopulateChatsAsync(fixture, layout, 4100);
        using var observation = await HistoricalFileObservation.StartAsync(fixture.WorkspaceRoot);
        var store = new FileSandboxWorkspaceHistoricalReadStore(fixture.WorkspaceRoot, fixture.StorageScope);
        var result = await store.ReadHistoricalRunAsync(fixture.Detail.Run.Id);
        Assert.Equal(AgentHistoricalStorageReadStatus.Ready, result.Status);
        Assert.Equal(fixture.Detail.Run.Id, result.Snapshot!.Run.Id);
        Assert.Equal(fixture.Detail.Run.Revision, result.Snapshot.Run.Revision);
        Assert.Equal(fixture.Detail.Run.MetadataJson, result.Snapshot.Run.MetadataJson);
        Assert.Equal(new AgentHistoricalChatIdentity(fixture.Detail.ChatSession!.Id, fixture.Agent.Id), result.Snapshot.Chat);
        Assert.Equal(AgentHistoricalStorageReadStatus.Missing, (await store.ReadHistoricalRunAsync(Guid.NewGuid())).Status);
        Assert.Equal(originalCount + 4100, Directory.EnumerateFiles(layout.ExecutionSessionsRoot, "*.json").Count());
        await observation.AssertNoChangesAsync();
    }

    [Fact]
    public async Task Only_exact_selected_publication_names_refuse_a_large_session_directory() {
        await using var fixture = await AgentToolAdmissionJournalFixture.CreateAsync();
        var layout = new FileSandboxWorkspaceStorageLayout(fixture.WorkspaceRoot, fixture.StorageScope);
        var selected = layout.SessionPath(fixture.Detail.ChatSession!.Id);
        var targetName = Path.GetFileName(selected);
        foreach (var index in Enumerable.Range(0, 4100)) {
            var unrelated = $".unrelated-{index:D4}.json.{Guid.NewGuid():N}.pending";
            await File.WriteAllBytesAsync(Path.Combine(layout.ExecutionSessionsRoot, unrelated), []);
        }
        foreach (var candidate in new[] { $".{targetName}.not-a-guid.pending", $".{targetName}.{Guid.NewGuid():N}.PENDING",
            $".{targetName}.{Guid.NewGuid():N}.pending.extra", $".{targetName}.{Guid.NewGuid():D}.pending" }) {
            Assert.False(DurableFileWriter.IsPendingPublicationFileName(candidate, targetName, StringComparison.OrdinalIgnoreCase));
            Assert.False(DurableFileWriter.IsPendingPublicationFileName(candidate.AsSpan(), targetName.AsSpan(), StringComparison.OrdinalIgnoreCase));
            await File.WriteAllBytesAsync(Path.Combine(layout.ExecutionSessionsRoot, candidate), []);
        }
        var store = new FileSandboxWorkspaceHistoricalReadStore(fixture.WorkspaceRoot, fixture.StorageScope);
        Assert.Equal(AgentHistoricalStorageReadStatus.Ready, (await store.ReadHistoricalRunAsync(fixture.Detail.Run.Id)).Status);
        var pendingName = $".{targetName}.{Guid.NewGuid():N}.pending";
        Assert.True(DurableFileWriter.IsPendingPublicationFileName(pendingName, targetName, StringComparison.OrdinalIgnoreCase));
        Assert.True(DurableFileWriter.IsPendingPublicationFileName(pendingName.AsSpan(), targetName.AsSpan(), StringComparison.OrdinalIgnoreCase));
        Assert.True(DurableFileWriter.IsPendingPublicationFileName(pendingName.AsSpan(), targetName.ToUpperInvariant().AsSpan(), StringComparison.OrdinalIgnoreCase));
        Assert.False(DurableFileWriter.IsPendingPublicationFileName(pendingName.AsSpan(), targetName.ToUpperInvariant().AsSpan(), StringComparison.Ordinal));
        var pending = Path.Combine(layout.ExecutionSessionsRoot, pendingName);
        await File.WriteAllTextAsync(pending, "retained exact publication");
        try {
            using var observation = await HistoricalFileObservation.StartAsync(fixture.WorkspaceRoot);
            var refused = await store.ReadHistoricalRunAsync(fixture.Detail.Run.Id);
            Assert.Equal(AgentHistoricalStorageReadStatus.RecoveryRequired, refused.Status);
            Assert.Null(refused.Snapshot);
            Assert.Equal("retained exact publication", await File.ReadAllTextAsync(pending));
            await observation.AssertNoChangesAsync();
        } finally {
            File.Delete(pending);
        }
        Assert.Equal(AgentHistoricalStorageReadStatus.Ready, (await store.ReadHistoricalRunAsync(fixture.Detail.Run.Id)).Status);
    }

    [Fact]
    public async Task Final_scan_detects_publication_of_an_absent_semantic_journal_after_payload_read() {
        await using var fixture = await AgentToolAdmissionJournalFixture.CreateAsync();
        var layout = new FileSandboxWorkspaceStorageLayout(fixture.WorkspaceRoot, fixture.StorageScope);
        var pending = Path.Combine(layout.ExecutionStorageRoot, $".pending-run-detail-update.json.{Guid.NewGuid():N}.pending");
        using var root = StartObservation();
        var introduced = false;
        using var listener = ObserveReads(root, payload => {
            if (payload == nameof(AgentHistoricalChatIdentity) && !introduced) {
                File.WriteAllText(pending, "retained concurrent publication");
                introduced = true;
            }
        });
        var store = new FileSandboxWorkspaceHistoricalReadStore(fixture.WorkspaceRoot, fixture.StorageScope);
        try {
            var refused = await store.ReadHistoricalRunAsync(fixture.Detail.Run.Id);
            Assert.True(introduced);
            Assert.Equal(AgentHistoricalStorageReadStatus.RecoveryRequired, refused.Status);
            Assert.Null(refused.Snapshot);
            Assert.False(File.Exists(Path.Combine(layout.ExecutionStorageRoot, "pending-run-detail-update.json")));
            Assert.Equal("retained concurrent publication", await File.ReadAllTextAsync(pending));
        } finally {
            File.Delete(pending);
        }
        using var observation = await HistoricalFileObservation.StartAsync(fixture.WorkspaceRoot);
        Assert.Equal(AgentHistoricalStorageReadStatus.Ready, (await store.ReadHistoricalRunAsync(fixture.Detail.Run.Id)).Status);
        await observation.AssertNoChangesAsync();
    }

    [Fact]
    public async Task Caller_cancellation_during_nonmatching_enumeration_propagates_and_the_next_read_is_usable() {
        await using var fixture = await AgentToolAdmissionJournalFixture.CreateAsync();
        var layout = new FileSandboxWorkspaceStorageLayout(fixture.WorkspaceRoot, fixture.StorageScope);
        await PopulateChatsAsync(fixture, layout, 512);
        using var cancellation = new CancellationTokenSource();
        var clock = new ScanClock();
        clock.Observe = calls => {
            if (calls == 128) {
                cancellation.Cancel();
            }
        };
        var store = new FileSandboxWorkspaceHistoricalReadStore(fixture.WorkspaceRoot, fixture.StorageScope, clock);
        using var observation = await HistoricalFileObservation.StartAsync(fixture.WorkspaceRoot);
        var failure = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.ReadHistoricalRunAsync(fixture.Detail.Run.Id, cancellation.Token));
        Assert.Equal(cancellation.Token, failure.CancellationToken);
        Assert.True(clock.Calls >= 128);
        clock.Observe = null;
        Assert.Equal(AgentHistoricalStorageReadStatus.Ready, (await store.ReadHistoricalRunAsync(fixture.Detail.Run.Id)).Status);
        await observation.AssertNoChangesAsync();
    }

    [Fact]
    public async Task One_elapsed_budget_covers_all_read_phases_and_refuses_a_page_without_a_successor() {
        await using var fixture = await AgentToolAdmissionJournalFixture.CreateAsync();
        var clock = new ScanClock();
        var store = new FileSandboxWorkspaceHistoricalReadStore(fixture.WorkspaceRoot, fixture.StorageScope, clock);
        using var observation = await HistoricalFileObservation.StartAsync(fixture.WorkspaceRoot);
        using var root = StartObservation();
        var phases = new HashSet<string>(StringComparer.Ordinal);
        using (var listener = ObserveReads(root, payload => {
            if ((payload is "WorkspaceStorageIndex" or nameof(ExecutionRunRecord) or nameof(AgentHistoricalChatIdentity)) && phases.Add(payload)) {
                clock.Advance(TimeSpan.FromSeconds(2));
            }
        })) {
            var refused = await store.ReadHistoricalRunAsync(fixture.Detail.Run.Id);
            Assert.Equal(AgentHistoricalStorageReadStatus.ReadBudgetExceeded, refused.Status);
            Assert.Null(refused.Snapshot);
            Assert.Equal(3, phases.Count);
        }
        Assert.Equal(AgentHistoricalStorageReadStatus.Ready, (await store.ReadHistoricalRunAsync(fixture.Detail.Run.Id)).Status);
        var begin = clock.Calls;
        clock.Observe = calls => {
            if (calls == begin + 2) {
                clock.Advance(TimeSpan.FromSeconds(6));
            }
        };
        var run = fixture.Detail.Run;
        var basis = new AgentHistoricalCatalogBasis(store, new(fixture.Profile, fixture.StorageScope), () => true);
        var page = await AgentToolAdmissionJournal.ReadHistoricalReceiptReviewAsync(store, fixture.Profile,
            new(run.Id, run.ChatSessionId, run.AgentId, run.CreatedAtUtc), fixture.StorageScope,
            (_, _, _) => ValueTask.FromResult(AgentHistoricalReceiptReadPolicy.Allowed), [], [], basis);
        Assert.Equal(AgentHistoricalReceiptReviewStop.CooperativeBudget, page.Stop);
        Assert.Empty(page.Observations);
        Assert.Null(page.NextCursor);
        Assert.Null(page.RunRevision);
        Assert.Null(page.JournalRevision);
        clock.Observe = null;
        Assert.Equal(AgentHistoricalStorageReadStatus.Ready, (await store.ReadHistoricalRunAsync(run.Id)).Status);
        await observation.AssertNoChangesAsync();
    }

    private static async Task PopulateChatsAsync(AgentToolAdmissionJournalFixture fixture, FileSandboxWorkspaceStorageLayout layout, int count) {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        foreach (var _ in Enumerable.Range(0, count)) {
            var chat = fixture.Detail.ChatSession! with { Id = Guid.NewGuid(), Messages = [] };
            await File.WriteAllBytesAsync(layout.SessionPath(chat.Id), JsonSerializer.SerializeToUtf8Bytes(chat, options));
        }
    }

    private static Activity StartObservation() => new Activity("historical-pending-scan")
        .SetParentId(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded).Start();

    private static ActivityListener ObserveReads(Activity root, Action<string> observed) {
        var listener = new ActivityListener {
            ShouldListenTo = source => source.Name == AgentFrameworkTelemetry.SourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = activity => {
                if (activity.TraceId == root.TraceId && activity.OperationName == "workspace.historical.read" &&
                    activity.GetTagItem("payload.type") is string payload) {
                    observed(payload);
                }
            }
        };
        ActivitySource.AddActivityListener(listener);
        return listener;
    }

    private sealed class ScanClock : TimeProvider {
        private long timestamp;
        private int calls;
        internal Action<int>? Observe { get; set; }
        internal int Calls => Volatile.Read(ref calls);
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() {
            var count = Interlocked.Increment(ref calls);
            Observe?.Invoke(count);
            return Interlocked.Read(ref timestamp);
        }
        internal void Advance(TimeSpan elapsed) => Interlocked.Add(ref timestamp, elapsed.Ticks);
    }
}
