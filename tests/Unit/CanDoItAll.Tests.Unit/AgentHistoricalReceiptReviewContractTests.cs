using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Tests.Unit;

public sealed class AgentHistoricalReceiptReviewContractTests {
    private const string PrivateArguments = "{\"private\":\"original-provider-input\"}";
    private const string PrivateFingerprint = "postgres:internal-ledger.example.invalid:5544:customer_accounts:receipt_operator";
    private static readonly DateTimeOffset Created = new(2026, 10, 16, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Historical_review_captures_bounded_ephemeral_observations_without_rebinding_original_admission() {
        var admission = Admission();
        var original = admission.OriginalSession;
        var observations = new[] { new AgentHistoricalReceiptObservation(admission.Binding, AgentHistoricalReceiptKnowledge.Accepted) };
        var cursor = new AgentHistoricalReceiptCursor(admission.Request.ExecutionRunId, 5, 7, 4, admission.CurrentFence.Context);
        var review = new AgentHistoricalReceiptReview(admission.Request, admission.CurrentFence.Context, 5, 7,
            AgentHistoricalReceiptReviewStop.LookupLimit, observations, cursor);
        observations[0] = new(admission.Binding, AgentHistoricalReceiptKnowledge.NotObserved);

        Assert.Equal(AgentHistoricalReceiptKnowledge.Accepted, Assert.Single(review.Observations).Knowledge);
        Assert.Equal(new DatabaseProfileGeneration(8), original.Profile.Generation);
        Assert.Equal(new DatabaseProfileGeneration(0), review.CurrentContext.Generation);
        Assert.Same(original, admission.OriginalSession);
        Assert.Equal(PrivateArguments, admission.Payload.ArgumentsJson);
        var serialized = JsonSerializer.Serialize(review, JsonSerializerOptions.Web);
        Assert.DoesNotContain("original-provider-input", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("internal-ledger.example.invalid", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("customer_accounts", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("receipt_operator", serialized, StringComparison.Ordinal);
        Assert.Equal(PrivateFingerprint, admission.CurrentFence.Profile.Fingerprint);
        var decoded = JsonSerializer.Deserialize<AgentHistoricalReceiptReview>(serialized, JsonSerializerOptions.Web);
        Assert.NotNull(decoded);
        Assert.Equal(review.Request, decoded.Request);
        Assert.Equal(review.CurrentContext, decoded.CurrentContext);
        Assert.Equal(review.RunRevision, decoded.RunRevision);
        Assert.Equal(review.JournalRevision, decoded.JournalRevision);
        Assert.Equal(review.Observations, decoded.Observations);
        Assert.Equal(cursor, decoded.NextCursor);
        using var serializedDocument = JsonDocument.Parse(serialized);
        Assert.Equal("5", serializedDocument.RootElement.GetProperty("runRevision").GetString());
        foreach (var revision in new[] { 9007199254740993L, long.MaxValue }) {
            var precise = new AgentHistoricalReceiptCursor(admission.Request.ExecutionRunId, revision, revision,
                AgentHistoricalReceiptCursor.MaximumProposalOrdinal, admission.CurrentFence.Context);
            var wire = JsonSerializer.Serialize(precise, JsonSerializerOptions.Web);
            Assert.Equal(precise, JsonSerializer.Deserialize<AgentHistoricalReceiptCursor>(wire, JsonSerializerOptions.Web));
            foreach (var invalid in new[] { "0", "01", "+1", "-1", " 1", "1 ", "1.0", "1e1", "9223372036854775808", "1\n" }) {
                var changed = System.Text.Json.Nodes.JsonNode.Parse(wire)!;
                changed["runRevision"] = invalid;
                Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<AgentHistoricalReceiptCursor>(changed.ToJsonString(), JsonSerializerOptions.Web));
            }
            var numeric = System.Text.Json.Nodes.JsonNode.Parse(wire)!;
            numeric["journalRevision"] = revision;
            Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<AgentHistoricalReceiptCursor>(numeric.ToJsonString(), JsonSerializerOptions.Web));
        }
        Assert.Throws<ArgumentOutOfRangeException>(() => new AgentHistoricalReceiptCursor(admission.Request.ExecutionRunId, 1, 1, -1, admission.CurrentFence.Context));
        Assert.Throws<ArgumentOutOfRangeException>(() => new AgentHistoricalReceiptCursor(admission.Request.ExecutionRunId, 1, 1,
            AgentHistoricalReceiptCursor.MaximumProposalOrdinal + 1, admission.CurrentFence.Context));
        Assert.Throws<ArgumentException>(() => new AgentHistoricalReceiptReview(admission.Request, admission.CurrentFence.Context, 5, 7,
            AgentHistoricalReceiptReviewStop.ExhaustedSnapshot, observations, cursor));
        Assert.Null(new AgentHistoricalReceiptReview(admission.Request, admission.CurrentFence.Context, 5, 7,
            AgentHistoricalReceiptReviewStop.ExhaustedSnapshot, []).NextCursor);
        var unchangedPosition = new AgentHistoricalReceiptCursor(admission.Request.ExecutionRunId, 5, 7, 0, admission.CurrentFence.Context);
        Assert.Throws<ArgumentException>(() => new AgentHistoricalReceiptReview(admission.Request, admission.CurrentFence.Context, 5, 7,
            AgentHistoricalReceiptReviewStop.LookupLimit, [], unchangedPosition));
        Assert.Equal(unchangedPosition, new AgentHistoricalReceiptReview(admission.Request, admission.CurrentFence.Context, 5, 7,
            AgentHistoricalReceiptReviewStop.CooperativeBudget, [], unchangedPosition).NextCursor);
        Assert.Equal(Created, review.Request.ExecutionCreatedAtUtc);
        Assert.Throws<ArgumentException>(() => new AgentHistoricalReceiptReview(admission.Request, admission.CurrentFence.Context, 5, 7,
            AgentHistoricalReceiptReviewStop.LookupLimit, [review.Observations[0], review.Observations[0]], cursor));
        Assert.Throws<ArgumentException>(() => new AgentHistoricalReceiptReview(admission.Request, admission.CurrentFence.Context, 5, 7,
            AgentHistoricalReceiptReviewStop.LookupLimit, new AgentHistoricalReceiptObservation[5], cursor));
        foreach (var stop in new[] { AgentHistoricalReceiptReviewStop.SnapshotChanged, AgentHistoricalReceiptReviewStop.ReadDenied,
                AgentHistoricalReceiptReviewStop.ContextChanged, AgentHistoricalReceiptReviewStop.Unsupported }) {
            Assert.Throws<ArgumentException>(() => new AgentHistoricalReceiptReview(admission.Request, admission.CurrentFence.Context, 5, 7,
                stop, review.Observations));
            var refused = new AgentHistoricalReceiptReview(admission.Request, admission.CurrentFence.Context, null, null, stop, []);
            Assert.Empty(refused.Observations);
            Assert.Null(refused.RunRevision);
        }
        Assert.Throws<ArgumentException>(() => new AgentToolHistoricalReceiptReadAdmission(admission.Request,
            original with { Profile = new(Guid.NewGuid(), original.Profile.Fingerprint, new(0)) }, admission.Binding,
            admission.Payload, admission.CurrentFence, 5, 7));
        var differentPayloadDigest = AgentToolProtocolEnvelope.ComputeDigest("different saved payload");
        Assert.NotEqual(admission.Payload.Digest, differentPayloadDigest);
        Assert.Throws<ArgumentException>(() => new AgentToolHistoricalReceiptReadAdmission(admission.Request, original,
            admission.Binding with { Digest = differentPayloadDigest },
            admission.Payload, admission.CurrentFence, 5, 7));
        var legacy = new AgentToolPreparedPayload(admission.Payload.ToolName, 1, admission.Payload.Digest,
            PrivateArguments, AgentToolProposalEffect.Mutation, AgentToolProposalRecovery.OwnerReceipt);
        Assert.Throws<ArgumentException>(() => new AgentToolHistoricalReceiptReadAdmission(admission.Request, original,
            admission.Binding with { SemanticVersion = 1 }, legacy, admission.CurrentFence, 5, 7));
        Assert.Throws<ArgumentException>(() => new AgentHistoricalReceiptReadFence(
            new(original.Profile.ProfileId, new string('x', AgentHistoricalReceiptReview.MaximumUtf8Bytes + 1), new(0)),
            WorkspaceScopeDescriptor.Sandbox));
    }

    [Fact]
    public async Task Historical_claim_validates_before_owner_read_and_retires_after_failure_or_drained_cancellation() {
        var admission = Admission();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var validations = 0;
        var claim = new AgentToolHistoricalReceiptReadClaim(admission, async (captured, cancellationToken) => {
            Assert.Same(admission, captured);
            if (Interlocked.Increment(ref validations) == 1) {
                entered.SetResult();
                await release.Task.WaitAsync(cancellationToken);
            }
        });
        var provider = new HistoricalProvider(async (current, cancellationToken) => {
            Assert.Same(admission, await current.RequireAsync(cancellationToken));
            return new(AgentHistoricalReceiptKnowledge.NotObserved);
        });
        await Assert.ThrowsAsync<InvalidOperationException>(() => claim.RequireAsync().AsTask());
        var read = claim.ReadAsync(provider).AsTask();
        try {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(0, provider.Calls);
        } finally {
            release.TrySetResult();
            await read.WaitAsync(TimeSpan.FromSeconds(5));
        }
        Assert.Equal(AgentHistoricalReceiptKnowledge.NotObserved, (await read.WaitAsync(TimeSpan.FromSeconds(5))).Knowledge);
        Assert.Equal(1, provider.Calls);
        Assert.Equal(3, validations);
        await Assert.ThrowsAsync<InvalidOperationException>(() => claim.RequireAsync().AsTask());
        await Assert.ThrowsAsync<InvalidOperationException>(() => claim.ReadAsync(provider).AsTask());

        var deniedProvider = new HistoricalProvider((_, _) => throw new InvalidOperationException("A denied read reached the owner."));
        var denied = new AgentToolHistoricalReceiptReadClaim(admission, (_, _) => throw new AgentToolReceiptAccessDeniedException());
        await Assert.ThrowsAsync<AgentToolReceiptAccessDeniedException>(() => denied.ReadAsync(deniedProvider).AsTask());
        Assert.Equal(0, deniedProvider.Calls);
        await Assert.ThrowsAsync<InvalidOperationException>(() => denied.RequireAsync().AsTask());

        var unsupportedProvider = new HistoricalProvider((_, _) => throw new InvalidOperationException("Unsupported owners cannot be invoked."), supports: false);
        var unsupported = new AgentToolHistoricalReceiptReadClaim(admission, (_, _) => ValueTask.CompletedTask);
        Assert.Equal(AgentHistoricalReceiptKnowledge.Unsupported, (await unsupported.ReadAsync(unsupportedProvider)).Knowledge);
        Assert.Equal(0, unsupportedProvider.Calls);
        await Assert.ThrowsAsync<InvalidOperationException>(() => unsupported.RequireAsync().AsTask());

        using var cancellation = new CancellationTokenSource();
        var ownerEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var ownerRelease = new TaskCompletionSource<AgentToolHistoricalReceiptObservation>(TaskCreationOptions.RunContinuationsAsynchronously);
        var pending = new AgentToolHistoricalReceiptReadClaim(admission, (_, token) => {
            token.ThrowIfCancellationRequested();
            return ValueTask.CompletedTask;
        });
        var pendingProvider = new HistoricalProvider(async (current, token) => {
            Assert.Same(admission, await current.RequireAsync(token));
            ownerEntered.SetResult();
            return await ownerRelease.Task;
        });
        var pendingRead = pending.ReadAsync(pendingProvider, cancellation.Token).AsTask();
        try {
            await ownerEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            cancellation.Cancel();
            Assert.False(pendingRead.IsCompleted);
        } finally {
            ownerRelease.TrySetResult(new(AgentHistoricalReceiptKnowledge.ClosedAbsent));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pendingRead.WaitAsync(TimeSpan.FromSeconds(5)));
        }
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pendingRead.WaitAsync(TimeSpan.FromSeconds(5)));
        await Assert.ThrowsAsync<InvalidOperationException>(() => pending.RequireAsync().AsTask());
        Assert.Equal(1, pendingProvider.Calls);

        var staleEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var staleRelease = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var stale = new AgentToolHistoricalReceiptReadClaim(admission, async (_, _) => {
            staleEntered.SetResult();
            await staleRelease.Task;
        });
        var staleRead = stale.ReadAsync(deniedProvider).AsTask();
        try {
            await staleEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            stale.Retire();
        } finally {
            staleRelease.TrySetResult();
            await Assert.ThrowsAsync<InvalidOperationException>(() => staleRead.WaitAsync(TimeSpan.FromSeconds(5)));
        }
        await Assert.ThrowsAsync<InvalidOperationException>(() => staleRead.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Equal(0, deniedProvider.Calls);
        await AssertCatalogBasisLifetimeAsync(admission.CurrentFence);
    }

    private static async Task AssertCatalogBasisLifetimeAsync(AgentHistoricalReceiptReadFence fence) {
        var tags = new List<string> { "original" };
        var secrets = new List<AgentAllowedSecretReference> { new(Guid.NewGuid(), "reference", "purpose") };
        var agent = new AgentDefinition(Guid.NewGuid(), "Historical agent", "Reader", "", "", AgentLifecycleStatus.Active,
            null, "model", AgentWorkloadKind.General, AgentChatHistoryMode.FrameworkManaged, 0, false, false, "{}", false,
            "historical-agent", AgentPermissionsPolicy.Default with { AllowedSecrets = secrets }, [], tags, Created, Created);
        var source = agent with { Id = Guid.NewGuid(), TemplateKey = "historical-source" };
        var catalog = new BasisCatalog(fence.Scope, agent, source);
        var basis = new AgentHistoricalCatalogBasis(catalog, fence, () => true);
        await basis.CaptureExecutorAsync(agent.Id, default);
        Assert.Same(basis.RequireExecutor(agent.Id), await basis.GetOriginalProcessSourceAsync(agent.Id));
        await basis.GetOriginalProcessSourceAsync(source.Id);
        tags[0] = "mutated";
        secrets.Clear();
        basis.Seal(default);
        Assert.Equal("original", Assert.Single(basis.RequireSelected(agent.Id).Tags));
        Assert.Single(basis.RequireSelected(agent.Id).Permissions.NormalizedAllowedSecrets);
        Assert.Equal(2, catalog.Reads);
        await Assert.ThrowsAsync<AgentToolReceiptAccessDeniedException>(() => basis.GetOriginalProcessSourceAsync(Guid.NewGuid()));
        Assert.Equal(2, catalog.Reads);
        await basis.ObserveFinalAsync(default);
        Assert.Equal(3, catalog.Reads);
        catalog.Digest = AgentToolProtocolEnvelope.ComputeDigest("same revision, different catalog bytes");
        await Assert.ThrowsAsync<AgentHistoricalCatalogChangedException>(() => basis.ObserveFinalAsync(default));
        basis.Retire();
        Assert.Throws<InvalidOperationException>(() => basis.RequireSelected(agent.Id));

        var other = new AgentHistoricalCatalogBasis(catalog, fence, () => true);
        await other.CaptureExecutorAsync(source.Id, default);
        other.Seal(default);
        Assert.Equal(source.Id, other.RequireSelected(source.Id).Id);
        Assert.Throws<AgentToolReceiptAccessDeniedException>(() => other.RequireSelected(agent.Id));
        other.Retire();

        using var cancellation = new CancellationTokenSource();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        catalog.BeforeRead = async _ => {
            entered.TrySetResult();
            await release.Task;
        };
        var pending = new AgentHistoricalCatalogBasis(catalog, fence, () => true);
        var capture = pending.CaptureExecutorAsync(agent.Id, cancellation.Token);
        try {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            cancellation.Cancel();
            Assert.False(capture.IsCompleted);
        } finally {
            release.TrySetResult();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => capture.WaitAsync(TimeSpan.FromSeconds(5)));
            pending.Retire();
        }
        Assert.Throws<InvalidOperationException>(() => pending.RequireCurrent());
    }

