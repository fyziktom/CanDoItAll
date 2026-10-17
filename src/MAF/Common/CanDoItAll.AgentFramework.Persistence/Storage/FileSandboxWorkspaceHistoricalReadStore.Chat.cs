using CanDoItAll.SharedKernel;
using System.Security.Cryptography;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;

namespace CanDoItAll.AgentFramework.Persistence;

public sealed partial class FileSandboxWorkspaceHistoricalReadStore {
    internal const int HistoricalChatTokenLimit = 1024 * 1024;

    private async Task<AgentHistoricalChatIdentity?> ReadHistoricalChatAsync(string path, HistoricalReadPass pass, CancellationToken cancellationToken) {
        if (!pass.FileExists(path)) {
            return null;
        }
        using var activity = AgentFrameworkTelemetry.ActivitySource.StartActivity("workspace.historical.read");
        activity?.SetTag("payload.type", nameof(AgentHistoricalChatIdentity));
        await using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete,
            bufferSize: 16 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
        if (file.Length > HistoricalChatScanLimit) {
            throw new HistoricalStorageRefusal(AgentHistoricalStorageReadStatus.SizeLimitExceeded);
        }
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        using var bounded = new HistoricalReadStream(file, HistoricalChatScanLimit, hash);
        var buffer = new byte[16 * 1024];
        var buffered = 0;
        var state = new JsonReaderState();
        var identity = new HistoricalChatProjection();
        try {
            while (true) {
                cancellationToken.ThrowIfCancellationRequested();
                var read = await bounded.ReadAsync(buffer.AsMemory(buffered), cancellationToken);
                buffered += read;
                var consumed = ReadHistoricalChatTokens(buffer.AsSpan(0, buffered), read == 0, ref state, ref identity);
                buffer.AsSpan(consumed, buffered - consumed).CopyTo(buffer);
                buffered -= consumed;
                if (read == 0) {
                    if (buffered != 0 || !identity.Complete || !identity.HasId || !identity.HasAgentId ||
                        identity.Id == Guid.Empty || identity.AgentId == Guid.Empty) {
                        throw new InvalidDataException("The historical chat has no complete identity projection.");
                    }
                    return new(identity.Id, identity.AgentId);
                }
                if (buffered == buffer.Length) {
                    if (buffer.Length == HistoricalChatTokenLimit) {
                        throw new HistoricalStorageRefusal(AgentHistoricalStorageReadStatus.SizeLimitExceeded);
                    }
                    Array.Resize(ref buffer, Math.Min(HistoricalChatTokenLimit, checked(buffer.Length * 2)));
                }
            }
        } finally {
            activity?.SetTag("payload.bytes", bounded.Position);
        }
    }

    private static int ReadHistoricalChatTokens(ReadOnlySpan<byte> bytes, bool final, ref JsonReaderState state,
        ref HistoricalChatProjection identity) {
        var reader = new Utf8JsonReader(bytes, final, state);
        while (reader.Read()) {
            if (!identity.Started) {
                if (reader.TokenType != JsonTokenType.StartObject) {
                    throw new InvalidDataException("The historical chat identity requires an object.");
                }
                identity.Started = true;
                continue;
            }
            if (reader.CurrentDepth == 0 && reader.TokenType == JsonTokenType.EndObject) {
                identity.Complete = true;
                continue;
            }
            if (reader.CurrentDepth != 1) {
                continue;
            }
            if (reader.TokenType == JsonTokenType.PropertyName) {
                identity.Pending = reader.ValueTextEquals("id"u8) || reader.ValueTextEquals("Id"u8)
                    ? HistoricalChatField.Id
                    : reader.ValueTextEquals("agentId"u8) || reader.ValueTextEquals("AgentId"u8)
                    ? HistoricalChatField.AgentId : HistoricalChatField.None;
                continue;
            }
            if (identity.Pending == HistoricalChatField.None) {
                continue;
            }
            if (reader.TokenType != JsonTokenType.String || !reader.TryGetGuid(out var value)) {
                throw new InvalidDataException("The historical chat identity contains an invalid identifier.");
            }
            if (identity.Pending == HistoricalChatField.Id) {
                if (identity.HasId) {
                    throw new InvalidDataException("The historical chat contains duplicate identifiers.");
                }
                identity.Id = value;
                identity.HasId = true;
            } else {
                if (identity.HasAgentId) {
                    throw new InvalidDataException("The historical chat contains duplicate Agent identifiers.");
                }
                identity.AgentId = value;
                identity.HasAgentId = true;
            }
            identity.Pending = HistoricalChatField.None;
        }
        state = reader.CurrentState;
        return checked((int)reader.BytesConsumed);
    }

    private enum HistoricalChatField { None, Id, AgentId }

    private struct HistoricalChatProjection {
        internal bool Started;
        internal bool Complete;
        internal bool HasId;
        internal bool HasAgentId;
        internal Guid Id;
        internal Guid AgentId;
        internal HistoricalChatField Pending;
    }
}
