using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;

namespace CanDoItAll.Tests.Integration.Runtime;

public sealed partial class AgentToolEffectJournalIntegrationTests {
    [Fact]
    public async Task Historical_pages_preserve_original_generation_and_reach_later_intents_without_workspace_coordination_or_writes() {
        await using var source = await AgentToolAdmissionJournalFixture.CreateAsync();
        await using var fixture = await RestoreAsync(source, HistoricalRun(source, 9));
        var run = fixture.Detail.Run;
        var original = run.ToolAdmission!.Session;
        var current = new AgentToolProfileBinding(original.Profile.ProfileId, original.Profile.Fingerprint, new(0));
        Assert.NotEqual(current.Generation, original.Profile.Generation);
        using var fileObservation = await HistoricalFileObservation.StartAsync(fixture.WorkspaceRoot);
        var journal = new HistoricalJournal(fixture, current);
        var reader = new HistoricalReader(async (claim, token) => {
            var admitted = await claim.RequireAsync(token);
            Assert.Equal(original, admitted.OriginalSession);
            Assert.Equal(current, admitted.CurrentFence.Profile);
            return new(AgentHistoricalReceiptKnowledge.Accepted, ObservedReceipt);
        });
        var request = HistoricalRequest(run);
        var expected = run.ToolAdmission.Batches.SelectMany(batch => batch.Proposals).Select(item => item.IntentId).ToArray();
        var seen = new List<AgentToolBusinessIntentId>();
        var before = HistoricalFiles(fixture);
        var layout = new FileSandboxWorkspaceStorageLayout(fixture.WorkspaceRoot, fixture.StorageScope);
        await using (var coordination = new FileStream(layout.WorkspaceLockPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) {
            for (var page = 0; page < 3; page++) {
                var result = await journal.ReadHistoricalReceiptReviewAsync(request, fixture.StorageScope, HistoricalAllow, [reader]);
                Assert.Equal(request, result.Request);
                Assert.Equal(page == 2 ? AgentHistoricalReceiptReviewStop.ExhaustedSnapshot : AgentHistoricalReceiptReviewStop.LookupLimit, result.Stop);
                seen.AddRange(result.Observations.Select(item => item.Binding.IntentId));
                Assert.All(result.Observations, item => Assert.Equal(AgentHistoricalReceiptKnowledge.Accepted, item.Knowledge));
                if (page < 2) {
                    Assert.Equal((page + 1) * 4, result.NextCursor!.NextProposalOrdinal);
                    request = HistoricalRequest(run, result.NextCursor);
                } else {
                    Assert.Null(result.NextCursor);
                }
            }
            var empty = await journal.ReadHistoricalReceiptReviewAsync(HistoricalRequest(run,
                new(run.Id, run.Revision, run.ToolAdmission.Revision, 9, new AgentHistoricalReceiptReadFence(current, fixture.StorageScope).Context)),
                fixture.StorageScope, HistoricalAllow, [reader]);
            Assert.Equal(AgentHistoricalReceiptReviewStop.ExhaustedSnapshot, empty.Stop);
            Assert.Empty(empty.Observations);
            Assert.Null(empty.NextCursor);
        }
        Assert.Equal(expected, seen);
        Assert.Equal(9, reader.Calls);
        Assert.Equal(before, HistoricalFiles(fixture));
        await fileObservation.AssertNoChangesAsync();
    }

    [Fact]
    public async Task Historical_cursor_and_current_read_failures_disclose_no_snapshot_or_successor() {
        await using var source = await AgentToolAdmissionJournalFixture.CreateAsync();
        await using var fixture = await RestoreAsync(source, HistoricalRun(source, 2));
        var run = fixture.Detail.Run;
        var context = new AgentHistoricalReceiptReadContext(fixture.Profile.ProfileId, fixture.Profile.Generation, fixture.StorageScope);
        var journal = new HistoricalJournal(fixture, fixture.Profile);
        var reader = new HistoricalReader();
        var before = HistoricalFiles(fixture);
        foreach (var cursor in new[] {
            new AgentHistoricalReceiptCursor(run.Id, run.Revision + 1, run.ToolAdmission!.Revision, 0, context),
            new AgentHistoricalReceiptCursor(run.Id, run.Revision, run.ToolAdmission!.Revision + 1, 0, context),
            new AgentHistoricalReceiptCursor(run.Id, run.Revision, run.ToolAdmission!.Revision, 3, context),
            new AgentHistoricalReceiptCursor(Guid.NewGuid(), run.Revision, run.ToolAdmission!.Revision, 0, context)
        }) {
            AssertHistoricalRefused(await journal.ReadHistoricalReceiptReviewAsync(HistoricalRequest(run, cursor), fixture.StorageScope,
                HistoricalAllow, [reader]), AgentHistoricalReceiptReviewStop.SnapshotChanged);
        }
        var foreign = new AgentHistoricalReceiptCursor(run.Id, run.Revision, run.ToolAdmission!.Revision, 0,
            new(Guid.NewGuid(), fixture.Profile.Generation, fixture.StorageScope));
        AssertHistoricalRefused(await journal.ReadHistoricalReceiptReviewAsync(HistoricalRequest(run, foreign), fixture.StorageScope,
            HistoricalAllow, [reader]), AgentHistoricalReceiptReviewStop.ContextChanged);
        foreach (var decision in new[] { AgentHistoricalReceiptReadPolicy.Denied, AgentHistoricalReceiptReadPolicy.Unsupported,
            AgentHistoricalReceiptReadPolicy.ContextChanged }) {
            AssertHistoricalRefused(await journal.ReadHistoricalReceiptReviewAsync(HistoricalRequest(run), fixture.StorageScope,
                (_, _, _) => ValueTask.FromResult(decision), [reader]), decision switch {
                    AgentHistoricalReceiptReadPolicy.Denied => AgentHistoricalReceiptReviewStop.ReadDenied,
                    AgentHistoricalReceiptReadPolicy.Unsupported => AgentHistoricalReceiptReviewStop.Unsupported,
                    _ => AgentHistoricalReceiptReviewStop.ContextChanged
                });
        }
        Assert.Equal(0, reader.Calls);
        var reads = 0;
        var unavailable = await journal.ReadHistoricalReceiptReviewAsync(HistoricalRequest(run), fixture.StorageScope, (_, _, _) => {
            if (++reads == 2) {
                throw new IOException("Current read policy became unavailable.");
            }
            return ValueTask.FromResult(AgentHistoricalReceiptReadPolicy.Allowed);
        }, [reader]);
        AssertHistoricalRefused(unavailable, AgentHistoricalReceiptReviewStop.ReadUnavailable);
        Assert.Equal(2, reader.Calls);
        Assert.Equal(before, HistoricalFiles(fixture));
    }

    [Fact]
    public async Task Historical_final_witness_refuses_owner_changes_including_same_revision_byte_replacement() {
        foreach (var preserveRevision in new[] { false, true }) {
            await using var source = await AgentToolAdmissionJournalFixture.CreateAsync();
            await using var fixture = await RestoreAsync(source, HistoricalRun(source, 1));
            var run = fixture.Detail.Run;
            string[]? afterExternalWrite = null;
            var reader = new HistoricalReader(async (_, _) => {
                var layout = new FileSandboxWorkspaceStorageLayout(fixture.WorkspaceRoot, fixture.StorageScope);
                var replacement = run with { Title = "Concurrent owner replacement", Revision = preserveRevision ? run.Revision : run.Revision + 1 };
                await File.WriteAllTextAsync(layout.RunPath(run.Id), JsonSerializer.Serialize(replacement,
                    new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }));
                afterExternalWrite = HistoricalFiles(fixture);
                return new(AgentHistoricalReceiptKnowledge.Accepted, ObservedReceipt);
            });
            var result = await new HistoricalJournal(fixture, fixture.Profile).ReadHistoricalReceiptReviewAsync(HistoricalRequest(run),
                fixture.StorageScope, HistoricalAllow, [reader]);
            AssertHistoricalRefused(result, AgentHistoricalReceiptReviewStop.SnapshotChanged);
            Assert.Equal(1, reader.Calls);
            Assert.Equal(afterExternalWrite, HistoricalFiles(fixture));
        }
    }