    private sealed class BasisCatalog(WorkspaceScopeDescriptor scope, params AgentDefinition[] agents) : IAgentHistoricalCatalogReadStore {
        internal int Reads { get; private set; }
        internal AgentToolSemanticDigest Digest { get; set; } = AgentToolProtocolEnvelope.ComputeDigest("original catalog");
        internal Func<CancellationToken, Task>? BeforeRead { get; set; }

        public async Task<AgentHistoricalAgentRead> ReadHistoricalAgentAsync(Guid agentId, CancellationToken cancellationToken = default) {
            Reads++;
            if (BeforeRead is not null) {
                await BeforeRead(cancellationToken);
            }
            return new(AgentHistoricalStorageReadStatus.Ready, scope, CatalogDataRevision.Initial,
                agents.Single(agent => agent.Id == agentId), Digest);
        }
    }

    private static AgentToolHistoricalReceiptReadAdmission Admission() {
        var original = new AgentToolSessionAdmission(new(Guid.NewGuid(), Guid.NewGuid(), AgentExecutionAuthorityId.Create()),
            Guid.NewGuid(), AgentRuntimeContextPurpose.InteractiveChat, new(Guid.NewGuid(), PrivateFingerprint, new(8)));
        var request = new AgentHistoricalReceiptReviewRequest(original.Reference.ExecutionRunId, original.Reference.ChatSessionId,
            original.AgentId, Created);
        var source = AgentToolProtocolEnvelope.Create("historical-test-owner", 1, "{}");
        var payload = new AgentToolPreparedPayload("historical-test-tool", 2, source.Digest, PrivateArguments,
            AgentToolProposalEffect.Mutation, AgentToolProposalRecovery.OwnerReceipt, source);
        return new(request, original, new(new(Guid.NewGuid()), new(Guid.NewGuid()), 2, payload.Digest), payload,
            new(new(original.Profile.ProfileId, original.Profile.Fingerprint, new(0)), WorkspaceScopeDescriptor.Project(Guid.NewGuid().ToString("D"))), 5, 7);
    }

    private sealed class HistoricalProvider(
        Func<AgentToolHistoricalReceiptReadClaim, CancellationToken, ValueTask<AgentToolHistoricalReceiptObservation>> read,
        bool supports = true)
        : IAgentToolHistoricalReceiptReadProvider {
        public int Calls { get; private set; }
        public bool Supports(string toolName) => supports;
        public ValueTask<AgentToolHistoricalReceiptObservation> ReadAsync(AgentToolHistoricalReceiptReadClaim claim,
            CancellationToken cancellationToken = default) {
            Calls++;
            return read(claim, cancellationToken);
        }
    }
}
