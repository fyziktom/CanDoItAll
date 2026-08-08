using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.AgentFramework.Llm.Abstractions;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Llm.Conversations;

/// <summary>
/// File-backed conversation store: one JSON document per conversation under
/// <c>{root}/conversations/{id}.json</c>, written atomically (temp file + move) with the optimistic
/// revision compare-and-swap serialized per conversation. The persistence schema is an explicit
/// versioned DTO decoupled from the contract records, and loads re-validate through the contract
/// constructors so a corrupted or tampered file fails typed instead of yielding partial state.
/// Assumes a single writing process per storage root, matching the other file-backed stores.
/// </summary>
public sealed class FileLlmConversationStore : ILlmConversationStore
{
    private const int CurrentSchemaVersion = 1;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true
    };

    private readonly string _conversationsRoot;
    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> _conversationGates = new();

    public FileLlmConversationStore(string rootPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        _conversationsRoot = Path.Combine(Path.GetFullPath(rootPath), "conversations");
    }

    public async Task<LlmConversationDocument> CreateAsync(
        LlmConversationDocument document, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        var gate = ResolveGate(document.ConversationId);
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var path = DocumentPath(document.ConversationId);
            if (File.Exists(path))
            {
                throw new LlmConversationException(
                    LlmConversationFailureKind.AlreadyExists, document.ConversationId);
            }

            await WriteAtomicallyAsync(path, document, cancellationToken).ConfigureAwait(false);
            return document;
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<LlmConversationDocument?> TryGetAsync(
        Guid conversationId, CancellationToken cancellationToken = default)
    {
        var path = DocumentPath(conversationId);
        if (!File.Exists(path))
        {
            return null;
        }

        return await ReadAsync(conversationId, path, cancellationToken).ConfigureAwait(false);
    }

    public async Task<LlmConversationDocument> ReplaceAsync(
        LlmConversationDocument document, long expectedTranscriptRevision, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (document.TranscriptRevision <= expectedTranscriptRevision)
        {
            throw new LlmConversationException(
                LlmConversationFailureKind.InvalidRequest,
                document.ConversationId,
                "A replacement document must advance the transcript revision.");
        }

        var gate = ResolveGate(document.ConversationId);
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var path = DocumentPath(document.ConversationId);
            if (!File.Exists(path))
            {
                throw new LlmConversationException(LlmConversationFailureKind.NotFound, document.ConversationId);
            }

            var stored = await ReadAsync(document.ConversationId, path, cancellationToken).ConfigureAwait(false);
            if (stored.TranscriptRevision != expectedTranscriptRevision)
            {
                throw new LlmConversationException(
                    LlmConversationFailureKind.ConcurrencyConflict,
                    document.ConversationId,
                    $"Stored revision {stored.TranscriptRevision}, expected {expectedTranscriptRevision}.");
            }

            await WriteAtomicallyAsync(path, document, cancellationToken).ConfigureAwait(false);
            return document;
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<IReadOnlyList<LlmConversationSummary>> ListAsync(CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(_conversationsRoot))
        {
            return [];
        }

        var summaries = new List<LlmConversationSummary>();
        foreach (var path in Directory.EnumerateFiles(_conversationsRoot, "*.json", SearchOption.TopDirectoryOnly))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var fileName = Path.GetFileNameWithoutExtension(path);
            if (!Guid.TryParseExact(fileName, "N", out var conversationId))
            {
                continue;
            }

            var document = await ReadAsync(conversationId, path, cancellationToken).ConfigureAwait(false);
            summaries.Add(new LlmConversationSummary(
                document.ConversationId,
                document.Title,
                document.Provider.ProviderName,
                document.Provider.Model,
                document.CreatedAtUtc,
                document.UpdatedAtUtc,
                document.TranscriptRevision,
                document.Entries.Length,
                document.ActiveTurn is not null));
        }

        return summaries;
    }

    public async Task DeleteAsync(Guid conversationId, CancellationToken cancellationToken = default)
    {
        var gate = ResolveGate(conversationId);
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var path = DocumentPath(conversationId);
            if (!File.Exists(path))
            {
                throw new LlmConversationException(LlmConversationFailureKind.NotFound, conversationId);
            }

            File.Delete(path);
        }
        finally
        {
            gate.Release();
        }
    }

    private SemaphoreSlim ResolveGate(Guid conversationId)
        => _conversationGates.GetOrAdd(conversationId, static _ => new SemaphoreSlim(1, 1));

    private string DocumentPath(Guid conversationId)
        => Path.Combine(_conversationsRoot, conversationId.ToString("N") + ".json");

    private async Task WriteAtomicallyAsync(
        string path, LlmConversationDocument document, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_conversationsRoot);
        var payload = JsonSerializer.Serialize(ConversationDocumentDto.FromDocument(document), SerializerOptions);
        var temporaryPath = path + ".tmp-" + Guid.NewGuid().ToString("N");
        await File.WriteAllTextAsync(temporaryPath, payload, cancellationToken).ConfigureAwait(false);
        File.Move(temporaryPath, path, overwrite: true);
    }

    private static async Task<LlmConversationDocument> ReadAsync(
        Guid conversationId, string path, CancellationToken cancellationToken)
    {
        try
        {
            var payload = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
            var dto = JsonSerializer.Deserialize<ConversationDocumentDto>(payload, SerializerOptions)
                      ?? throw new JsonException("The conversation document deserialized to null.");
            return dto.ToDocument();
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException
                                              or ArgumentNullException or ArgumentOutOfRangeException
                                              or FormatException or NotSupportedException or OverflowException)
        {
            throw new LlmConversationException(
                LlmConversationFailureKind.StorageCorrupted,
                conversationId,
                $"File '{Path.GetFileName(path)}'.",
                exception);
        }
    }

    private sealed record ConversationDocumentDto(
        int SchemaVersion,
        Guid ConversationId,
        string Title,
        ProviderSnapshotDto Provider,
        DateTimeOffset CreatedAtUtc,
        DateTimeOffset UpdatedAtUtc,
        long TranscriptRevision,
        IReadOnlyList<TranscriptEntryDto> Entries,
        ActiveTurnDto? ActiveTurn,
        AccelerationEnvelopeDto? AccelerationState)
    {
        public static ConversationDocumentDto FromDocument(LlmConversationDocument document)
            => new(
                CurrentSchemaVersion,
                document.ConversationId,
                document.Title,
                new ProviderSnapshotDto(
                    document.Provider.ProviderId,
                    document.Provider.ProviderName,
                    document.Provider.ProviderKind.ToString(),
                    document.Provider.Model),
                document.CreatedAtUtc,
                document.UpdatedAtUtc,
                document.TranscriptRevision,
                [.. document.Entries.Select(TranscriptEntryDto.FromEntry)],
                document.ActiveTurn is null
                    ? null
                    : new ActiveTurnDto(
                        document.ActiveTurn.TurnId,
                        document.ActiveTurn.PendingUserEntryId,
                        document.ActiveTurn.AdmittedAtUtc),
                document.AccelerationState is null
                    ? null
                    : new AccelerationEnvelopeDto(
                        document.AccelerationState.StrategyId,
                        document.AccelerationState.ProviderName,
                        document.AccelerationState.Model,
                        document.AccelerationState.PayloadJson));

        public LlmConversationDocument ToDocument()
        {
            if (SchemaVersion is < 1 or > CurrentSchemaVersion)
            {
                throw new JsonException($"Unsupported conversation schema version {SchemaVersion}.");
            }

            ArgumentNullException.ThrowIfNull(Provider);
            ArgumentNullException.ThrowIfNull(Entries);
            return new LlmConversationDocument(
                ConversationId,
                Title ?? string.Empty,
                new LlmConversationProviderSnapshot(
                    Provider.ProviderId,
                    Provider.ProviderName,
                    ParseProviderKind(Provider.ProviderKind),
                    Provider.Model),
                CreatedAtUtc,
                UpdatedAtUtc,
                TranscriptRevision,
                [.. Entries.Select(static entry => entry.ToEntry())],
                ActiveTurn is null
                    ? null
                    : new LlmConversationActiveTurn(
                        ActiveTurn.TurnId, ActiveTurn.PendingUserEntryId, ActiveTurn.AdmittedAtUtc),
                AccelerationState is null
                    ? null
                    : new LlmConversationAccelerationEnvelope(
                        AccelerationState.StrategyId,
                        AccelerationState.ProviderName,
                        AccelerationState.Model,
                        AccelerationState.PayloadJson));
        }

        private static ProviderKind ParseProviderKind(string value)
            => Enum.TryParse<ProviderKind>(value, ignoreCase: false, out var kind) && Enum.IsDefined(kind)
                ? kind
                : throw new JsonException($"Unknown provider kind '{value}'.");
    }

    private sealed record ProviderSnapshotDto(Guid ProviderId, string ProviderName, string ProviderKind, string Model);

    private sealed record TranscriptEntryDto(
        Guid EntryId,
        Guid TurnId,
        string Role,
        string Text,
        DateTimeOffset CreatedAtUtc,
        string? Model,
        UsageDto? Usage)
    {
        public static TranscriptEntryDto FromEntry(LlmConversationTranscriptEntry entry)
            => new(
                entry.EntryId,
                entry.TurnId,
                entry.Role.ToString(),
                entry.Text,
                entry.CreatedAtUtc,
                entry.Model.Length == 0 ? null : entry.Model,
                entry.Usage is null
                    ? null
                    : new UsageDto(entry.Usage.InputTokens, entry.Usage.OutputTokens, entry.Usage.CachedInputTokens));

        public LlmConversationTranscriptEntry ToEntry()
            => new(
                EntryId,
                TurnId,
                Enum.TryParse<LlmMessageRole>(Role, ignoreCase: false, out var role) && Enum.IsDefined(role)
                    ? role
                    : throw new JsonException($"Unknown transcript entry role '{Role}'."),
                Text ?? throw new JsonException("A transcript entry requires text."),
                CreatedAtUtc,
                Model ?? string.Empty,
                Usage is null ? null : new LlmUsage(Usage.InputTokens, Usage.OutputTokens, Usage.CachedInputTokens));
    }

    private sealed record ActiveTurnDto(Guid TurnId, Guid PendingUserEntryId, DateTimeOffset AdmittedAtUtc);

    private sealed record AccelerationEnvelopeDto(
        string StrategyId, string ProviderName, string Model, string PayloadJson);

    private sealed record UsageDto(int InputTokens, int OutputTokens, int CachedInputTokens);
}
