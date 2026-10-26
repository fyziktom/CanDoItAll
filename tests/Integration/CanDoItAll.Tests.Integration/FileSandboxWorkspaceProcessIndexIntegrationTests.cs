using System.Text.Json;
using System.Text.Json.Nodes;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;

namespace CanDoItAll.Tests.Integration.Runtime;

[Trait("Category", "FileSystemPortability")]
[Trait("Category", "HostPlatform")]
public sealed class FileSandboxWorkspaceProcessIndexIntegrationTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Explicit_preparation_uses_complete_metadata_without_hydrating_journals_or_rewriting_its_source(bool legacy) {
        await using var fixture = await AgentToolAdmissionJournalFixture.CreateAsync();
        var run = Run(fixture);
        await fixture.Store.SaveExecutionRunDetailAsync(new(run, null, [], []));
        var layout = Layout(fixture);
        var source = await Json(layout.ExecutionChatIndexPath);
        if (legacy) {
            source["reportingProjectionVersion"] = 0;
            foreach (var row in source["runSummaries"]!.AsArray()) {
                row!["reportingProjectionVersion"] = 0;
                row["processRunId"] = "";
            }
            await File.WriteAllTextAsync(layout.ExecutionChatIndexPath, source.ToJsonString());
        }
        File.Delete(layout.ExecutionProcessIndexPath);
        var before = await File.ReadAllBytesAsync(layout.ExecutionChatIndexPath);
        var reads = new List<FileSandboxWorkspacePhysicalJsonRead>();
        var owner = Observed(fixture, reads);
        Assert.Equal(ProcessExecutionRunPageStatus.IndexUnavailable, (await owner.ReadProcessExecutionRunPageAsync(new(Guid.Parse(run.ProcessRunId)))).Status);

        Assert.Equal(ProcessExecutionRunPageStatus.Ready, await owner.PrepareProcessExecutionRunIndexAsync());

