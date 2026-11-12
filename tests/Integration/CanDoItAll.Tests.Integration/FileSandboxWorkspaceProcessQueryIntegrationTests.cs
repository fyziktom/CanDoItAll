using System.Text.Json.Nodes;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;

namespace CanDoItAll.Tests.Integration.Runtime;

[Trait("Category", "FileSystemPortability")]
[Trait("Category", "HostPlatform")]
public sealed class FileSandboxWorkspaceProcessQueryIntegrationTests {
    [Theory]
    [InlineData(4)]
    [InlineData(96)]
    public async Task Process_query_reads_only_selected_current_records(int unrelatedCount) {
        await using var fixture = await AgentToolAdmissionJournalFixture.CreateAsync();
        var processId = Guid.NewGuid().ToString("D");
        var selected = CreateRun(fixture, processId);
        var otherStep = CreateRun(fixture, processId);
        var history = Enumerable.Range(0, unrelatedCount).Select(_ => CreateRun(fixture, Guid.NewGuid().ToString("D"))).ToArray();
        await fixture.Store.UpdateExecutionAsync(current => current with {
            ExecutionRuns = [.. current.ExecutionRuns, selected, otherStep, .. history]
        });
        var reads = new List<FileSandboxWorkspacePhysicalJsonRead>();
        var store = new FileSandboxWorkspaceStore(fixture.WorkspaceRoot, fixture.StorageScope, null, null,
            new FileSandboxWorkspaceJsonReadDiagnostics(reads.Add));

        var runs = await store.ListExecutionRunCandidatesAsync(new(ProcessRunId: processId.ToUpperInvariant()));

        Assert.Equal(new[] { selected.Id, otherStep.Id }.Order(), runs.Select(run => run.Id).Order());
        Assert.Equal(2, reads.Count(read => read.PayloadType == typeof(ExecutionRunRecord)));
        Assert.DoesNotContain(reads, read => history.Any(run => read.FullPath.Contains(run.Id.ToString("N"), StringComparison.Ordinal)));
        Assert.All(runs, run => Assert.NotNull(run.MetadataJson));
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("stale")]
    [InlineData("legacy")]
    [InlineData("older-reporting")]
    [InlineData("newer-reporting")]
    public async Task Index_recovery_never_omits_a_matching_run(string indexState) {
        await using var fixture = await AgentToolAdmissionJournalFixture.CreateAsync();
        var run = CreateRun(fixture, Guid.NewGuid().ToString("D"));
        await fixture.Store.SaveExecutionRunDetailAsync(new(run, null, [], []));
        var layout = new FileSandboxWorkspaceStorageLayout(fixture.WorkspaceRoot, fixture.StorageScope);
        var index = JsonNode.Parse(await File.ReadAllTextAsync(layout.ExecutionChatIndexPath))!;
        if (indexState == "missing") {
            File.Delete(layout.ExecutionChatIndexPath);
        } else {
            if (indexState == "stale") {
                index["revision"] = 0;
                index["runSummaries"] = new JsonArray();
            } else {
                foreach (var summary in index["runSummaries"]!.AsArray()) {
                    summary!["reportingProjectionVersion"] = indexState switch {
                        "older-reporting" => WorkspaceChatProjectionBuilder.CurrentReportingProjectionVersion - 1,
                        "newer-reporting" => WorkspaceChatProjectionBuilder.CurrentReportingProjectionVersion + 1,
                        _ => 0
                    };
                    summary["processRunId"] = "";
                }
            }
            await File.WriteAllTextAsync(layout.ExecutionChatIndexPath, index.ToJsonString());
        }

        var candidates = await fixture.NewStore().ListExecutionRunCandidatesAsync(new(ProcessRunId: run.ProcessRunId));

        Assert.Contains(candidates, item => item.Id == run.Id && item.ProcessRunId == run.ProcessRunId);
    }