    [Fact]
    public async Task Historical_cancellation_drains_owner_reads_and_distinguishes_owner_outage_from_read_policy_failure() {
        await using var source = await AgentToolAdmissionJournalFixture.CreateAsync();
        await using var fixture = await RestoreAsync(source, HistoricalRun(source, 2));
        var before = HistoricalFiles(fixture);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var reader = new HistoricalReader(async (_, _) => {
            entered.TrySetResult();
            await release.Task;
            return new(AgentHistoricalReceiptKnowledge.Accepted, ObservedReceipt);
        });
        using var cancellation = new CancellationTokenSource();
        var journal = new HistoricalJournal(fixture, fixture.Profile);
        var request = HistoricalRequest(fixture.Detail.Run);
        var pending = journal.ReadHistoricalReceiptReviewAsync(request, fixture.StorageScope, HistoricalAllow, [reader], cancellation.Token);
        try {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            cancellation.Cancel();
            Assert.False(pending.IsCompleted);
        } finally {
            release.TrySetResult();
        }
        AssertHistoricalRefused(await pending.WaitAsync(TimeSpan.FromSeconds(5)), AgentHistoricalReceiptReviewStop.Cancelled);
        var outage = new HistoricalReader((_, _) => throw new IOException("Owner receipt unavailable."));
        var unavailable = await journal.ReadHistoricalReceiptReviewAsync(request, fixture.StorageScope, HistoricalAllow, [outage]);
        Assert.Equal(AgentHistoricalReceiptReviewStop.ReadUnavailable, unavailable.Stop);
        Assert.Empty(unavailable.Observations);
        Assert.Equal(0, unavailable.NextCursor!.NextProposalOrdinal);
        Assert.NotNull(unavailable.RunRevision);
        var unrelatedCancellation = new HistoricalReader((_, _) => throw new OperationCanceledException("Unrelated owner cancellation."));
        await Assert.ThrowsAsync<OperationCanceledException>(() => journal.ReadHistoricalReceiptReviewAsync(request,
            fixture.StorageScope, HistoricalAllow, [unrelatedCancellation]));
        Assert.Equal(before, HistoricalFiles(fixture));
    }

