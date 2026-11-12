using System.Diagnostics;
using System.Text.Json.Nodes;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;

namespace CanDoItAll.Tests.Integration.Runtime;

[Trait("Category", "FileSystemPortability")]
[Trait("Category", "HostPlatform")]
public sealed class FileSandboxWorkspaceSnapshotReuseIntegrationTests {
    [Fact]
    public async Task WarmRunReads_ReusePayloadAtSameRevision() {
        await using var fixture = await AgentToolAdmissionJournalFixture.CreateAsync();
        var owner = fixture.NewStore();
        var first = await owner.GetExecutionRunDetailAsync(fixture.Session.ExecutionRunId);
        var deserializations = 0;
        using var listener = new ActivityListener {
            ShouldListenTo = source => source.Name == AgentFrameworkTelemetry.SourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = activity => {
                if (activity.OperationName == "workspace.json.deserialize") {
                    Interlocked.Increment(ref deserializations);
                }
            }
        };
        ActivitySource.AddActivityListener(listener);

        var second = await owner.GetExecutionRunDetailAsync(fixture.Session.ExecutionRunId);

        Assert.Equal(first!.Run, second!.Run);
        Assert.Equal(0, deserializations);
    }

    [Fact]
    public async Task ExternalCommit_InvalidatesWarmSnapshot() {
        await using var fixture = await AgentToolAdmissionJournalFixture.CreateAsync();
        var original = await fixture.Store.GetExecutionRunDetailAsync(fixture.Session.ExecutionRunId);
        var saved = await fixture.NewStore().UpdateExecutionRunDetailAsync(fixture.Session.ExecutionRunId,
            detail => detail with { Run = detail.Run with { Title = "Independent owner", Revision = detail.Run.Revision + 1 } });

        var current = await fixture.Store.GetExecutionRunDetailAsync(fixture.Session.ExecutionRunId);

        Assert.Equal("Independent owner", current!.Run.Title);
        Assert.Equal(saved.Run.Revision, current.Run.Revision);
        Assert.NotEqual(original!.Run.Revision, current.Run.Revision);
        Assert.NotEqual("Independent owner", original.Run.Title);
    }

    [Fact]
    public async Task SameRevisionRawEdit_CannotHideBehindWarmSnapshot() {
        await using var fixture = await AgentToolAdmissionJournalFixture.CreateAsync();
        var original = await fixture.Store.GetExecutionRunDetailAsync(fixture.Session.ExecutionRunId);
        var layout = new FileSandboxWorkspaceStorageLayout(fixture.WorkspaceRoot, fixture.StorageScope);
        var path = layout.RunPath(fixture.Session.ExecutionRunId);
        var raw = JsonNode.Parse(await File.ReadAllTextAsync(path))!.AsObject();
        raw["title"] = "External raw edit";
        await File.WriteAllTextAsync(path, raw.ToJsonString());

        var current = await fixture.Store.GetExecutionRunDetailAsync(fixture.Session.ExecutionRunId);

        Assert.Equal(original!.Run.Revision, current!.Run.Revision);
        Assert.Equal("External raw edit", current.Run.Title);
        Assert.NotEqual("External raw edit", original.Run.Title);
    }

    [Fact]
    public async Task FailedFill_DoesNotPoisonNextRead() {
        await using var fixture = await AgentToolAdmissionJournalFixture.CreateAsync();
        await fixture.Store.GetExecutionRunDetailAsync(fixture.Session.ExecutionRunId);
        var layout = new FileSandboxWorkspaceStorageLayout(fixture.WorkspaceRoot, fixture.StorageScope);
        var path = layout.RunPath(fixture.Session.ExecutionRunId);
        var original = await File.ReadAllTextAsync(path);
        await File.WriteAllTextAsync(path, "{invalid");

        await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Store.GetExecutionRunDetailAsync(fixture.Session.ExecutionRunId));
        await File.WriteAllTextAsync(path, original);

