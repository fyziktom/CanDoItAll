using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.AgentFramework.Llm.Abstractions;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure;
using CanDoItAll.Infrastructure.FileSystem;

namespace CanDoItAll.AgentFramework.Llm.Conversations;

/// <summary>
/// File-backed conversation store: one JSON document per conversation under
/// <c>{root}/conversations/{id}.json</c>. The persistence schema is an explicit
/// versioned DTO decoupled from the contract records, and loads re-validate through the contract
/// constructors so a corrupted or tampered file fails typed instead of yielding partial state.
/// </summary>
public sealed class FileLlmConversationStore : ILlmConversationStore
{
    private const int CurrentSchemaVersion = 2;
    private static readonly TimeSpan CoordinationTimeout = TimeSpan.FromSeconds(15);

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true
    };

    private readonly string _managedRoot;
    private readonly string _conversationsRoot;
    private readonly IPhysicalFileSystemPathPolicyFactory _pathPolicyFactory;
    private readonly DurableFileWriter _durableFileWriter;
    private readonly Func<CancellationToken, ValueTask>? _beforeCommit;

    public FileLlmConversationStore(string rootPath)
        : this(rootPath, new PhysicalFileSystemPathPolicyFactory())
    {
    }

    private FileLlmConversationStore(
        string rootPath,
        IPhysicalFileSystemPathPolicyFactory pathPolicyFactory)
        : this(rootPath, pathPolicyFactory, new DurableFileWriter(pathPolicyFactory))
    {
    }

    public FileLlmConversationStore(
        string rootPath,
        IPhysicalFileSystemPathPolicyFactory pathPolicyFactory,
        DurableFileWriter durableFileWriter)
        : this(rootPath, pathPolicyFactory, durableFileWriter, beforeCommit: null)
    {
    }

    internal FileLlmConversationStore(
        string rootPath,
        Func<CancellationToken, ValueTask> beforeCommit)
        : this(
            rootPath,
            new PhysicalFileSystemPathPolicyFactory(),
            durableFileWriter: null,
            beforeCommit)
    {
    }

    private FileLlmConversationStore(
        string rootPath,
        IPhysicalFileSystemPathPolicyFactory pathPolicyFactory,
        DurableFileWriter? durableFileWriter,
        Func<CancellationToken, ValueTask>? beforeCommit)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        ArgumentNullException.ThrowIfNull(pathPolicyFactory);
        IPhysicalFileSystemPathPolicy policy = pathPolicyFactory.Create(rootPath);
        _managedRoot = policy.RootPath;
        _conversationsRoot = policy.ResolveContainedPath("conversations");
        _pathPolicyFactory = pathPolicyFactory;
        _durableFileWriter = durableFileWriter ?? new DurableFileWriter(pathPolicyFactory);
        _beforeCommit = beforeCommit;
    }

    public async Task<LlmConversationDocument> CreateAsync(
        LlmConversationDocument document, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        string path = DocumentPath(document.ConversationId);
        await using IAsyncDisposable lease = await AcquireCoordinationAsync(path, cancellationToken)
            .ConfigureAwait(false);
        if (File.Exists(path))
        {
            throw new LlmConversationException(
                LlmConversationFailureKind.AlreadyExists, document.ConversationId);
        }

        await WriteAtomicallyAsync(path, document, cancellationToken).ConfigureAwait(false);
        return document;
    }

    public async Task<LlmConversationDocument?> TryGetAsync(
        Guid conversationId, CancellationToken cancellationToken = default)
    {
        string path = DocumentPath(conversationId);
        await using IAsyncDisposable lease = await AcquireCoordinationAsync(path, cancellationToken)
            .ConfigureAwait(false);
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

        string path = DocumentPath(document.ConversationId);
        await using IAsyncDisposable lease = await AcquireCoordinationAsync(path, cancellationToken)
            .ConfigureAwait(false);
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

    public async Task<IReadOnlyList<LlmConversationSummary>> ListAsync(CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(_conversationsRoot))
        {
            return [];
        }

        _pathPolicyFactory.Create(_managedRoot).EnsureSafePath(_conversationsRoot);
        var summaries = new List<LlmConversationSummary>();
        foreach (var path in Directory.EnumerateFiles(_conversationsRoot, "*.json", SearchOption.TopDirectoryOnly)
                     .OrderBy(Path.GetFileName, StringComparer.Ordinal)
                     .ThenBy(path => path, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var fileName = Path.GetFileNameWithoutExtension(path);
            if (!Guid.TryParseExact(fileName, "N", out var conversationId))
            {
                continue;
            }

            await using IAsyncDisposable lease = await AcquireCoordinationAsync(path, cancellationToken)
                .ConfigureAwait(false);
            if (!File.Exists(path))
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
        string path = DocumentPath(conversationId);
        await using IAsyncDisposable lease = await AcquireCoordinationAsync(path, cancellationToken)
            .ConfigureAwait(false);
        if (!File.Exists(path))
        {
            throw new LlmConversationException(LlmConversationFailureKind.NotFound, conversationId);
        }

        await _durableFileWriter.DeleteAsync(
            _managedRoot,
            path,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private string DocumentPath(Guid conversationId)
        => Path.Combine(_conversationsRoot, conversationId.ToString("N") + ".json");

    private ValueTask<IAsyncDisposable> AcquireCoordinationAsync(
        string documentPath,
        CancellationToken cancellationToken)
        => _durableFileWriter.AcquireCoordinationAsync(
            _managedRoot,
            documentPath + ".conversation-transaction.candoitall.lock",
            CoordinationTimeout,
            requirePrivateUnixMode: false,
            cancellationToken);

    private async Task WriteAtomicallyAsync(
        string path, LlmConversationDocument document, CancellationToken cancellationToken)
    {
        string payload = JsonSerializer.Serialize(ConversationDocumentDto.FromDocument(document), SerializerOptions);
        await _durableFileWriter.WriteTextAsync(
            _managedRoot,
            path,
            payload,
            cancellationToken: cancellationToken,
            beforeCommit: _beforeCommit).ConfigureAwait(false);
    }

    private async Task<LlmConversationDocument> ReadAsync(
        Guid conversationId, string path, CancellationToken cancellationToken)
    {
        try
        {
            _pathPolicyFactory.Create(_managedRoot).EnsureSafePath(path);
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
                ProviderSnapshotDto.FromSnapshot(document.Provider),
                document.CreatedAtUtc,
                document.UpdatedAtUtc,
                document.TranscriptRevision,
                [.. document.Entries.Select(TranscriptEntryDto.FromEntry)],
                document.ActiveTurn is null
                    ? null
                    : new ActiveTurnDto(
                        document.ActiveTurn.TurnId,
                        document.ActiveTurn.PendingUserEntryId,
                        document.ActiveTurn.AdmittedAtUtc,
                        document.ActiveTurn.AdmittedRevision,
                        document.ActiveTurn.Compensation is null
                            ? null
                            : ProviderSnapshotDto.FromSnapshot(document.ActiveTurn.Compensation.Provider),
                        document.ActiveTurn.Compensation?.AccelerationState is null
                            ? null
                            : AccelerationEnvelopeDto.FromEnvelope(
                                document.ActiveTurn.Compensation.AccelerationState)),
                document.AccelerationState is null
                    ? null
                    : AccelerationEnvelopeDto.FromEnvelope(document.AccelerationState));

        public LlmConversationDocument ToDocument()
        {
            if (SchemaVersion is < 1 or > CurrentSchemaVersion)
            {
                throw new JsonException($"Unsupported conversation schema version {SchemaVersion}.");
            }

            ArgumentNullException.ThrowIfNull(Provider);
            ArgumentNullException.ThrowIfNull(Entries);
            if (ActiveTurn is not null && SchemaVersion < 2)
            {
                throw new JsonException(
                    "A legacy active turn does not contain durable compensation metadata.");
            }

            return new LlmConversationDocument(
                ConversationId,
                Title ?? string.Empty,
                Provider.ToSnapshot(),
                CreatedAtUtc,
                UpdatedAtUtc,
                TranscriptRevision,
                [.. Entries.Select(static entry => entry.ToEntry())],
                ActiveTurn is null
                    ? null
                    : ActiveTurn.ToActiveTurn(),
                AccelerationState is null
                    ? null
                    : AccelerationState.ToEnvelope());
        }
    }

    private sealed record ProviderSnapshotDto(Guid ProviderId, string ProviderName, string ProviderKind, string Model)
    {
        public static ProviderSnapshotDto FromSnapshot(LlmConversationProviderSnapshot snapshot)
            => new(snapshot.ProviderId, snapshot.ProviderName, snapshot.ProviderKind.ToString(), snapshot.Model);

        public LlmConversationProviderSnapshot ToSnapshot()
            => new(ProviderId, ProviderName, ParseProviderKind(ProviderKind), Model);
    }

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

    private sealed record ActiveTurnDto(
        Guid TurnId,
        Guid PendingUserEntryId,
        DateTimeOffset AdmittedAtUtc,
        long AdmittedRevision,
        ProviderSnapshotDto? PreTurnProvider,
        AccelerationEnvelopeDto? PreTurnAccelerationState)
    {
        public LlmConversationActiveTurn ToActiveTurn()
        {
            if (PreTurnProvider is null && PreTurnAccelerationState is not null)
            {
                throw new JsonException(
                    "Active-turn acceleration compensation requires a pre-turn provider.");
            }

            return new LlmConversationActiveTurn(
                TurnId,
                PendingUserEntryId,
                AdmittedAtUtc,
                AdmittedRevision,
                PreTurnProvider is null
                    ? null
                    : new LlmConversationTurnCompensation(
                        PreTurnProvider.ToSnapshot(), PreTurnAccelerationState?.ToEnvelope()));
        }
    }

    private sealed record AccelerationEnvelopeDto(
        string StrategyId, string ProviderName, string Model, string PayloadJson)
    {
        public static AccelerationEnvelopeDto FromEnvelope(LlmConversationAccelerationEnvelope envelope)
            => new(envelope.StrategyId, envelope.ProviderName, envelope.Model, envelope.PayloadJson);

        public LlmConversationAccelerationEnvelope ToEnvelope()
            => new(StrategyId, ProviderName, Model, PayloadJson);
    }

    private sealed record UsageDto(int InputTokens, int OutputTokens, int CachedInputTokens);

    private static ProviderKind ParseProviderKind(string value)
        => Enum.TryParse<ProviderKind>(value, ignoreCase: false, out var kind) && Enum.IsDefined(kind)
            ? kind
            : throw new JsonException($"Unknown provider kind '{value}'.");
}
