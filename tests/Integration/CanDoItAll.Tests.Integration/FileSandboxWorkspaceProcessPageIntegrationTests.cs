using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;
using Xunit.Abstractions;

namespace CanDoItAll.Tests.Integration.Runtime;

[Trait("Category", "FileSystemPortability")]
[Trait("Category", "HostPlatform")]
public sealed class FileSandboxWorkspaceProcessPageIntegrationTests(ITestOutputHelper output) {
    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(16)]
    public async Task Restarted_pages_select_only_matching_records_in_key_order_without_detail_hydration(int take) {
        await using var fixture = await AgentToolAdmissionJournalFixture.CreateAsync();
        var process = Guid.NewGuid();
        var selected = Enumerable.Range(0, 7).Select(_ => Run(fixture, process)).ToArray();
        var history = Enumerable.Range(0, 96).Select(_ => Run(fixture, Guid.NewGuid())).ToArray();
        await fixture.Store.UpdateExecutionAsync(state => state with { ExecutionRuns = [.. state.ExecutionRuns, .. selected, .. history] });
        var seen = new List<Guid>();
        Guid? after = null;
        for (var turn = 0; ; turn++) {
            Assert.True(turn < 8);
            var reads = new List<FileSandboxWorkspacePhysicalJsonRead>();
            var page = await Observed(fixture, reads).ReadProcessExecutionRunPageAsync(new(process, after, take));
            Assert.Equal(ProcessExecutionRunPageStatus.Ready, page.Status);
            Assert.InRange(page.Candidates.Count, 1, take);
            Assert.Equal(page.Candidates.Count, reads.Count(read => read.PayloadType == typeof(ExecutionRunRecord)));
            Assert.DoesNotContain(reads, read => read.FullPath == Layout(fixture).ExecutionChatIndexPath);
            Assert.DoesNotContain(reads, read => history.Any(run => read.FullPath.Contains(run.Id.ToString("N"), StringComparison.Ordinal)));
            Assert.DoesNotContain(reads, read => read.FullPath.Contains("logs", StringComparison.Ordinal));
            Assert.All(page.Candidates, run => Assert.Equal(process, Guid.Parse(run.ProcessRunId)));
            seen.AddRange(page.Candidates.Select(run => run.Id));
            after = page.LastExecutionRunId;
            Assert.Equal(seen[^1], after);
            if (page.Exhausted) {
                break;
            }
        }
        Assert.Equal(selected.Select(run => run.Id).Order(), seen);
        var exhausted = await fixture.NewStore().ReadProcessExecutionRunPageAsync(new(process, after, take));
        Assert.True(exhausted.Exhausted);
        Assert.Empty(exhausted.Candidates);
    }

    [Theory]
    [InlineData(UnavailableIndex.Missing)]
    [InlineData(UnavailableIndex.Stale)]
    [InlineData(UnavailableIndex.LegacyLayout)]
    public async Task Incomplete_indexes_report_pending_without_rebuilding_or_reading_journals(UnavailableIndex state) {
        await using var fixture = await AgentToolAdmissionJournalFixture.CreateAsync();
        var run = Run(fixture, Guid.NewGuid());
        await fixture.Store.SaveExecutionRunDetailAsync(new(run, null, [], []));
        var layout = Layout(fixture);
        if (state == UnavailableIndex.Missing) {
            File.Delete(layout.ExecutionProcessIndexPath);
        } else if (state == UnavailableIndex.Stale) {
            var index = JsonNode.Parse(await File.ReadAllTextAsync(layout.ExecutionProcessIndexPath))!;
            index["revision"] = -1;
            await File.WriteAllTextAsync(layout.ExecutionProcessIndexPath, index.ToJsonString());
        } else {
            File.Delete(layout.ExecutionIndexPath);
            await File.WriteAllTextAsync(layout.LegacyExecutionPath, "{}");
        }
        var before = await IndexBytes(layout);
        var reads = new List<FileSandboxWorkspacePhysicalJsonRead>();

        var page = await Observed(fixture, reads).ReadProcessExecutionRunPageAsync(new(Guid.Parse(run.ProcessRunId)));

        Assert.Equal(state == UnavailableIndex.LegacyLayout ? ProcessExecutionRunPageStatus.LayoutUnavailable : ProcessExecutionRunPageStatus.IndexUnavailable, page.Status);
        Assert.False(page.Exhausted);
        Assert.Empty(page.Candidates);
        Assert.DoesNotContain(reads, read => read.PayloadType == typeof(ExecutionRunRecord));
        Assert.Equal(before, await IndexBytes(layout));
    }

    [Fact]
    public async Task Legacy_projection_rows_are_conservative_candidates_and_are_never_silently_omitted() {
        await using var fixture = await AgentToolAdmissionJournalFixture.CreateAsync();
        var process = Guid.NewGuid();
        var selected = Run(fixture, process);
        var unrelated = Run(fixture, Guid.NewGuid());
        await fixture.Store.UpdateExecutionAsync(state => state with { ExecutionRuns = [.. state.ExecutionRuns, selected, unrelated] });
        var layout = Layout(fixture);
        var index = await ReadIndex(layout);
        foreach (var summary in index["runSummaries"]!.AsArray()) {
            summary!["reportingProjectionVersion"] = 0;
            summary["processRunId"] = "";
        }
        index["reportingProjectionVersion"] = 0;
        await File.WriteAllTextAsync(layout.ExecutionChatIndexPath, index.ToJsonString());
        File.Delete(layout.ExecutionProcessIndexPath);
        Assert.Equal(ProcessExecutionRunPageStatus.IndexUnavailable, (await fixture.Store.ReadProcessExecutionRunPageAsync(new(process))).Status);
        Assert.Equal(ProcessExecutionRunPageStatus.Ready, await fixture.Store.PrepareProcessExecutionRunIndexAsync());
        var before = await IndexBytes(layout);
        var candidates = new List<Guid>();
        Guid? after = null;
        for (var turn = 0; ; turn++) {
            Assert.True(turn < 4);
            var page = await fixture.NewStore().ReadProcessExecutionRunPageAsync(new(process, after, 1));
            candidates.Add(Assert.Single(page.Candidates).Id);
            after = page.LastExecutionRunId;
            if (page.Exhausted) {
                break;
            }
        }
        Assert.Contains(selected.Id, candidates);
        Assert.Contains(unrelated.Id, candidates);
        Assert.Equal(3, candidates.Count);
        Assert.Equal(before, await IndexBytes(layout));
    }

    [Fact]
    public async Task Future_selection_projection_evidence_is_pending_and_does_not_trigger_journal_reads() {
        await using var fixture = await AgentToolAdmissionJournalFixture.CreateAsync();
        var layout = Layout(fixture);
        var index = JsonNode.Parse(await File.ReadAllTextAsync(layout.ExecutionProcessIndexPath))!;
        index["schemaVersion"] = FileProcessExecutionIndex.SchemaVersion + 1;
        await File.WriteAllTextAsync(layout.ExecutionProcessIndexPath, index.ToJsonString());
        var reads = new List<FileSandboxWorkspacePhysicalJsonRead>();

        var page = await Observed(fixture, reads).ReadProcessExecutionRunPageAsync(new(Guid.NewGuid()));

        Assert.Equal(ProcessExecutionRunPageStatus.IndexUnavailable, page.Status);
        Assert.False(page.Exhausted);
        Assert.DoesNotContain(reads, read => read.PayloadType == typeof(ExecutionRunRecord));
    }

    [Fact]
    public async Task Duplicate_metadata_identities_fail_before_any_journal_is_hydrated() {
        await using var fixture = await AgentToolAdmissionJournalFixture.CreateAsync();
        var run = Run(fixture, Guid.NewGuid());
        await fixture.Store.SaveExecutionRunDetailAsync(new(run, null, [], []));
        await fixture.Store.SaveExecutionRunDetailAsync(new(Run(fixture, Guid.NewGuid()), null, [], []));
        var layout = Layout(fixture);
        var index = JsonNode.Parse(await File.ReadAllTextAsync(layout.ExecutionProcessIndexPath))!;
        index["runs"]![1] = index["runs"]![0]!.DeepClone();
        await File.WriteAllTextAsync(layout.ExecutionProcessIndexPath, index.ToJsonString());
        var reads = new List<FileSandboxWorkspacePhysicalJsonRead>();

        await Assert.ThrowsAsync<InvalidDataException>(() => Observed(fixture, reads).ReadProcessExecutionRunPageAsync(new(Guid.Parse(run.ProcessRunId))));

        Assert.DoesNotContain(reads, read => read.PayloadType == typeof(ExecutionRunRecord));
    }

    [Fact]
    public async Task Selected_record_same_stamp_and_revision_changes_are_visible_on_repeated_reads() {
        await using var fixture = await AgentToolAdmissionJournalFixture.CreateAsync();
        var run = Run(fixture, Guid.NewGuid());
        await fixture.Store.SaveExecutionRunDetailAsync(new(run, null, [], []));
        var query = new ProcessExecutionRunPageQuery(Guid.Parse(run.ProcessRunId));
        Assert.Equal("Indexed run", Assert.Single((await fixture.Store.ReadProcessExecutionRunPageAsync(query)).Candidates).Title);
        var path = Layout(fixture).RunPath(run.Id);
        var stamp = File.GetLastWriteTimeUtc(path);
        var original = await File.ReadAllTextAsync(path);
        await File.WriteAllTextAsync(path, original.Replace("Indexed run", "Changed run", StringComparison.Ordinal));
        File.SetLastWriteTimeUtc(path, stamp);

        var changed = Assert.Single((await fixture.Store.ReadProcessExecutionRunPageAsync(query)).Candidates);

        Assert.Equal("Changed run", changed.Title);
        Assert.Equal(run.Revision, changed.Revision);
    }

    [Fact]
    public async Task Prepared_publication_is_reported_without_recovery_and_becomes_readable_after_its_owner_recovers() {
        await using var fixture = await AgentToolAdmissionJournalFixture.CreateAsync();
        var run = Run(fixture, Guid.NewGuid());
        await fixture.Store.SaveExecutionRunDetailAsync(new(run, null, [], []));
        var faulted = fixture.NewStore(stage => {
            if (stage == ExistingRunDetailCommitStage.JournalPersisted) {
                throw new IOException("Prepared page-read fixture.");
            }
        });
        await Assert.ThrowsAsync<IOException>(() => faulted.UpdateExecutionRunDetailAsync(run.Id,
            detail => detail with { Run = detail.Run with { Title = "Recovered run", Revision = run.Revision + 1 } }));
        var layout = Layout(fixture);
        var before = Directory.EnumerateFiles(layout.ExecutionStorageRoot, "*.json").ToDictionary(path => path,
            path => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))));
        var reads = new List<FileSandboxWorkspacePhysicalJsonRead>();
        var query = new ProcessExecutionRunPageQuery(Guid.Parse(run.ProcessRunId));

        var pending = await Observed(fixture, reads).ReadProcessExecutionRunPageAsync(query);

        Assert.Equal(ProcessExecutionRunPageStatus.PublicationPending, pending.Status);
        Assert.Equal(ProcessExecutionRunPageStatus.PublicationPending, await fixture.NewStore().PrepareProcessExecutionRunIndexAsync());
        Assert.False(pending.Exhausted);
        Assert.Empty(reads);
        foreach (var item in before) {
            Assert.Equal(item.Value, Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(item.Key))));
        }
        Assert.Equal("Recovered run", (await fixture.NewStore().GetExecutionRunDetailAsync(run.Id))!.Run.Title);
        Assert.Equal("Recovered run", Assert.Single((await fixture.NewStore().ReadProcessExecutionRunPageAsync(query)).Candidates).Title);
    }

    [Fact]
    public async Task Invalid_input_and_pre_cancelled_calls_do_not_access_storage() {
        await using var fixture = await AgentToolAdmissionJournalFixture.CreateAsync();
        var reads = new List<FileSandboxWorkspacePhysicalJsonRead>();
        var store = Observed(fixture, reads);
        ProcessExecutionRunPageQuery[] invalid = [new(Guid.Empty), new(Guid.NewGuid(), Guid.Empty),
            new(Guid.NewGuid(), Take: 0), new(Guid.NewGuid(), Take: 65)];
        foreach (var query in invalid) {
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => store.ReadProcessExecutionRunPageAsync(query));
        }
        await Assert.ThrowsAsync<ArgumentNullException>(() => store.ReadProcessExecutionRunPageAsync(null!));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.ReadProcessExecutionRunPageAsync(new(Guid.NewGuid()), cancellation.Token));
        Assert.Empty(reads);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Missing_or_corrupt_selected_records_cannot_be_reported_as_exhaustion(bool missing) {
        await using var fixture = await AgentToolAdmissionJournalFixture.CreateAsync();
        var run = Run(fixture, Guid.NewGuid());
        await fixture.Store.SaveExecutionRunDetailAsync(new(run, null, [], []));
        var path = Layout(fixture).RunPath(run.Id);
        if (missing) {
            File.Delete(path);
        } else {
            await File.WriteAllTextAsync(path, "{invalid");
        }
        await Assert.ThrowsAsync<InvalidDataException>(() => fixture.NewStore().ReadProcessExecutionRunPageAsync(new(Guid.Parse(run.ProcessRunId))));
    }

    [Fact]
    public async Task Empty_storage_is_exhausted_without_seeding_business_files() {
        await using var fixture = await AgentToolAdmissionJournalFixture.CreateAsync();
        var emptyRoot = Path.Combine(fixture.WorkspaceRoot, "empty-process-page");
        Directory.CreateDirectory(emptyRoot);
        var store = new FileSandboxWorkspaceStore(emptyRoot);

        var page = await store.ReadProcessExecutionRunPageAsync(new(Guid.NewGuid()));

        Assert.Equal(ProcessExecutionRunPageStatus.Ready, page.Status);
        Assert.True(page.Exhausted);
        Assert.Empty(page.Candidates);
        Assert.Equal(ProcessExecutionRunPageStatus.Ready, await store.PrepareProcessExecutionRunIndexAsync());
        var layout = new FileSandboxWorkspaceStorageLayout(emptyRoot);
        Assert.False(File.Exists(layout.CatalogPath));
        Assert.False(File.Exists(layout.ExecutionIndexPath));
        Assert.False(File.Exists(layout.ExecutionProcessIndexPath));
    }

    [Fact]
    public async Task Identical_process_ids_in_other_workspaces_do_not_cross_the_selected_owner() {
        await using var first = await AgentToolAdmissionJournalFixture.CreateAsync();
        await using var second = await AgentToolAdmissionJournalFixture.CreateAsync();
        var process = Guid.NewGuid();
        var selected = Run(first, process);
        var unrelated = Run(second, process);
        await first.Store.SaveExecutionRunDetailAsync(new(selected, null, [], []));
        await second.Store.SaveExecutionRunDetailAsync(new(unrelated, null, [], []));

        Assert.Equal(selected.Id, Assert.Single((await first.NewStore().ReadProcessExecutionRunPageAsync(new(process))).Candidates).Id);
        Assert.Equal(unrelated.Id, Assert.Single((await second.NewStore().ReadProcessExecutionRunPageAsync(new(process))).Candidates).Id);
    }

    [Theory]
    [InlineData(20000)]
    [InlineData(100000)]
    public async Task Large_metadata_diagnostic_records_fresh_and_repeated_read_cost_with_fixed_journal_hydration(int historyCount) {
        await using var fixture = await AgentToolAdmissionJournalFixture.CreateAsync();
        var process = Guid.NewGuid();
        var selected = Enumerable.Range(0, 3).Select(_ => Run(fixture, process)).ToArray();
        await fixture.Store.UpdateExecutionAsync(state => state with { ExecutionRuns = [.. state.ExecutionRuns, .. selected] });
        var layout = Layout(fixture);
        var serializer = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var index = JsonSerializer.Deserialize<ExecutionChatIndex>(await File.ReadAllTextAsync(layout.ExecutionChatIndexPath), serializer)!;
        var execution = JsonSerializer.Deserialize<ExecutionStorageIndex>(await File.ReadAllTextAsync(layout.ExecutionIndexPath), serializer)!;
        var template = index.RunSummaries.First(summary => summary.ExecutionRunId == selected[0].Id);
        var summaries = index.RunSummaries.Concat(Enumerable.Range(0, historyCount).Select(_ => template with {
            ExecutionRunId = Guid.NewGuid(), ProcessRunId = Guid.NewGuid().ToString("D")
        })).ToArray();
        await using (var stream = File.Create(layout.ExecutionChatIndexPath)) {
            await JsonSerializer.SerializeAsync(stream, index with { RunSummaries = summaries }, serializer);
        }
        await using (var stream = File.Create(layout.ExecutionIndexPath)) {
            await JsonSerializer.SerializeAsync(stream, execution with { RunCount = summaries.Length }, serializer);
        }
        var preparation = Stopwatch.StartNew();
        Assert.Equal(ProcessExecutionRunPageStatus.Ready, await fixture.Store.PrepareProcessExecutionRunIndexAsync());
        preparation.Stop();
        var reads = new List<FileSandboxWorkspacePhysicalJsonRead>();
        var store = Observed(fixture, reads);
        for (var attempt = 0; attempt < 2; attempt++) {
            reads.Clear();
            var allocated = GC.GetTotalAllocatedBytes(precise: true);
            var watch = Stopwatch.StartNew();
            var page = await store.ReadProcessExecutionRunPageAsync(new(process, Take: 2));
            watch.Stop();
            allocated = GC.GetTotalAllocatedBytes(precise: true) - allocated;
            Assert.Equal(ProcessExecutionRunPageStatus.Ready, page.Status);
            Assert.Equal(2, page.Candidates.Count);
            Assert.False(page.Exhausted);
            Assert.Equal(2, reads.Count(read => read.PayloadType == typeof(ExecutionRunRecord)));
            Assert.Equal(selected.Select(run => run.Id).Order().Take(2), page.Candidates.Select(run => run.Id));
            output.WriteLine(JsonSerializer.Serialize(new { historyCount, attempt, elapsedMilliseconds = watch.Elapsed.TotalMilliseconds,
                allocatedBytes = allocated, metadataBytes = new FileInfo(layout.ExecutionProcessIndexPath).Length,
                sourceMetadataBytes = new FileInfo(layout.ExecutionChatIndexPath).Length, preparationMilliseconds = preparation.Elapsed.TotalMilliseconds,
                physicalReadBytes = reads.Sum(read => read.LengthBytes), journalReads = 2,
                scope = "Synthetic metadata population; actual selected run owner; filesystem cache uncontrolled; not sustained producer throughput." }));
        }
    }

    private static FileSandboxWorkspaceStore Observed(AgentToolAdmissionJournalFixture fixture, List<FileSandboxWorkspacePhysicalJsonRead> reads)
        => new(fixture.WorkspaceRoot, fixture.StorageScope, null, null, new FileSandboxWorkspaceJsonReadDiagnostics(reads.Add));
    private static FileSandboxWorkspaceStorageLayout Layout(AgentToolAdmissionJournalFixture fixture) => new(fixture.WorkspaceRoot, fixture.StorageScope);
    private static async Task<JsonNode> ReadIndex(FileSandboxWorkspaceStorageLayout layout)
        => JsonNode.Parse(await File.ReadAllTextAsync(layout.ExecutionChatIndexPath))!;
    private static async Task<string?> IndexBytes(FileSandboxWorkspaceStorageLayout layout)
        => File.Exists(layout.ExecutionProcessIndexPath) ? await File.ReadAllTextAsync(layout.ExecutionProcessIndexPath) : null;
    private static ExecutionRunRecord Run(AgentToolAdmissionJournalFixture fixture, Guid process) => fixture.Detail.Run with {
        Id = Guid.NewGuid(), ChatSessionId = null, Title = "Indexed run", SourceKind = "process-step", SourceId = "page-proof",
        ProcessRunId = process.ToString("D"), ProcessStepId = Guid.NewGuid().ToString("D"), ToolAdmission = null
    };
    public enum UnavailableIndex { Missing, Stale, LegacyLayout }
}
