using System.Text.Json;
using System.Text.Json.Nodes;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;

namespace CanDoItAll.Tests.Integration.Runtime;

[Trait("Category", "FileSystemPortability")]
[Trait("Category", "HostPlatform")]
public sealed class FileSandboxWorkspaceProcessIndexReuseTests {
    private const string SelectionRevisionProperty = "processSelectionRevision";
    private const string RevisionProperty = "revision";
    private const string PendingUpdateFileName = "pending-run-detail-update.json";

    [Fact]
    public async Task Runtime_updates_reuse_compact_bytes_and_read_the_latest_original_revision() {
        await using var fixture = await AgentToolAdmissionJournalFixture.CreateAsync();
        var run = Run(fixture);
        var other = Run(fixture);
        await fixture.Store.UpdateExecutionAsync(state => state with { ExecutionRuns = [.. state.ExecutionRuns, run, other] });
        var layout = Layout(fixture);
        var before = await File.ReadAllBytesAsync(layout.ExecutionProcessIndexPath);
        var canonical = await Json(layout.ExecutionIndexPath);
        var stamp = new DateTime(2001, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(layout.ExecutionProcessIndexPath, stamp);

        await fixture.NewStore().UpdateExecutionRunDetailAsync(run.Id, detail => detail with {
            Run = detail.Run with { Revision = detail.Run.Revision + 1, UpdatedAtUtc = other.UpdatedAtUtc.AddDays(1), Title = "New runtime title" }
        });

        Assert.Equal(before, await File.ReadAllBytesAsync(layout.ExecutionProcessIndexPath));
        Assert.Equal(stamp, File.GetLastWriteTimeUtc(layout.ExecutionProcessIndexPath));
        var after = await Json(layout.ExecutionIndexPath);
        Assert.Equal(canonical[SelectionRevisionProperty]!.GetValue<long>(), after[SelectionRevisionProperty]!.GetValue<long>());
        Assert.Equal(canonical[RevisionProperty]!.GetValue<long>() + 1, after[RevisionProperty]!.GetValue<long>());
        var page = await fixture.NewStore().ReadProcessExecutionRunPageAsync(new(Guid.Parse(run.ProcessRunId)));
        Assert.Equal(run.Revision + 1, Assert.Single(page.Candidates).Revision);
        Assert.Equal("New runtime title", page.Candidates[0].Title);
    }

    [Fact]
    public async Task A_Process_identity_change_advances_selection_even_when_counts_are_unchanged() {
        await using var fixture = await AgentToolAdmissionJournalFixture.CreateAsync();
        var run = Run(fixture);
        await fixture.Store.SaveExecutionRunDetailAsync(new(run, null, [], []));
        var layout = Layout(fixture);
        var before = await Json(layout.ExecutionIndexPath);
        var targetProcess = Guid.NewGuid();

        await fixture.NewStore().UpdateExecutionRunDetailAsync(run.Id, detail => detail with {
            Run = detail.Run with { Revision = detail.Run.Revision + 1, ProcessRunId = targetProcess.ToString("D") }
        });

        var after = await Json(layout.ExecutionIndexPath);
        Assert.Equal(before["runCount"]!.GetValue<int>(), after["runCount"]!.GetValue<int>());
        Assert.True(after[SelectionRevisionProperty]!.GetValue<long>() > before[SelectionRevisionProperty]!.GetValue<long>());
        Assert.Empty((await fixture.NewStore().ReadProcessExecutionRunPageAsync(new(Guid.Parse(run.ProcessRunId)))).Candidates);
        Assert.Equal(run.Id, Assert.Single((await fixture.NewStore().ReadProcessExecutionRunPageAsync(new(targetProcess))).Candidates).Id);
    }

    [Theory]
    [InlineData(ProjectionDamage.Missing)]
    [InlineData(ProjectionDamage.InvalidJson)]
    [InlineData(ProjectionDamage.WrongIdentity)]
    public async Task Stable_runtime_publication_restores_missing_or_different_compact_bytes(ProjectionDamage damage) {
        await using var fixture = await AgentToolAdmissionJournalFixture.CreateAsync();
        var run = Run(fixture);
        await fixture.Store.SaveExecutionRunDetailAsync(new(run, null, [], []));
        var path = Layout(fixture).ExecutionProcessIndexPath;
        if (damage == ProjectionDamage.Missing) {
            File.Delete(path);
        } else if (damage == ProjectionDamage.InvalidJson) {
            await File.WriteAllTextAsync(path, "{invalid");
        } else {
            var content = await File.ReadAllTextAsync(path);
            await File.WriteAllTextAsync(path, content.Replace(run.Id.ToString("D"), Guid.NewGuid().ToString("D"), StringComparison.Ordinal));
        }

        await fixture.NewStore().UpdateExecutionRunDetailAsync(run.Id, detail => detail with { Run = detail.Run with { Revision = detail.Run.Revision + 1 } });

        var page = await fixture.NewStore().ReadProcessExecutionRunPageAsync(new(Guid.Parse(run.ProcessRunId)));
        Assert.Equal(run.Id, Assert.Single(page.Candidates).Id);
        Assert.Equal(run.Revision + 1, page.Candidates[0].Revision);
    }

    [Fact]
    public async Task Preparation_refuses_a_source_selection_stamp_that_differs_from_the_canonical_target() {
        await using var fixture = await AgentToolAdmissionJournalFixture.CreateAsync();
        var layout = Layout(fixture);
        var source = await Json(layout.ExecutionChatIndexPath);
        source[SelectionRevisionProperty] = source[RevisionProperty]!.GetValue<long>() + 1;
        await File.WriteAllTextAsync(layout.ExecutionChatIndexPath, source.ToJsonString());
        File.Delete(layout.ExecutionProcessIndexPath);

        Assert.Equal(ProcessExecutionRunPageStatus.IndexUnavailable, await fixture.NewStore().PrepareProcessExecutionRunIndexAsync());
        Assert.False(File.Exists(layout.ExecutionProcessIndexPath));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task An_invalid_canonical_selection_stamp_cannot_report_exhaustion(bool future) {
        await using var fixture = await AgentToolAdmissionJournalFixture.CreateAsync();
        var path = Layout(fixture).ExecutionIndexPath;
        var canonical = await Json(path);
        canonical[SelectionRevisionProperty] = future ? canonical[RevisionProperty]!.GetValue<long>() + 1 : -1;
        await File.WriteAllTextAsync(path, canonical.ToJsonString());

        await Assert.ThrowsAsync<InvalidDataException>(() => fixture.NewStore().ReadProcessExecutionRunPageAsync(new(Guid.NewGuid())));
        await Assert.ThrowsAsync<InvalidDataException>(() => fixture.NewStore().SaveExecutionRunDetailAsync(new(Run(fixture), null, [], [])));
    }

    [Fact]
    public async Task Schema_one_keeps_its_exact_global_revision_binding() {
        await using var fixture = await AgentToolAdmissionJournalFixture.CreateAsync();
        var layout = Layout(fixture);
        var canonical = await Json(layout.ExecutionIndexPath);
        var compact = await Json(layout.ExecutionProcessIndexPath);
        compact["schemaVersion"] = 1;
        compact[RevisionProperty] = canonical[RevisionProperty]!.GetValue<long>();
        await File.WriteAllTextAsync(layout.ExecutionProcessIndexPath, compact.ToJsonString());
        Assert.Equal(ProcessExecutionRunPageStatus.Ready, (await fixture.NewStore().ReadProcessExecutionRunPageAsync(new(Guid.NewGuid()))).Status);

        canonical[RevisionProperty] = canonical[RevisionProperty]!.GetValue<long>() + 1;
        await File.WriteAllTextAsync(layout.ExecutionIndexPath, canonical.ToJsonString());

        var page = await fixture.NewStore().ReadProcessExecutionRunPageAsync(new(Guid.NewGuid()));
        Assert.Equal(ProcessExecutionRunPageStatus.IndexUnavailable, page.Status);
        Assert.False(page.Exhausted);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Original_legacy_wire_targets_recover_before_or_after_canonical_publication(bool canonicalPublished) {
        await using var fixture = await AgentToolAdmissionJournalFixture.CreateAsync();
        var run = Run(fixture);
        await fixture.Store.SaveExecutionRunDetailAsync(new(run, null, [], []));
        var layout = Layout(fixture);
        var pending = Path.Combine(layout.ExecutionStorageRoot, PendingUpdateFileName);
        var stage = canonicalPublished ? ExistingRunDetailCommitStage.ExecutionIndexPersisted : ExistingRunDetailCommitStage.JournalPersisted;
        await InterruptUpdate(fixture, run, stage);
        foreach (var path in new[] { pending, layout.ExecutionIndexPath, layout.ExecutionChatIndexPath }) {
            var document = await Json(path);
            RemoveSelectionStamp(document);
            await File.WriteAllTextAsync(path, document.ToJsonString());
        }
        Assert.Equal(ProcessExecutionRunPageStatus.PublicationPending,
            (await fixture.NewStore().ReadProcessExecutionRunPageAsync(new(Guid.Parse(run.ProcessRunId)))).Status);

        var recovered = (await fixture.NewStore().GetExecutionRunDetailAsync(run.Id))!;

        Assert.Equal(run.Revision + 1, recovered.Run.Revision);
        Assert.False(File.Exists(pending));
        Assert.Null((await Json(layout.ExecutionIndexPath))[SelectionRevisionProperty]);
        Assert.Equal(run.Id, Assert.Single((await fixture.NewStore().ReadProcessExecutionRunPageAsync(new(Guid.Parse(run.ProcessRunId)))).Candidates).Id);
        await fixture.NewStore().UpdateExecutionRunDetailAsync(run.Id, detail => detail with { Run = detail.Run with { Revision = detail.Run.Revision + 1 } });
        var initialized = await Json(layout.ExecutionIndexPath);
        Assert.Equal(initialized[RevisionProperty]!.GetValue<long>(), initialized[SelectionRevisionProperty]!.GetValue<long>());
    }

    [Fact]
    public async Task A_present_original_selection_stamp_cannot_downgrade_to_a_legacy_target() {
        await using var fixture = await AgentToolAdmissionJournalFixture.CreateAsync();
        var run = Run(fixture);
        await fixture.Store.SaveExecutionRunDetailAsync(new(run, null, [], []));
        await InterruptUpdate(fixture, run, ExistingRunDetailCommitStage.JournalPersisted);
        var pending = Path.Combine(Layout(fixture).ExecutionStorageRoot, PendingUpdateFileName);
        var journal = await Json(pending);
        Assert.NotNull(journal["persistencePlan"]!["previousIndex"]![SelectionRevisionProperty]);
        journal["persistencePlan"]!["targetIndex"]!.AsObject().Remove(SelectionRevisionProperty);
        await File.WriteAllTextAsync(pending, journal.ToJsonString());
        var retained = await File.ReadAllBytesAsync(pending);

        await Assert.ThrowsAsync<InvalidDataException>(() => fixture.NewStore().GetExecutionRunDetailAsync(run.Id));

        Assert.Equal(retained, await File.ReadAllBytesAsync(pending));
    }

    private static async Task InterruptUpdate(AgentToolAdmissionJournalFixture fixture, ExecutionRunRecord run, ExistingRunDetailCommitStage boundary) {
        var entered = false;
        var store = fixture.NewStore(stage => {
            if (stage == boundary) {
                entered = true;
                throw new IOException("Interrupted original publication for compatibility proof.");
            }
        });
        await Assert.ThrowsAsync<IOException>(() => store.UpdateExecutionRunDetailAsync(run.Id,
            detail => detail with { Run = detail.Run with { Revision = detail.Run.Revision + 1 } }));
        Assert.True(entered);
    }

    private static void RemoveSelectionStamp(JsonNode node) {
        if (node is JsonObject value) {
            value.Remove(SelectionRevisionProperty);
            foreach (var child in value.ToArray()) {
                if (child.Value is not null) {
                    RemoveSelectionStamp(child.Value);
                }
            }
        } else if (node is JsonArray values) {
            foreach (var child in values) {
                if (child is not null) {
                    RemoveSelectionStamp(child);
                }
            }
        }
    }

    private static FileSandboxWorkspaceStorageLayout Layout(AgentToolAdmissionJournalFixture fixture) => new(fixture.WorkspaceRoot, fixture.StorageScope);
    private static async Task<JsonNode> Json(string path) => JsonNode.Parse(await File.ReadAllTextAsync(path))!;
    private static ExecutionRunRecord Run(AgentToolAdmissionJournalFixture fixture) => fixture.Detail.Run with {
        Id = Guid.NewGuid(), ChatSessionId = null, ToolAdmission = null, Title = "Stable selection",
        SourceKind = "process-step", SourceId = "selection-reuse", ProcessRunId = Guid.NewGuid().ToString("D"), ProcessStepId = Guid.NewGuid().ToString("D")
    };

    public enum ProjectionDamage { Missing, InvalidJson, WrongIdentity }
}