    [Fact]
    public async Task Independent_commit_and_same_revision_edit_are_visible() {
        await using var fixture = await AgentToolAdmissionJournalFixture.CreateAsync();
        var run = CreateRun(fixture, Guid.NewGuid().ToString("D"));
        var query = new ExecutionRunQuery(ProcessRunId: run.ProcessRunId);
        Assert.Empty(await fixture.Store.ListExecutionRunCandidatesAsync(query));
        await fixture.NewStore().SaveExecutionRunDetailAsync(new(run, null, [], []));
        Assert.Equal(run.Id, Assert.Single(await fixture.Store.ListExecutionRunCandidatesAsync(query)).Id);
        var layout = new FileSandboxWorkspaceStorageLayout(fixture.WorkspaceRoot, fixture.StorageScope);
        var path = layout.RunPath(run.Id);
        var originalTime = File.GetLastWriteTimeUtc(path);
        var raw = await File.ReadAllTextAsync(path);
        await File.WriteAllTextAsync(path, raw.Replace("Indexed run", "Changed run", StringComparison.Ordinal));
        File.SetLastWriteTimeUtc(path, originalTime);

        var current = Assert.Single(await fixture.Store.ListExecutionRunCandidatesAsync(query));

        Assert.Equal("Changed run", current.Title);
        Assert.Equal(run.Revision, current.Revision);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Missing_or_corrupt_selected_record_is_an_explicit_failure(bool missing) {
        await using var fixture = await AgentToolAdmissionJournalFixture.CreateAsync();
        var run = CreateRun(fixture, Guid.NewGuid().ToString("D"));
        await fixture.Store.SaveExecutionRunDetailAsync(new(run, null, [], []));
        var layout = new FileSandboxWorkspaceStorageLayout(fixture.WorkspaceRoot, fixture.StorageScope);
        if (missing) {
            File.Delete(layout.RunPath(run.Id));
        } else {
            await File.WriteAllTextAsync(layout.RunPath(run.Id), "{invalid");
        }

        await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Store.ListExecutionRunCandidatesAsync(new(ProcessRunId: run.ProcessRunId)));
    }

    [Fact]
    public async Task Candidate_read_recovers_a_prepared_commit_before_selecting() {
        await using var fixture = await AgentToolAdmissionJournalFixture.CreateAsync();
        var run = CreateRun(fixture, Guid.NewGuid().ToString("D"));
        await fixture.Store.SaveExecutionRunDetailAsync(new(run, null, [], []));
        var faulted = fixture.NewStore(stage => {
            if (stage == ExistingRunDetailCommitStage.JournalPersisted) {
                throw new IOException("candidate-read recovery fault");
            }
        });
        await Assert.ThrowsAsync<IOException>(() => faulted.UpdateExecutionRunDetailAsync(run.Id,
            detail => detail with { Run = detail.Run with { Title = "Recovered run", Revision = run.Revision + 1 } }));

        var current = Assert.Single(await fixture.NewStore().ListExecutionRunCandidatesAsync(new(ProcessRunId: run.ProcessRunId)));

        Assert.Equal("Recovered run", current.Title);
        Assert.Equal(run.Revision + 1, current.Revision);
    }

    [Fact]
    public async Task Exact_execution_selection_does_not_enumerate_unrelated_corrupt_records() {
        await using var fixture = await AgentToolAdmissionJournalFixture.CreateAsync();
        var run = CreateRun(fixture, Guid.NewGuid().ToString("D"));
        await fixture.Store.SaveExecutionRunDetailAsync(new(run, null, [], []));
        var layout = new FileSandboxWorkspaceStorageLayout(fixture.WorkspaceRoot, fixture.StorageScope);
        await File.WriteAllTextAsync(layout.RunPath(fixture.Detail.Run.Id), "{invalid");

        Assert.Equal(run.Id, Assert.Single(await fixture.Store.ListExecutionRunCandidatesAsync(new() { ExecutionRunId = run.Id })).Id);
        Assert.Empty(await fixture.Store.ListExecutionRunCandidatesAsync(new() { ExecutionRunId = Guid.NewGuid() }));
    }

    private static ExecutionRunRecord CreateRun(AgentToolAdmissionJournalFixture fixture, string processId) => fixture.Detail.Run with {
        Id = Guid.NewGuid(), ChatSessionId = null, Title = "Indexed run", SourceKind = "process-step",
        SourceId = "query-proof", ProcessRunId = processId, ProcessStepId = Guid.NewGuid().ToString("D"), ToolAdmission = null
    };
}
