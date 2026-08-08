using System.Collections.Immutable;
using CanDoItAll.AgentFramework.Llm.Abstractions;
using CanDoItAll.AgentFramework.Llm.Conversations;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Tests.Unit;

/// <summary>
/// SB15: persistence behavior of the file-backed ordinary LLM conversation store — durable round trips
/// including usage and acceleration state, atomic optimistic-concurrency replacement, and typed failures
/// for duplicates, missing documents, and corrupted payloads.
/// </summary>
public sealed class FileLlmConversationStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "cdia-llm-conv-store-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public async Task Create_and_reload_round_trips_the_full_document_including_usage_and_acceleration_state()
    {
        var store = new FileLlmConversationStore(_root);
        var document = CreateDocument();

        await store.CreateAsync(document);
        var reloaded = await store.TryGetAsync(document.ConversationId);

        Assert.NotNull(reloaded);
        Assert.Equal(document.ConversationId, reloaded!.ConversationId);
        Assert.Equal(document.Title, reloaded.Title);
        Assert.Equal(document.Provider, reloaded.Provider);
        Assert.Equal(document.CreatedAtUtc, reloaded.CreatedAtUtc);
        Assert.Equal(document.UpdatedAtUtc, reloaded.UpdatedAtUtc);
        Assert.Equal(document.TranscriptRevision, reloaded.TranscriptRevision);
        Assert.Equal(document.AccelerationState, reloaded.AccelerationState);
        Assert.Equal(document.Entries.Length, reloaded.Entries.Length);
        for (var i = 0; i < document.Entries.Length; i++)
        {
            Assert.Equal(document.Entries[i], reloaded.Entries[i]);
        }

        Assert.Equal(new LlmUsage(7, 3, 1), reloaded.ComputeTotalUsage());
    }

    [Fact]
    public async Task CreateAsync_rejects_a_duplicate_conversation_id()
    {
        var store = new FileLlmConversationStore(_root);
        var document = CreateDocument();
        await store.CreateAsync(document);

        var exception = await Assert.ThrowsAsync<LlmConversationException>(() => store.CreateAsync(document));

        Assert.Equal(LlmConversationFailureKind.AlreadyExists, exception.Kind);
    }

    [Fact]
    public async Task ReplaceAsync_applies_only_the_expected_revision_and_fails_typed_on_mismatch()
    {
        var store = new FileLlmConversationStore(_root);
        var document = CreateDocument();
        await store.CreateAsync(document);

        var updated = document with { Title = "Renamed", TranscriptRevision = document.TranscriptRevision + 1 };
        await store.ReplaceAsync(updated, document.TranscriptRevision);

        var stale = await Assert.ThrowsAsync<LlmConversationException>(() => store.ReplaceAsync(
            updated with { TranscriptRevision = updated.TranscriptRevision + 1 },
            document.TranscriptRevision));
        Assert.Equal(LlmConversationFailureKind.ConcurrencyConflict, stale.Kind);

        var reloaded = await store.TryGetAsync(document.ConversationId);
        Assert.Equal("Renamed", reloaded!.Title);
        Assert.Equal(updated.TranscriptRevision, reloaded.TranscriptRevision);
    }

    [Fact]
    public async Task Concurrent_replacements_from_the_same_revision_admit_exactly_one_winner()
    {
        var store = new FileLlmConversationStore(_root);
        var document = CreateDocument();
        await store.CreateAsync(document);

        var attempts = Enumerable.Range(0, 8).Select(index => Task.Run(async () =>
        {
            try
            {
                await store.ReplaceAsync(
                    document with
                    {
                        Title = $"Winner {index}",
                        TranscriptRevision = document.TranscriptRevision + 1
                    },
                    document.TranscriptRevision);
                return true;
            }
            catch (LlmConversationException exception)
                when (exception.Kind == LlmConversationFailureKind.ConcurrencyConflict)
            {
                return false;
            }
        })).ToArray();

        var outcomes = await Task.WhenAll(attempts);

        Assert.Equal(1, outcomes.Count(won => won));
        var reloaded = await store.TryGetAsync(document.ConversationId);
        Assert.Equal(document.TranscriptRevision + 1, reloaded!.TranscriptRevision);
    }

    [Fact]
    public async Task ReplaceAsync_and_DeleteAsync_fail_typed_for_a_missing_conversation()
    {
        var store = new FileLlmConversationStore(_root);
        var document = CreateDocument();

        var replace = await Assert.ThrowsAsync<LlmConversationException>(
            () => store.ReplaceAsync(document, 0));
        Assert.Equal(LlmConversationFailureKind.NotFound, replace.Kind);

        var delete = await Assert.ThrowsAsync<LlmConversationException>(
            () => store.DeleteAsync(document.ConversationId));
        Assert.Equal(LlmConversationFailureKind.NotFound, delete.Kind);
    }

    [Fact]
    public async Task DeleteAsync_removes_the_document_durably()
    {
        var store = new FileLlmConversationStore(_root);
        var document = CreateDocument();
        await store.CreateAsync(document);

        await store.DeleteAsync(document.ConversationId);

        Assert.Null(await store.TryGetAsync(document.ConversationId));
        Assert.Empty(await store.ListAsync());
    }

    [Fact]
    public async Task ListAsync_returns_a_summary_per_persisted_conversation()
    {
        var store = new FileLlmConversationStore(_root);
        var first = CreateDocument();
        var second = CreateDocument(title: "Second conversation");
        await store.CreateAsync(first);
        await store.CreateAsync(second);

        var summaries = await store.ListAsync();

        Assert.Equal(2, summaries.Count);
        var firstSummary = Assert.Single(summaries, summary => summary.ConversationId == first.ConversationId);
        Assert.Equal(first.Title, firstSummary.Title);
        Assert.Equal(first.Entries.Length, firstSummary.EntryCount);
        Assert.Equal(first.Provider.ProviderName, firstSummary.ProviderName);
        Assert.Equal(first.Provider.Model, firstSummary.Model);
        Assert.False(firstSummary.HasActiveTurn);
    }

    [Fact]
    public async Task TryGetAsync_fails_typed_on_a_corrupted_payload_instead_of_returning_partial_state()
    {
        var store = new FileLlmConversationStore(_root);
        var document = CreateDocument();
        await store.CreateAsync(document);
        var path = Path.Combine(_root, "conversations", document.ConversationId.ToString("N") + ".json");
        await File.WriteAllTextAsync(path, "{ this is not a conversation document");

        var exception = await Assert.ThrowsAsync<LlmConversationException>(
            () => store.TryGetAsync(document.ConversationId));

        Assert.Equal(LlmConversationFailureKind.StorageCorrupted, exception.Kind);
    }

    private static LlmConversationDocument CreateDocument(string title = "Persisted conversation")
    {
        var turnId = Guid.NewGuid();
        var createdAt = new DateTimeOffset(2026, 8, 8, 9, 30, 0, TimeSpan.Zero);
        var entries = ImmutableArray.Create(
            new LlmConversationTranscriptEntry(
                Guid.NewGuid(), turnId, LlmMessageRole.System, "Be helpful.", createdAt),
            new LlmConversationTranscriptEntry(
                Guid.NewGuid(), turnId, LlmMessageRole.User, "Hello there", createdAt.AddSeconds(5)),
            new LlmConversationTranscriptEntry(
                Guid.NewGuid(), turnId, LlmMessageRole.Assistant, "Hi!", createdAt.AddSeconds(9),
                model: "gpt-conversation-test", usage: new LlmUsage(7, 3, 1)));
        return new LlmConversationDocument(
            Guid.NewGuid(),
            title,
            new LlmConversationProviderSnapshot(
                Guid.NewGuid(), "Conversation test provider", ProviderKind.OpenAi, "gpt-conversation-test"),
            createdAt,
            createdAt.AddSeconds(9),
            transcriptRevision: 3,
            entries,
            activeTurn: null,
            accelerationState: new LlmConversationAccelerationEnvelope(
                "provider-conversation-id", "Conversation test provider", "gpt-conversation-test", """{"id":"conv_1"}"""));
    }
}
