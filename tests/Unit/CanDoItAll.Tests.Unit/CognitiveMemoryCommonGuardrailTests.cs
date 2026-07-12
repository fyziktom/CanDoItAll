using CanDoItAll.Memory.SourceGateway;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.Modules.CognitiveMemory;
using CanDoItAll.Tests.Support.CognitiveMemory;

namespace CanDoItAll.Tests.Unit;

public sealed class CognitiveMemoryCommonGuardrailTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(CognitiveMemoryPageRequest.MaxTake + 1)]
    public void PageRequest_RejectsInvalidTakeInsteadOfSilentlyClamping(int take)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new CognitiveMemoryPageRequest(take: take));
    }

    [Fact]
    public void BudgetTracker_ReportsTheExactLimitThatStopsProcessing()
    {
        var budget = new CognitiveMemoryProcessingBudget(maxItemCount: 2, maxByteCount: 10, timeout: TimeSpan.FromSeconds(5));
        var tracker = new CognitiveMemoryBudgetTracker(budget, DateTimeOffset.UnixEpoch);

        Assert.True(tracker.TryAccept(5, DateTimeOffset.UnixEpoch).Accepted);
        Assert.True(tracker.TryAccept(5, DateTimeOffset.UnixEpoch).Accepted);
        var decision = tracker.TryAccept(1, DateTimeOffset.UnixEpoch);

        Assert.False(decision.Accepted);
        Assert.Equal(CognitiveMemoryBudgetLimit.ItemCount, decision.Limit);
        Assert.Equal(2, decision.AcceptedItemCount);
        Assert.Equal(10, decision.AcceptedByteCount);
    }

    [Fact]
    public async Task FakeEmbeddingProvider_ReturnsDeterministicVectors()
    {
        var provider = new FakeCognitiveMemoryEmbeddingProvider(dimensions: 6);
        var request = new CognitiveMemoryEmbeddingRequest(
            new CognitiveMemoryEmbeddingProfileId("fake-embedding-v1"),
            "Docker production context",
            new CognitiveMemoryProcessingBudget(1, 1024, TimeSpan.FromSeconds(1)));

        var first = await provider.EmbedAsync(request);
        var second = await provider.EmbedAsync(request);

        Assert.Equal(first.InputHash, second.InputHash);
        Assert.Equal(first.Vector.ToArrayForAdapterBoundary(), second.Vector.ToArrayForAdapterBoundary());
        Assert.Equal(6, first.Vector.Length);
    }

    [Fact]
    public async Task FakeVectorStore_RequiresRegisteredProfileAndReturnsBoundedOrderedHits()
    {
        var profileId = new CognitiveMemoryProjectionProfileId("fake-projection-v1");
        var store = new FakeCognitiveMemoryVectorStore();
        var closer = CreateHit(0.1f);
        var farther = CreateHit(0.9f);
        store.SetHits(profileId, [farther, closer]);

        var result = await store.SearchAsync(new CognitiveMemoryVectorSearchRequest(
            profileId,
            new CognitiveMemoryVector(new[] { 1f, 0f }),
            new CognitiveMemoryPageRequest(take: 1),
            CognitiveMemoryFakePolicyContexts.Project(Guid.NewGuid())));

        Assert.Single(result.Hits);
        Assert.Equal(closer.RecordId, result.Hits[0].RecordId);
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await store.SearchAsync(new CognitiveMemoryVectorSearchRequest(
                new CognitiveMemoryProjectionProfileId("missing-profile"),
                new CognitiveMemoryVector(new[] { 1f, 0f }),
                new CognitiveMemoryPageRequest(take: 1),
                CognitiveMemoryFakePolicyContexts.Project(Guid.NewGuid()))));
    }

    [Fact]
    public void DurablePayloadEnvelope_UsesCachedSourceGeneratedContext()
    {
        var envelope = new CognitiveMemoryDurablePayloadEnvelope(
            new CognitiveMemoryPayloadSchemaVersion("trace-v1"),
            CognitiveMemoryDurablePayloadKind.RecallTrace,
            "{\"trace\":\"ok\"}",
            new Dictionary<string, string>
            {
                ["algorithmVersion"] = "test-v1"
            });

        var json = CognitiveMemoryJson.SerializeEnvelope(envelope);
        var roundTrip = CognitiveMemoryJson.DeserializeEnvelope(json);

        Assert.Equal(envelope.SchemaVersion, roundTrip.SchemaVersion);
        Assert.Equal(envelope.PayloadKind, roundTrip.PayloadKind);
        Assert.Equal("test-v1", roundTrip.Metadata["algorithmVersion"]);
    }

    [Fact]
    public async Task FakeProjectStructureSourceSnapshotProvider_UsesDeterministicPagingAndCursors()
    {
        var projectId = Guid.NewGuid();
        var provider = new FakeProjectStructureSourceSnapshotProvider();
        provider.SetItems(projectId, [
            FakeProjectStructureSourceSnapshotProvider.CreateNode(projectId, "node-b", "B", "second"),
            FakeProjectStructureSourceSnapshotProvider.CreateNode(projectId, "node-a", "A", "first")
        ]);

        var first = await provider.ReadSnapshotAsync(new ProjectStructureSourceSnapshotRequest(projectId, Take: 1));
        var second = await provider.ReadSnapshotAsync(new ProjectStructureSourceSnapshotRequest(projectId, first.Manifest.NextCursor, Take: 1));

        Assert.True(first.Manifest.HasMore);
        Assert.False(second.Manifest.HasMore);
        Assert.NotEqual(first.Items[0].Id, second.Items[0].Id);
        Assert.Equal(MemorySourceSnapshotPageStatus.EndOfSource, second.Manifest.PageStatus);
    }

    [Fact]
    public void CognitiveMemoryContracts_DoNotUseStringlyTypedStateProperties()
    {
        var stateNameParts = new[]
        {
            "Mode",
            "Status",
            "State",
            "Kind",
            "Level",
            "Risk",
            "Role",
            "Origin"
        };

        var offenders = typeof(CognitiveMemoryRecord).Assembly.GetTypes()
            .Where(type => string.Equals(type.Namespace, typeof(CognitiveMemoryRecord).Namespace, StringComparison.Ordinal))
            .SelectMany(type => type.GetProperties().Select(property => new { Type = type, Property = property }))
            .Where(item => item.Property.PropertyType == typeof(string) &&
                           !item.Property.Name.EndsWith("Message", StringComparison.OrdinalIgnoreCase) &&
                           stateNameParts.Any(part => item.Property.Name.Contains(part, StringComparison.OrdinalIgnoreCase)))
            .Select(item => $"{item.Type.Name}.{item.Property.Name}")
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        Assert.Empty(offenders);
    }

    private static CognitiveMemoryVectorSearchHit CreateHit(float distance)
        => new(
            CognitiveMemoryRecordId.New(),
            CognitiveMemoryHash.FromUtf8(distance.ToString("R")),
            distance);
}