        Assert.DoesNotContain(reads, read => read.PayloadType == typeof(ExecutionRunRecord));
        Assert.Equal(before, await File.ReadAllBytesAsync(layout.ExecutionChatIndexPath));
        var page = await fixture.NewStore().ReadProcessExecutionRunPageAsync(new(Guid.Parse(run.ProcessRunId)));
        Assert.True(page.Exhausted);
        Assert.Contains(page.Candidates, candidate => candidate.Id == run.Id);
        Assert.Equal(legacy ? 2 : 1, page.Candidates.Count);
    }

    [Theory]
    [InlineData(SourceGap.Missing)]
    [InlineData(SourceGap.Revision)]
    [InlineData(SourceGap.Count)]
    [InlineData(SourceGap.FutureHeader)]
    [InlineData(SourceGap.FutureEntry)]
    public async Task Unavailable_source_is_not_repaired_or_reported_as_exhaustion(SourceGap gap) {
        await using var fixture = await AgentToolAdmissionJournalFixture.CreateAsync();
        var layout = Layout(fixture);
        File.Delete(layout.ExecutionProcessIndexPath);
        if (gap == SourceGap.Missing) {
            File.Delete(layout.ExecutionChatIndexPath);
        } else {
            var source = await Json(layout.ExecutionChatIndexPath);
            switch (gap) {
                case SourceGap.Revision:
                    source["revision"] = -1;
                    break;
                case SourceGap.Count:
                    source["runSummaries"] = new JsonArray();
                    break;
                case SourceGap.FutureHeader:
                    source["reportingProjectionVersion"] = WorkspaceChatProjectionBuilder.CurrentReportingProjectionVersion + 1;
                    break;
                case SourceGap.FutureEntry:
                    source["runSummaries"]![0]!["reportingProjectionVersion"] = WorkspaceChatProjectionBuilder.CurrentReportingProjectionVersion + 1;
                    break;
            }
            await File.WriteAllTextAsync(layout.ExecutionChatIndexPath, source.ToJsonString());
        }
        var before = File.Exists(layout.ExecutionChatIndexPath) ? await File.ReadAllTextAsync(layout.ExecutionChatIndexPath) : null;
        var reads = new List<FileSandboxWorkspacePhysicalJsonRead>();
        var owner = Observed(fixture, reads);

        Assert.Equal(ProcessExecutionRunPageStatus.IndexUnavailable, await owner.PrepareProcessExecutionRunIndexAsync());

        var page = await owner.ReadProcessExecutionRunPageAsync(new(Guid.NewGuid()));
        Assert.Equal(ProcessExecutionRunPageStatus.IndexUnavailable, page.Status);
        Assert.False(page.Exhausted);
        Assert.False(File.Exists(layout.ExecutionProcessIndexPath));
        Assert.DoesNotContain(reads, read => read.PayloadType == typeof(ExecutionRunRecord));
        Assert.Equal(before, File.Exists(layout.ExecutionChatIndexPath) ? await File.ReadAllTextAsync(layout.ExecutionChatIndexPath) : null);
    }

    [Fact]
    public async Task Duplicate_source_identities_refuse_preparation_before_publication() {
        await using var fixture = await AgentToolAdmissionJournalFixture.CreateAsync();
        await fixture.Store.SaveExecutionRunDetailAsync(new(Run(fixture), null, [], []));
        var layout = Layout(fixture);
        File.Delete(layout.ExecutionProcessIndexPath);
        var source = await Json(layout.ExecutionChatIndexPath);
        source["runSummaries"]![1] = source["runSummaries"]![0]!.DeepClone();
        await File.WriteAllTextAsync(layout.ExecutionChatIndexPath, source.ToJsonString());

        await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Store.PrepareProcessExecutionRunIndexAsync());

        Assert.False(File.Exists(layout.ExecutionProcessIndexPath));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Interrupted_companion_publication_retains_the_original_journal_and_recovers_once(bool newRun) {
        await using var fixture = await AgentToolAdmissionJournalFixture.CreateAsync();
        var original = Run(fixture);
        if (!newRun) {
            await fixture.Store.SaveExecutionRunDetailAsync(new(original, null, [], []));
        }
        var target = original with { Title = "Published target", Revision = newRun ? original.Revision : original.Revision + 1 };
        var layout = Layout(fixture);
        var entered = false;
        void Obstruct() {
            File.Delete(layout.ExecutionProcessIndexPath);
            Directory.CreateDirectory(layout.ExecutionProcessIndexPath);
            entered = true;
        }
        var faulted = new FileSandboxWorkspaceStore(fixture.WorkspaceRoot, fixture.StorageScope, null,
            stage => {
                if (!newRun && stage == ExistingRunDetailCommitStage.RemainingRecordsPersisted) {
                    Obstruct();
                }
            },
            stage => {
                if (newRun && stage == GenericNewRunCommitStage.ExecutionIndexPersisted) {
                    Obstruct();
                }
            }, null);

        var failure = await Record.ExceptionAsync(() => faulted.SaveExecutionRunDetailAsync(new(target, null, [], [])));

        Assert.NotNull(failure);
        Assert.True(entered);
        var canonical = await Json(layout.ExecutionIndexPath);
        var chat = await Json(layout.ExecutionChatIndexPath);
        Assert.Equal(canonical["revision"]!.GetValue<long>(), chat["revision"]!.GetValue<long>());
        Assert.Equal(target.Title, JsonSerializer.Deserialize<ExecutionRunRecord>(await File.ReadAllTextAsync(layout.RunPath(target.Id)), new JsonSerializerOptions(JsonSerializerDefaults.Web))!.Title);
        Assert.True(Directory.Exists(layout.ExecutionProcessIndexPath));
        var query = new ProcessExecutionRunPageQuery(Guid.Parse(target.ProcessRunId));
        Assert.Equal(ProcessExecutionRunPageStatus.PublicationPending, (await fixture.NewStore().ReadProcessExecutionRunPageAsync(query)).Status);
        Assert.Equal(ProcessExecutionRunPageStatus.PublicationPending, await fixture.NewStore().PrepareProcessExecutionRunIndexAsync());

        Directory.Delete(layout.ExecutionProcessIndexPath);
        var recovered = (await fixture.NewStore().GetExecutionRunDetailAsync(target.Id))!.Run;
        Assert.Equal(target.Revision, recovered.Revision);
        Assert.Equal(target.Title, recovered.Title);
        var page = await fixture.NewStore().ReadProcessExecutionRunPageAsync(query);
        Assert.Equal(target.Id, Assert.Single(page.Candidates).Id);
        Assert.True(page.Exhausted);
        var index = await File.ReadAllBytesAsync(layout.ExecutionProcessIndexPath);
        await fixture.NewStore().GetExecutionRunDetailAsync(target.Id);
        Assert.Equal(index, await File.ReadAllBytesAsync(layout.ExecutionProcessIndexPath));
        Assert.Equal(target.Revision, (await fixture.NewStore().GetExecutionRunAsync(target.Id))!.Revision);
    }

    [Fact]
    public async Task Bulk_addition_removal_and_session_update_publish_current_selection_evidence() {
        await using var fixture = await AgentToolAdmissionJournalFixture.CreateAsync();
        var run = Run(fixture);
        var query = new ProcessExecutionRunPageQuery(Guid.Parse(run.ProcessRunId));
        await fixture.Store.UpdateExecutionAsync(state => state with { ExecutionRuns = [.. state.ExecutionRuns, run] });
        Assert.Equal(run.Id, Assert.Single((await fixture.NewStore().ReadProcessExecutionRunPageAsync(query)).Candidates).Id);

        var session = fixture.Detail.ChatSession!;
        await fixture.Store.RenameChatSessionAsync(new(session.AgentId, session.Id, session.Title, "Renamed session", session.UpdatedAtUtc));

        Assert.Equal(run.Id, Assert.Single((await fixture.NewStore().ReadProcessExecutionRunPageAsync(query)).Candidates).Id);
        await fixture.Store.UpdateExecutionAsync(state => state with { ExecutionRuns = state.ExecutionRuns.Where(item => item.Id != run.Id).ToArray() });
        var empty = await fixture.NewStore().ReadProcessExecutionRunPageAsync(query);
        Assert.Equal(ProcessExecutionRunPageStatus.Ready, empty.Status);
        Assert.True(empty.Exhausted);
        Assert.Empty(empty.Candidates);
    }

    [Theory]
    [InlineData("schemaVersion")]
    [InlineData("supportedProjection")]
    [InlineData("runCount")]
    [InlineData("nonProcessRunCount")]
    [InlineData("runs")]
    [InlineData("revision")]
    public async Task Required_selection_fields_cannot_default_to_an_empty_success(string field) {
        await using var fixture = await AgentToolAdmissionJournalFixture.CreateAsync();
        var path = Layout(fixture).ExecutionProcessIndexPath;
        var index = await Json(path);
        Assert.True(index.AsObject().Remove(field));
        await File.WriteAllTextAsync(path, index.ToJsonString());

        await Assert.ThrowsAsync<InvalidDataException>(() => fixture.NewStore().ReadProcessExecutionRunPageAsync(new(Guid.NewGuid())));
    }

    [Fact]
    public async Task Pre_cancelled_preparation_does_not_read_or_publish() {
        await using var fixture = await AgentToolAdmissionJournalFixture.CreateAsync();
        var reads = new List<FileSandboxWorkspacePhysicalJsonRead>();
        var before = await File.ReadAllBytesAsync(Layout(fixture).ExecutionProcessIndexPath);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Observed(fixture, reads).PrepareProcessExecutionRunIndexAsync(cancellation.Token));

        Assert.Empty(reads);
        Assert.Equal(before, await File.ReadAllBytesAsync(Layout(fixture).ExecutionProcessIndexPath));
    }

    [Fact]
    public async Task Same_stamp_index_changes_are_reread_instead_of_using_cached_membership() {
        await using var fixture = await AgentToolAdmissionJournalFixture.CreateAsync();
        var run = Run(fixture);
        await fixture.Store.SaveExecutionRunDetailAsync(new(run, null, [], []));
        var query = new ProcessExecutionRunPageQuery(Guid.Parse(run.ProcessRunId));
        Assert.Equal(run.Id, Assert.Single((await fixture.Store.ReadProcessExecutionRunPageAsync(query)).Candidates).Id);
        var path = Layout(fixture).ExecutionProcessIndexPath;
        var stamp = File.GetLastWriteTimeUtc(path);
        var bytes = await File.ReadAllTextAsync(path);
        await File.WriteAllTextAsync(path, bytes.Replace(run.Id.ToString("D"), Guid.NewGuid().ToString("D"), StringComparison.Ordinal));
        File.SetLastWriteTimeUtc(path, stamp);

        await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Store.ReadProcessExecutionRunPageAsync(query));
    }

    [Fact]
    public async Task Explicit_preparation_repairs_a_corrupt_projection_without_rewriting_business_evidence() {
        await using var fixture = await AgentToolAdmissionJournalFixture.CreateAsync();
        var run = Run(fixture);
        await fixture.Store.SaveExecutionRunDetailAsync(new(run, null, [], []));
        var layout = Layout(fixture);
        var before = await File.ReadAllBytesAsync(layout.RunPath(run.Id));
        var query = new ProcessExecutionRunPageQuery(Guid.Parse(run.ProcessRunId));
        await File.WriteAllTextAsync(layout.ExecutionProcessIndexPath, "{invalid");
        await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Store.ReadProcessExecutionRunPageAsync(query));

        Assert.Equal(ProcessExecutionRunPageStatus.Ready, await fixture.Store.PrepareProcessExecutionRunIndexAsync());

        Assert.Equal(run.Id, Assert.Single((await fixture.Store.ReadProcessExecutionRunPageAsync(query)).Candidates).Id);
        Assert.Equal(before, await File.ReadAllBytesAsync(layout.RunPath(run.Id)));
    }

    [Fact]
    public async Task Agent_deletion_removes_only_its_original_membership_from_the_compact_projection() {
        await using var fixture = await AgentToolAdmissionJournalFixture.CreateAsync();
        var agent = fixture.Agent with { Id = Guid.NewGuid(), Name = "Disposable selection", IsTemplate = false, TemplateKey = string.Empty, ConfigurationJson = "{}" };
        await fixture.Store.UpdateCatalogAsync(catalog => catalog with { Agents = [.. catalog.Agents, agent] });
        var run = Run(fixture) with {
            AgentId = agent.Id, State = ExecutionState.Completed, Outcome = RunOutcome.Succeeded,
            CompletedAtUtc = fixture.Detail.Run.UpdatedAtUtc, PendingApprovals = []
        };
        var survivor = Run(fixture) with { ProcessRunId = run.ProcessRunId };
        await fixture.Store.SaveExecutionRunDetailAsync(new(run, null, [], []));
        await fixture.Store.SaveExecutionRunDetailAsync(new(survivor, null, [], []));

        var deleted = await fixture.Store.DeleteAgentWorkspaceDataAsync(agent.Id);

        Assert.True(deleted.Deleted);
        Assert.Equal(1, deleted.DeletedExecutionRunCount);
        var page = await fixture.NewStore().ReadProcessExecutionRunPageAsync(new(Guid.Parse(run.ProcessRunId)));
        Assert.Equal(survivor.Id, Assert.Single(page.Candidates).Id);
        Assert.True(page.Exhausted);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task An_orphan_projection_without_a_canonical_index_is_not_an_empty_workspace(bool compact) {
        await using var fixture = await AgentToolAdmissionJournalFixture.CreateAsync();
        var root = Path.Combine(fixture.WorkspaceRoot, "orphan-projection");
        var layout = new FileSandboxWorkspaceStorageLayout(root);
        Directory.CreateDirectory(layout.ExecutionStorageRoot);
        await File.WriteAllTextAsync(compact ? layout.ExecutionProcessIndexPath : layout.ExecutionChatIndexPath, "{}");
        var store = new FileSandboxWorkspaceStore(root);

        var page = await store.ReadProcessExecutionRunPageAsync(new(Guid.NewGuid()));

        Assert.Equal(ProcessExecutionRunPageStatus.LayoutUnavailable, page.Status);
        Assert.False(page.Exhausted);
        Assert.Equal(ProcessExecutionRunPageStatus.LayoutUnavailable, await store.PrepareProcessExecutionRunIndexAsync());
        Assert.False(File.Exists(layout.ExecutionIndexPath));
    }

    private static FileSandboxWorkspaceStore Observed(AgentToolAdmissionJournalFixture fixture, List<FileSandboxWorkspacePhysicalJsonRead> reads)
        => new(fixture.WorkspaceRoot, fixture.StorageScope, null, null, new FileSandboxWorkspaceJsonReadDiagnostics(reads.Add));
    private static FileSandboxWorkspaceStorageLayout Layout(AgentToolAdmissionJournalFixture fixture) => new(fixture.WorkspaceRoot, fixture.StorageScope);
    private static async Task<JsonNode> Json(string path) => JsonNode.Parse(await File.ReadAllTextAsync(path))!;
    private static ExecutionRunRecord Run(AgentToolAdmissionJournalFixture fixture) => fixture.Detail.Run with {
        Id = Guid.NewGuid(), ChatSessionId = null, ToolAdmission = null, Title = "Selection source",
        SourceKind = "process-step", SourceId = "selection-proof", ProcessRunId = Guid.NewGuid().ToString("D"), ProcessStepId = Guid.NewGuid().ToString("D")
    };
    public enum SourceGap { Missing, Revision, Count, FutureHeader, FutureEntry }
}
