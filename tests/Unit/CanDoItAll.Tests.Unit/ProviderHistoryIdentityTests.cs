using CanDoItAll.AgentFramework.ProviderHistory;

namespace CanDoItAll.Tests.Unit;

public sealed class ProviderHistoryIdentityTests {
    [Fact]
    public void Durable_json_roundtrip_preserves_exact_model_identity() {
        var model = new ProviderModelIdentity("Exact-Model");
        var json = System.Text.Json.JsonSerializer.Serialize(model);
        var restored = System.Text.Json.JsonSerializer.Deserialize<ProviderModelIdentity>(json);
        Assert.Equal(model, restored);
    }

    [Fact]
    public async Task Canonical_snapshot_preserves_attempt_identity_and_excludes_payloads() {
        var recorder = new RecordingProviderHistory();
        var context = HistoryInvocationContext.Create(currentTurn: new("private prompt", 0));
        var first = await recorder.BeginAsync(new(new(new(Guid.NewGuid()), "provider", "OpenAi", new("model"), new("model")),
            HistoryOperation.CompleteChat, context), default);
        var offset = context.Attempts.Count;
        var second = await recorder.BeginAsync(new(first.Provider, first.Operation, context), default);
        var completion = new HistoryAttemptCompletion(HistoryOutcome.Succeeded, second.StartedAtUtc.AddSeconds(1),
            new(HistoryUsageState.Complete, 7, 3), new(HistoryPriceState.ProviderReported, 0.2m, "USD"));
        context.Attempts.Complete(second, completion);
        var snapshot = Assert.Single(context.Attempts.EvidenceSnapshot(offset));
        Assert.Equal(second.AttemptId, snapshot.AttemptId);
        Assert.Equal(completion.Usage, snapshot.Usage);
        var json = System.Text.Json.JsonSerializer.Serialize(snapshot);
        Assert.DoesNotContain("private prompt", json, StringComparison.Ordinal);
        Assert.DoesNotContain("Policy", json, StringComparison.Ordinal);
        Assert.Throws<ProviderHistoryException>(() => new HistoryAttemptCollection().Complete(first, completion));
    }

    [Fact]
    public void Canonical_identity_uses_exact_owner_and_evidence_boundaries() {
        var first = new HistoryOwnerIdentity(HistorySourceKind.SimpleChat, new("a:b"), new("c"));
        var second = first with { OwnerId = new("a"), EvidenceId = new("b:c") };
        Assert.NotEqual(HistoryEntryId.ForCanonical(first), HistoryEntryId.ForCanonical(second));
        Assert.Equal(HistoryEntryId.ForCanonical(first), HistoryEntryId.ForCanonical(first with { }));
    }

    [Fact]
    public void Source_version_update_preserves_entry_identity() {
        var partition = new HistoryPartition(Guid.NewGuid(), Guid.NewGuid(), "local");
        var source = new CanonicalEvidenceReference(partition, HistorySourceKind.SimpleChat,
            new("conversation/operation"), new("invocation/1"));
        var original = new HistoryOwnerLink(HistoryEntryId.New(), source, new(1),
            HistoryOwnerRole.ContentOwner, HistoryOwnerState.Linked);
        var updated = original with { Version = new(2) };
        Assert.Equal(original.EntryId, updated.EntryId);
        Assert.Equal(original.Source, updated.Source);
        Assert.NotEqual(original.Version, updated.Version);
    }

    [Fact]
    public void Same_correlation_does_not_merge_attempts() {
        var request = ProviderRequestId.New();
        var first = (Request: request, Attempt: ProviderAttemptId.New(), Correlation: "batch-item");
        var retry = (Request: request, Attempt: ProviderAttemptId.New(), Correlation: "batch-item");
        Assert.Equal(first.Request, retry.Request);
        Assert.Equal(first.Correlation, retry.Correlation);
        Assert.NotEqual(first.Attempt, retry.Attempt);
    }

    [Fact]
    public void Model_identity_is_ordinal_and_does_not_normalize_external_ids() {
        Assert.NotEqual(new ProviderModelIdentity("Model-A"), new ProviderModelIdentity("model-a"));
        Assert.Equal(" model ", new ProviderModelIdentity(" model ").Value);
        Assert.Throws<ArgumentException>(() => new ProviderModelIdentity(" "));
    }

    [Fact]
    public void Profile_generation_is_not_part_of_persistent_source_identity() {
        var partition = new HistoryPartition(Guid.NewGuid(), Guid.NewGuid(), "local");
        var first = new HistoryAccessContext(partition, new(1, 1), new(HistoryAuthenticationKind.TrustedLocalOperator), null);
        var resumed = first with { Fence = new(3, 4) };
        Assert.Equal(first.Partition, resumed.Partition);
        Assert.NotEqual(first.Fence, resumed.Fence);
    }

    [Fact]
    public void Old_canonical_intervals_are_searchable_without_widening_query_limit() {
        var from = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);
        HistoryContractValidation.Validate(new ProviderRequestHistoryQuery(new HistoryProviderScope.AllAuthorized(), from, from.AddDays(31)));
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(201, 1)]
    [InlineData(50, 0)]
    [InlineData(50, 32)]
    public void Query_rejects_unbounded_or_empty_pages_and_ranges(int pageSize, int days) {
        var from = DateTimeOffset.UtcNow;
        var query = new ProviderRequestHistoryQuery(new HistoryProviderScope.AllAuthorized(), from, from.AddDays(days)) { PageSize = pageSize };
        var error = Assert.Throws<ProviderHistoryException>(() => HistoryContractValidation.Validate(query));
        Assert.Equal(HistoryFailure.InvalidQuery, error.Failure);
    }

    [Fact]
    public void Policy_defaults_are_light_and_bounded() {
        var policy = new HistoryPolicy();
        HistoryContractValidation.Validate(policy);
        Assert.Equal(HistoryCaptureMode.Light, policy.CaptureMode);
        Assert.Equal(30, policy.MetadataRetentionDays);
        Assert.Equal(7, policy.DetailRetentionDays);
    }

    [Theory]
    [InlineData(0, 7, 32768, 500)]
    [InlineData(30, 31, 32768, 500)]
    [InlineData(30, 7, 131073, 500)]
    [InlineData(30, 7, 32768, 1001)]
    public void Invalid_policy_is_rejected_not_clamped(int metadataDays, int detailDays, int bytes, int batch) {
        var policy = new HistoryPolicy { MetadataRetentionDays = metadataDays, DetailRetentionDays = detailDays,
            MaximumTextBytes = bytes, BatchSize = batch };
        Assert.Throws<ArgumentException>(() => HistoryContractValidation.Validate(policy));
    }
}