    [Fact]
    public async Task Historical_storage_refuses_pending_incomplete_and_oversized_files_without_recovery_or_initialization() {
        await using var fixture = await AgentToolAdmissionJournalFixture.CreateAsync();
        var store = new FileSandboxWorkspaceHistoricalReadStore(fixture.WorkspaceRoot, fixture.StorageScope);
        var layout = new FileSandboxWorkspaceStorageLayout(fixture.WorkspaceRoot, fixture.StorageScope);
        var runId = fixture.Detail.Run.Id;
        foreach (var marker in new[] { "pending-chat-run-start.json", "pending-run-start.json", "pending-run-detail-update.json", "pending-agent-deletion.json" }) {
            var path = Path.Combine(layout.ExecutionStorageRoot, marker);
            await File.WriteAllTextAsync(path, "{invalid retained recovery journal}");
            var before = HistoricalFiles(fixture);
            try {
                Assert.Equal(AgentHistoricalStorageReadStatus.RecoveryRequired, (await store.ReadHistoricalRunAsync(runId)).Status);
                Assert.Equal(AgentHistoricalStorageReadStatus.RecoveryRequired, (await store.ReadHistoricalAgentAsync(fixture.Agent.Id)).Status);
                Assert.Equal(before, HistoricalFiles(fixture));
            } finally {
                File.Delete(path);
            }
        }
        var pendingTargets = new[] { layout.CatalogPath, layout.WorkspaceIndexPath, layout.ExecutionIndexPath,
            layout.RunPath(runId), layout.SessionPath(fixture.Detail.ChatSession!.Id),
            Path.Combine(layout.ExecutionStorageRoot, "pending-run-detail-update.json") };
        foreach (var target in pendingTargets) {
            var pending = Path.Combine(Path.GetDirectoryName(target)!, $".{Path.GetFileName(target)}.{Guid.NewGuid():N}.pending");
            await File.WriteAllTextAsync(pending, "unconfirmed publication");
            try {
                var before = HistoricalFiles(fixture);
                Assert.Equal(AgentHistoricalStorageReadStatus.RecoveryRequired, (await store.ReadHistoricalRunAsync(runId)).Status);
                Assert.Equal(before, HistoricalFiles(fixture));
            } finally {
                File.Delete(pending);
            }
        }
        var nearMatch = Path.Combine(layout.RunRoot(runId), ".run.json.not-a-guid.pending");
        await File.WriteAllTextAsync(nearMatch, "not an owner publication");
        try {
            Assert.Equal(AgentHistoricalStorageReadStatus.Ready, (await store.ReadHistoricalRunAsync(runId)).Status);
        } finally {
            File.Delete(nearMatch);
        }
        var nonmatching = Enumerable.Range(0, 4097).Select(index => Path.Combine(layout.RunRoot(runId), $"nonmatching-{index:D4}.txt")).ToArray();
        try {
            foreach (var path in nonmatching) {
                await File.WriteAllBytesAsync(path, []);
            }
            Assert.Equal(AgentHistoricalStorageReadStatus.Ready, (await store.ReadHistoricalRunAsync(runId)).Status);
        } finally {
            foreach (var path in nonmatching) {
                File.Delete(path);
            }
        }
        var index = await File.ReadAllBytesAsync(layout.ExecutionIndexPath);
        File.Delete(layout.ExecutionIndexPath);
        try {
            Assert.Equal(AgentHistoricalStorageReadStatus.UnsupportedLayout, (await store.ReadHistoricalRunAsync(runId)).Status);
            Assert.False(File.Exists(layout.ExecutionIndexPath));
        } finally {
            await File.WriteAllBytesAsync(layout.ExecutionIndexPath, index);
        }
        Assert.Equal(AgentHistoricalStorageReadStatus.Missing, (await store.ReadHistoricalRunAsync(Guid.NewGuid())).Status);
        var original = await File.ReadAllBytesAsync(layout.RunPath(runId));
        try {
            await using (var oversized = new FileStream(layout.RunPath(runId), FileMode.Create, FileAccess.Write)) {
                oversized.SetLength(FileSandboxWorkspaceHistoricalReadStore.HistoricalRawDocumentLimit + 1);
            }
            Assert.Equal(AgentHistoricalStorageReadStatus.SizeLimitExceeded, (await store.ReadHistoricalRunAsync(runId)).Status);
        } finally {
            await File.WriteAllBytesAsync(layout.RunPath(runId), original);
        }
        var absent = Path.Combine(fixture.WorkspaceRoot, "not-created-by-historical-read");
        Assert.Equal(AgentHistoricalStorageReadStatus.UnsupportedLayout,
            (await new FileSandboxWorkspaceHistoricalReadStore(absent, fixture.StorageScope).ReadHistoricalRunAsync(runId)).Status);
        Assert.False(Directory.Exists(absent));
    }

