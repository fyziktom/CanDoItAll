using System.Text.Json;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class FileSandboxWorkspaceSnapshotCacheTests {
    [Fact]
    public void CachedSnapshot_DoesNotRetainMutableCallerCollections() {
        List<ChatMessageRecord> messages = [new(Guid.NewGuid(), ChatMessageRole.User, "Original", DateTimeOffset.UtcNow, 1)];
        List<PendingToolApprovalRecord> approvals = [new("approval", "call", "tool", "native", "Approve", "{}")];
        var original = CreateChat("Original") with {
            Messages = messages, Compatibility = new("session", "{}", approvals)
        };
        var bytes = JsonSerializer.SerializeToUtf8Bytes(original);
        var cache = new FileSandboxWorkspaceSnapshotCache();
        cache.Remember("session", bytes, original);

        messages.Clear();
        approvals.Clear();

        Assert.True(cache.TryRead<ChatSessionRecord>("session", bytes, out var cached));
        Assert.Equal("Original", Assert.Single(cached!.Messages).Content);
        Assert.Equal("approval", Assert.Single(cached.Compatibility!.PendingApprovals).ApprovalId);
        Assert.Throws<NotSupportedException>(() => ((IList<ChatMessageRecord>)cached.Messages).Clear());
    }

    [Fact]
    public void CachedSnapshot_RequiresExactContentAndOwnerPath() {
        var original = CreateChat("Original");
        var bytes = JsonSerializer.SerializeToUtf8Bytes(original);
        var cache = new FileSandboxWorkspaceSnapshotCache();
        cache.Remember("scope-one/session", bytes, original);

        Assert.False(cache.TryRead<ChatSessionRecord>("scope-two/session", bytes, out _));
        var changed = JsonSerializer.SerializeToUtf8Bytes(original with { Title = "Changed" });
        Assert.False(cache.TryRead<ChatSessionRecord>("scope-one/session", changed, out _));
        Assert.Equal(0, cache.Count);
        Assert.Equal(0, cache.EstimatedRetainedBytes);
    }

    [Theory]
    [InlineData(2, 65536)]
    [InlineData(16, 12000)]
    public void Eviction_RespectsEntryAndByteBudgets(int entries, long bytes) {
        var cache = new FileSandboxWorkspaceSnapshotCache(entries, bytes);
        var first = CreateChat("First");
        var firstBytes = JsonSerializer.SerializeToUtf8Bytes(first);
        cache.Remember("first", firstBytes, first);
        for (var index = 0; index < 4; index++) {
            var chat = CreateChat($"Chat {index}");
            var payload = JsonSerializer.SerializeToUtf8Bytes(chat);
            cache.Remember($"session-{index}", payload, chat);
            Assert.InRange(cache.Count, 1, entries);
            Assert.InRange(cache.EstimatedRetainedBytes, 1, bytes);
        }

        Assert.False(cache.TryRead<ChatSessionRecord>("first", firstBytes, out _));
    }

    [Fact]
    public void OversizedSnapshot_IsReturnedWithoutRetention() {
        var cache = new FileSandboxWorkspaceSnapshotCache(maximumEstimatedBytes: 1);
        var original = CreateChat("Larger than budget");
        var bytes = JsonSerializer.SerializeToUtf8Bytes(original);

        var returned = cache.Remember("session", bytes, original);

        Assert.Equal(original.Title, returned.Title);
        Assert.Equal(0, cache.Count);
        Assert.False(cache.TryRead<ChatSessionRecord>("session", bytes, out _));
    }

    private static ChatSessionRecord CreateChat(string title) =>
        new(Guid.NewGuid(), Guid.NewGuid(), title, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, []);
}
