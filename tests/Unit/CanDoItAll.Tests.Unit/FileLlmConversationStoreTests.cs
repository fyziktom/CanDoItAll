using System.Collections.Immutable;
using System.Text.Json.Nodes;
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
    public async Task Concurrent_replacements_across_independent_store_instances_admit_exactly_one_winner()
    {
        var initialStore = new FileLlmConversationStore(_root);
        var document = CreateDocument();
        await initialStore.CreateAsync(document);
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var attempts = Enumerable.Range(0, 16).Select(index => Task.Run(async () =>
        {
            var store = new FileLlmConversationStore(_root);
            await start.Task;
            try
            {
                await store.ReplaceAsync(
                    document with
                    {
                        Title = $"Cross-instance winner {index}",
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

        start.SetResult();
        var outcomes = await Task.WhenAll(attempts);

        Assert.Equal(1, outcomes.Count(won => won));
        var reloaded = await initialStore.TryGetAsync(document.ConversationId);
        Assert.Equal(document.TranscriptRevision + 1, reloaded!.TranscriptRevision);
    }

    [Fact]
    public async Task Concurrent_creates_across_independent_store_instances_admit_exactly_one_creator()
    {
        var document = CreateDocument();
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var attempts = Enumerable.Range(0, 16).Select(_ => Task.Run(async () =>
        {
            var store = new FileLlmConversationStore(_root);
            await start.Task;
            try
            {
                await store.CreateAsync(document);
                return true;
            }
            catch (LlmConversationException exception)
                when (exception.Kind == LlmConversationFailureKind.AlreadyExists)
            {
                return false;
            }
        })).ToArray();

        start.SetResult();
        var outcomes = await Task.WhenAll(attempts);

        Assert.Equal(1, outcomes.Count(created => created));
        var reloaded = await new FileLlmConversationStore(_root).TryGetAsync(document.ConversationId);
        Assert.NotNull(reloaded);
        Assert.Equal(document.ConversationId, reloaded!.ConversationId);
        Assert.Equal(document.TranscriptRevision, reloaded.TranscriptRevision);
    }

    [Fact]
    public async Task Concurrent_replace_and_delete_across_independent_instances_leave_no_corruption()
    {
        var document = CreateDocument();
        await new FileLlmConversationStore(_root).CreateAsync(document);
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var replace = Task.Run(async () =>
        {
            var store = new FileLlmConversationStore(_root);
            await start.Task;
            try
            {
                await store.ReplaceAsync(
                    document with { Title = "Replacement", TranscriptRevision = document.TranscriptRevision + 1 },
                    document.TranscriptRevision);
            }
            catch (LlmConversationException exception)
                when (exception.Kind == LlmConversationFailureKind.NotFound)
            {
            }
        });
        var delete = Task.Run(async () =>
        {
            var store = new FileLlmConversationStore(_root);
            await start.Task;
            await store.DeleteAsync(document.ConversationId);
        });

        start.SetResult();
        await Task.WhenAll(replace, delete);

        Assert.Null(await new FileLlmConversationStore(_root).TryGetAsync(document.ConversationId));
        Assert.Empty(EnumerateTemporaryFiles());
    }

    [Fact]
    public async Task Failed_atomic_write_removes_the_temporary_file()
    {
        var store = new FileLlmConversationStore(_root, async (path, payload, cancellationToken) =>
        {
            await File.WriteAllTextAsync(path, payload, cancellationToken);
            throw new IOException("Injected write failure.");
        });
        var document = CreateDocument();

        await Assert.ThrowsAsync<IOException>(() => store.CreateAsync(document));

        Assert.Empty(EnumerateTemporaryFiles());
        Assert.Null(await new FileLlmConversationStore(_root).TryGetAsync(document.ConversationId));
    }

    [Fact]
    public async Task Canceled_atomic_write_removes_the_temporary_file()
    {
        using var cancellation = new CancellationTokenSource();
        var store = new FileLlmConversationStore(_root, async (path, payload, cancellationToken) =>
        {
            await File.WriteAllTextAsync(path, payload, cancellationToken);
            cancellation.Cancel();
            cancellationToken.ThrowIfCancellationRequested();
        });
        var document = CreateDocument();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => store.CreateAsync(document, cancellation.Token));

        Assert.Empty(EnumerateTemporaryFiles());
        Assert.False(File.Exists(DocumentPath(document.ConversationId)));
    }

    [Fact]
    public async Task Coordinator_releases_the_canonical_path_entry_after_an_operation()
    {
        var store = new FileLlmConversationStore(Path.Combine(_root, "."));
        var document = CreateDocument();

        await store.CreateAsync(document);

        Assert.False(LlmConversationFileCoordinator.IsTracked(DocumentPath(document.ConversationId)));
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

    [Fact]
    public async Task TryGetAsync_fails_typed_when_active_turn_identity_is_corrupted()
    {
        var store = new FileLlmConversationStore(_root);
        var document = CreateDocument();
        var timestamp = DateTimeOffset.Parse("2026-08-08T10:00:00Z");
        var pending = new LlmConversationTranscriptEntry(
            Guid.NewGuid(), Guid.NewGuid(), LlmMessageRole.User, "Pending", timestamp);
        var active = document with
        {
            Entries = document.Entries.Add(pending),
            ActiveTurn = new LlmConversationActiveTurn(
                pending.TurnId,
                pending.EntryId,
                timestamp,
                document.TranscriptRevision + 1),
            TranscriptRevision = document.TranscriptRevision + 1
        };
        await store.CreateAsync(active);
        var path = DocumentPath(document.ConversationId);
        var json = JsonNode.Parse(await File.ReadAllTextAsync(path))!;
        json["activeTurn"]!["turnId"] = Guid.NewGuid();
        await File.WriteAllTextAsync(path, json.ToJsonString());

        var exception = await Assert.ThrowsAsync<LlmConversationException>(
            () => store.TryGetAsync(document.ConversationId));

        Assert.Equal(LlmConversationFailureKind.StorageCorrupted, exception.Kind);
    }

    [Fact]
    public async Task Active_turn_compensation_round_trips_with_the_admitted_revision()
    {
        var store = new FileLlmConversationStore(_root);
        var document = CreateDocument();
        var timestamp = DateTimeOffset.Parse("2026-08-08T10:00:00Z");
        var pending = new LlmConversationTranscriptEntry(
            Guid.NewGuid(), Guid.NewGuid(), LlmMessageRole.User, "Pending adoption", timestamp);
        var adoptedProvider = new LlmConversationProviderSnapshot(
            Guid.NewGuid(), "Adopted provider", ProviderKind.OpenAi, "adopted-model");
        var admittedRevision = document.TranscriptRevision + 1;
        var active = new LlmConversationDocument(
            document.ConversationId,
            document.Title,
            adoptedProvider,
            document.CreatedAtUtc,
            timestamp,
            admittedRevision,
            document.Entries.Add(pending),
            new LlmConversationActiveTurn(
                pending.TurnId,
                pending.EntryId,
                timestamp,
                admittedRevision,
                new LlmConversationTurnCompensation(document.Provider, document.AccelerationState)),
            accelerationState: null);

        await store.CreateAsync(active);
        var reloaded = Assert.IsType<LlmConversationDocument>(
            await new FileLlmConversationStore(_root).TryGetAsync(document.ConversationId));

        Assert.Equal(active.ActiveTurn, reloaded.ActiveTurn);
        Assert.Equal(admittedRevision, reloaded.ActiveTurn!.AdmittedRevision);
        Assert.Equal(document.Provider, reloaded.ActiveTurn.Compensation!.Provider);
        Assert.Equal(document.AccelerationState, reloaded.ActiveTurn.Compensation.AccelerationState);
    }

    [Fact]
    public async Task TryGetAsync_fails_typed_for_a_legacy_schema_active_turn_without_compensation_metadata()
    {
        var store = new FileLlmConversationStore(_root);
        var document = CreateDocument();
        var timestamp = DateTimeOffset.Parse("2026-08-08T10:00:00Z");
        var pending = new LlmConversationTranscriptEntry(
            Guid.NewGuid(), Guid.NewGuid(), LlmMessageRole.User, "Legacy pending", timestamp);
        var admittedRevision = document.TranscriptRevision + 1;
        var active = document with
        {
            Entries = document.Entries.Add(pending),
            ActiveTurn = new LlmConversationActiveTurn(
                pending.TurnId, pending.EntryId, timestamp, admittedRevision),
            TranscriptRevision = admittedRevision
        };
        await store.CreateAsync(active);
        var path = DocumentPath(document.ConversationId);
        var json = JsonNode.Parse(await File.ReadAllTextAsync(path))!;
        json["schemaVersion"] = 1;
        await File.WriteAllTextAsync(path, json.ToJsonString());

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

    private string DocumentPath(Guid conversationId)
        => Path.Combine(_root, "conversations", conversationId.ToString("N") + ".json");

    private IEnumerable<string> EnumerateTemporaryFiles()
        => Directory.Exists(Path.Combine(_root, "conversations"))
            ? Directory.EnumerateFiles(Path.Combine(_root, "conversations"), "*.tmp-*", SearchOption.TopDirectoryOnly)
            : [];
}