    [Fact]
    public async Task Historical_chat_projection_scans_long_history_without_materializing_messages_and_refuses_oversized_tokens() {
        await using var fixture = await AgentToolAdmissionJournalFixture.CreateAsync();
        var layout = new FileSandboxWorkspaceStorageLayout(fixture.WorkspaceRoot, fixture.StorageScope);
        var chat = fixture.Detail.ChatSession!;
        var path = layout.SessionPath(chat.Id);
        var original = await File.ReadAllBytesAsync(path);
        try {
            var messages = Enumerable.Range(0, 96).Select(_ => new { content = new string('x', 64 * 1024) }).ToArray();
            await File.WriteAllTextAsync(path, JsonSerializer.Serialize(new { id = chat.Id, agentId = chat.AgentId, messages }));
            Assert.True(new FileInfo(path).Length > 1024 * 1024);
            var before = HistoricalFiles(fixture);
            await using (var coordination = new FileStream(layout.WorkspaceLockPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) {
                var observed = await new FileSandboxWorkspaceHistoricalReadStore(fixture.WorkspaceRoot, fixture.StorageScope).ReadHistoricalRunAsync(fixture.Detail.Run.Id);
                Assert.Equal(AgentHistoricalStorageReadStatus.Ready, observed.Status);
                Assert.Equal(new AgentHistoricalChatIdentity(chat.Id, chat.AgentId), observed.Snapshot!.Chat);
            }
            Assert.Equal(before, HistoricalFiles(fixture));
            await File.WriteAllTextAsync(path, JsonSerializer.Serialize(new { id = chat.Id, agentId = chat.AgentId,
                messages = new[] { new { content = new string('x', FileSandboxWorkspaceHistoricalReadStore.HistoricalChatTokenLimit + 1) } } }));
            Assert.Equal(AgentHistoricalStorageReadStatus.SizeLimitExceeded,
                (await new FileSandboxWorkspaceHistoricalReadStore(fixture.WorkspaceRoot, fixture.StorageScope).ReadHistoricalRunAsync(fixture.Detail.Run.Id)).Status);
        } finally {
            await File.WriteAllBytesAsync(path, original);
        }
    }

    private static ExecutionRunRecord HistoricalRun(AgentToolAdmissionJournalFixture fixture, int count) {
        var run = EffectsRun(fixture, count);
        var envelope = AgentToolProtocolEnvelope.Create("historical-fixture-owner", 1, "{\"original\":true}");
        var payload = new AgentToolPreparedPayload(AgentToolAdmissionJournalFixture.Payload().ToolName, 2,
            envelope.Digest, "{}", AgentToolProposalEffect.Mutation, AgentToolProposalRecovery.OwnerReceipt, envelope);
        var journal = run.ToolAdmission!;
        run = run with { ToolAdmission = journal with { Batches = journal.Batches.Select(batch => batch with {
            RequestDigest = payload.Digest,
            Proposals = batch.Proposals.Select(proposal => proposal with { Payload = payload, ApprovedDigest = payload.Digest }).ToImmutableArray()
        }).ToImmutableArray() } };
        run.ToolAdmission!.Validate();
        return run;
    }

    private static AgentHistoricalReceiptReviewRequest HistoricalRequest(ExecutionRunRecord run, AgentHistoricalReceiptCursor? cursor = null)
        => new(run.Id, run.ChatSessionId, run.AgentId, run.CreatedAtUtc, cursor);

    private static ValueTask<AgentHistoricalReceiptReadPolicy> HistoricalAllow(AgentToolSessionObservation original,
        AgentHistoricalReceiptReadFence current, CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        Assert.Equal(original.Session.Profile.ProfileId, current.Profile.ProfileId);
        Assert.Equal(original.Session.Profile.Fingerprint, current.Profile.Fingerprint);
        return ValueTask.FromResult(AgentHistoricalReceiptReadPolicy.Allowed);
    }

    private static void AssertHistoricalRefused(AgentHistoricalReceiptReview result, AgentHistoricalReceiptReviewStop stop) {
        Assert.Equal(stop, result.Stop);
        Assert.Empty(result.Observations);
        Assert.Null(result.RunRevision);
        Assert.Null(result.JournalRevision);
        Assert.Null(result.NextCursor);
    }

    private static string[] HistoricalFiles(AgentToolAdmissionJournalFixture fixture) => Directory.EnumerateFiles(fixture.WorkspaceRoot,
        "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal).Select(path =>
        Path.GetRelativePath(fixture.WorkspaceRoot, path) + ":" + Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)))).ToArray();

    private sealed class HistoricalJournal(AgentToolAdmissionJournalFixture fixture, AgentToolProfileBinding profile) {
        private readonly FileSandboxWorkspaceHistoricalReadStore store = new(fixture.WorkspaceRoot, fixture.StorageScope);

        internal Task<AgentHistoricalReceiptReview> ReadHistoricalReceiptReviewAsync(AgentHistoricalReceiptReviewRequest request,
            WorkspaceScopeDescriptor scope,
            Func<AgentToolSessionObservation, AgentHistoricalReceiptReadFence, CancellationToken, ValueTask<AgentHistoricalReceiptReadPolicy>> read,
            IReadOnlyList<IAgentToolHistoricalReceiptReadProvider> providers, CancellationToken cancellationToken = default) {
            var basis = new AgentHistoricalCatalogBasis(store, new(profile, scope), () => true);
            return AgentToolAdmissionJournal.ReadHistoricalReceiptReviewAsync(store, profile, request, scope, read, [], providers, basis, cancellationToken);
        }
    }

    private sealed class HistoricalReader(
        Func<AgentToolHistoricalReceiptReadClaim, CancellationToken, ValueTask<AgentToolHistoricalReceiptObservation>>? read = null)
        : IAgentToolHistoricalReceiptReadProvider {
        internal int Calls { get; private set; }
        public bool Supports(string toolName) => toolName == AgentToolAdmissionJournalFixture.Payload().ToolName;
        public async ValueTask<AgentToolHistoricalReceiptObservation> ReadAsync(AgentToolHistoricalReceiptReadClaim claim,
            CancellationToken cancellationToken = default) {
            Calls++;
            await claim.RequireAsync(cancellationToken);
            return read is null ? new(AgentHistoricalReceiptKnowledge.NotObserved) : await read(claim, cancellationToken);
        }
    }
}