        var recovered = await fixture.Store.GetExecutionRunDetailAsync(fixture.Session.ExecutionRunId);
        Assert.Equal(fixture.Detail.Run.Title, recovered!.Run.Title);
        Assert.Equal(fixture.Session, recovered.Run.ToolAdmission!.Session.Reference);
    }

    [Fact]
    public async Task Utf8Preamble_PreservesLegacyReadCompatibility() {
        await using var fixture = await AgentToolAdmissionJournalFixture.CreateAsync();
        var layout = new FileSandboxWorkspaceStorageLayout(fixture.WorkspaceRoot, fixture.StorageScope);
        var path = layout.RunPath(fixture.Session.ExecutionRunId);
        var original = await File.ReadAllTextAsync(path);
        await File.WriteAllTextAsync(path, original, new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

        var first = await fixture.Store.GetExecutionRunAsync(fixture.Session.ExecutionRunId);
        var second = await fixture.Store.GetExecutionRunAsync(fixture.Session.ExecutionRunId);

        Assert.Equal(fixture.Detail.Run.Title, first!.Title);
        Assert.Equal(first, second);
    }

    [Fact]
    public async Task CancelledWaiter_DoesNotCancelCoordinatedOwner() {
        await using var fixture = await AgentToolAdmissionJournalFixture.CreateAsync();
        await fixture.Store.GetExecutionRunDetailAsync(fixture.Session.ExecutionRunId);
        using var release = new ManualResetEventSlim();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var writer = Task.Run(() => fixture.Store.UpdateExecutionRunDetailAsync(fixture.Session.ExecutionRunId, detail => {
            entered.SetResult();
            if (!release.Wait(TimeSpan.FromSeconds(30))) {
                throw new TimeoutException("The test did not release the coordinated owner.");
            }
            return detail with { Run = detail.Run with { Title = "Committed", Revision = detail.Run.Revision + 1 } };
        }));
        try {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(30));
            using var cancellation = new CancellationTokenSource();
            var cancelledRead = fixture.Store.GetExecutionRunDetailAsync(fixture.Session.ExecutionRunId, cancellation.Token);
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelledRead);
        } finally {
            release.Set();
        }
        await writer;

        Assert.Equal("Committed", (await fixture.Store.GetExecutionRunDetailAsync(fixture.Session.ExecutionRunId))!.Run.Title);
    }

    [Fact]
    public async Task ValidatedJournalClone_StillRejectsInvalidState() {
        await using var fixture = await AgentToolAdmissionJournalFixture.CreateAsync();
        var journal = fixture.Detail.Run.ToolAdmission!;
        journal.Validate();

        Assert.Throws<InvalidDataException>(() => (journal with { SchemaVersion = -1 }).Validate());
        Assert.Throws<InvalidDataException>(() => (journal with { Revision = 0 }).Validate());
        journal.Validate();
    }

    [Fact]
    public async Task AdmissionSnapshot_DoesNotHydrateReportingRecords() {
        await using var fixture = await AgentToolAdmissionJournalFixture.CreateAsync();
        var layout = new FileSandboxWorkspaceStorageLayout(fixture.WorkspaceRoot, fixture.StorageScope);
        var logs = layout.RunLogsRoot(fixture.Session.ExecutionRunId);
        Directory.CreateDirectory(logs);
        await File.WriteAllTextAsync(Path.Combine(logs, $"{Guid.NewGuid():N}.json"), "{invalid-report");

        var snapshot = await fixture.Store.GetExecutionRunAdmissionSnapshotAsync(fixture.Session.ExecutionRunId);

        Assert.Equal(fixture.Session, snapshot!.Run.ToolAdmission!.Session.Reference);
        await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Store.GetExecutionRunDetailAsync(fixture.Session.ExecutionRunId));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task WarmCache_CurrentAuthorityStillPreventsDispatch(bool revokeReadAuthority) {
        await using var fixture = await AgentToolAdmissionJournalFixture.CreateAsync();
        var journal = fixture.NewJournal();
        await using var lease = await journal.AcquireRunAsync(fixture.Session, default);
        using var bound = lease.Bind();
        await journal.BeginSegmentAsync(lease, AgentToolAdmissionJournalFixture.Envelope(), null, default);
        var payload = AgentToolAdmissionJournalFixture.Payload(AgentToolProposalRecovery.RevalidateAndRead);
        var admitted = await journal.AdmitBatchAsync(lease, payload.Digest, AgentToolAdmissionJournalFixture.Envelope(),
            [new("call", payload, false)], default);
        await journal.ReadAsync(lease, default);
        await journal.ReadAsync(lease, default);
        await fixture.NewStore().UpdateExecutionRunDetailAsync(fixture.Session.ExecutionRunId, detail => {
            var metadata = JsonNode.Parse(detail.Run.MetadataJson)!.AsObject();
            if (revokeReadAuthority) {
                var authority = metadata[AgentTurnContextMetadata.ExecutionAuthorityMetadataKey]!.AsObject();
                authority["readAllowed"] = false;
                authority["mutationAllowed"] = false;
            }
            return detail with { Run = detail.Run with {
                State = revokeReadAuthority ? detail.Run.State : ExecutionState.Completed,
                Outcome = revokeReadAuthority ? detail.Run.Outcome : RunOutcome.Cancelled,
                MetadataJson = metadata.ToJsonString(), Revision = detail.Run.Revision + 1
            } };
        });

        await Assert.ThrowsAsync<AgentToolAdmissionException>(() => journal.ClaimInvocationAsync(lease,
            admitted.Batches[0].Id, "call", payload, default));

        var persisted = await fixture.NewStore().GetExecutionRunAsync(fixture.Session.ExecutionRunId);
        Assert.Null(persisted!.ToolAdmission!.Batches[0].Proposals[0].DispatchClaimId);
    }

    [Fact]
    public async Task ProfileSwitch_CannotReuseOldSnapshot() {
        await using var fixture = await AgentToolAdmissionJournalFixture.CreateAsync();
        await fixture.Store.GetExecutionRunAdmissionSnapshotAsync(fixture.Session.ExecutionRunId);
        var journal = new AgentToolAdmissionJournal(fixture.Store,
            new(fixture.Profile.ProfileId, fixture.Profile.Fingerprint, new(fixture.Profile.Generation.Value + 1)));

        await Assert.ThrowsAsync<AgentToolAdmissionException>(async () => await journal.AcquireRunAsync(fixture.Session, default));

        Assert.Null((await fixture.NewStore().GetExecutionRunAsync(fixture.Session.ExecutionRunId))!.ToolAdmission!.ActiveDispatchLeaseId);
    }

    [Fact]
    public async Task ParallelCommitFailure_RetainsCoordinationUntilAllWritesSettle() {
        await using var fixture = await AgentToolAdmissionJournalFixture.CreateAsync();
        using var release = new ManualResetEventSlim();
        var runPaused = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var indexFailed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var store = fixture.NewStore(stage => {
            if (stage == ExistingRunDetailCommitStage.RunPersisted) {
                runPaused.SetResult();
                if (!release.Wait(TimeSpan.FromSeconds(30))) {
                    throw new TimeoutException("The test did not release the run write.");
                }
            }
            if (stage == ExistingRunDetailCommitStage.ExecutionIndexPersisted) {
                indexFailed.SetResult();
                throw new IOException("Injected concurrent index acknowledgement failure.");
            }
        });
        var writer = Task.Run(() => store.UpdateExecutionRunDetailAsync(fixture.Session.ExecutionRunId, detail =>
            detail with { Run = detail.Run with { Title = "Recovered concurrent commit", Revision = detail.Run.Revision + 1 } }));
        Exception? failure;
        try {
            await Task.WhenAll(runPaused.Task, indexFailed.Task).WaitAsync(TimeSpan.FromSeconds(30));
            Assert.False(writer.IsCompleted);
            using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(250));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                fixture.NewStore().GetExecutionRunDetailAsync(fixture.Session.ExecutionRunId, cancellation.Token));
        } finally {
            release.Set();
            failure = await Record.ExceptionAsync(() => writer);
        }
        Assert.IsType<IOException>(failure);

        var recovered = await fixture.NewStore().GetExecutionRunDetailAsync(fixture.Session.ExecutionRunId);
        Assert.Equal("Recovered concurrent commit", recovered!.Run.Title);
        Assert.Equal(fixture.Detail.Run.Revision + 1, recovered.Run.Revision);
        Assert.Equal(recovered.Run.Revision, (await fixture.NewStore().GetExecutionRunDetailAsync(fixture.Session.ExecutionRunId))!.Run.Revision);
    }
}
