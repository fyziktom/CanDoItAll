using System.Collections.Immutable;
using System.Security.Cryptography;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Persistence;

internal sealed class FileSandboxWorkspaceSnapshotCache(int maximumEntries = 16,
    long maximumEstimatedBytes = 64 * 1024 * 1024) {
    internal const int MaximumPayloadBytes = 8 * 1024 * 1024;
    private readonly object sync = new();
    private readonly Dictionary<(string Path, Type Type), CachedWorkspaceSnapshot> entries = [];
    private long sequence;
    private long estimatedBytes;

    internal int Count {
        get {
            lock (sync) {
                return entries.Count;
            }
        }
    }

    internal long EstimatedRetainedBytes {
        get {
            lock (sync) {
                return estimatedBytes;
            }
        }
    }

    internal static bool Supports<T>(long length) => length <= MaximumPayloadBytes &&
        (typeof(T) == typeof(ExecutionRunRecord) || typeof(T) == typeof(ChatSessionRecord));

    internal bool TryRead<T>(string path, ReadOnlySpan<byte> payload, out T? value) {
        var digest = Convert.ToHexString(SHA256.HashData(payload));
        lock (sync) {
            var key = (path, typeof(T));
            if (entries.TryGetValue(key, out var entry) && entry.ContentDigest == digest) {
                entries[key] = entry with { LastAccess = ++sequence };
                value = (T)entry.Value;
                return true;
            }
            Remove(key);
        }
        value = default;
        return false;
    }

    internal T Remember<T>(string path, ReadOnlySpan<byte> payload, T value) {
        var frozen = Freeze(value);
        var weight = checked(payload.Length * 4L + 4096L);
        var digest = Convert.ToHexString(SHA256.HashData(payload));
        lock (sync) {
            var key = (path, typeof(T));
            Remove(key);
            if (maximumEntries <= 0 || weight > maximumEstimatedBytes || !Supports<T>(payload.Length)) {
                return frozen;
            }
            while (entries.Count >= maximumEntries || estimatedBytes + weight > maximumEstimatedBytes) {
                Remove(entries.MinBy(item => item.Value.LastAccess).Key);
            }
            entries.Add(key, new(frozen!, digest, weight, ++sequence));
            estimatedBytes += weight;
        }
        return frozen;
    }

    internal void Remove<T>(string path) {
        lock (sync) {
            Remove((path, typeof(T)));
        }
    }

    private void Remove((string Path, Type Type) key) {
        if (entries.Remove(key, out var entry)) {
            estimatedBytes -= entry.EstimatedBytes;
        }
    }

    private static T Freeze<T>(T value) => (T)(object)(value switch {
        ExecutionRunRecord run => run with { PendingApprovals = run.PendingApprovals.ToImmutableArray() },
        ChatSessionRecord chat => chat with {
            Messages = chat.Messages.ToImmutableArray(),
            Compatibility = chat.Compatibility is { } compatibility
                ? compatibility with { PendingApprovals = compatibility.PendingApprovals.ToImmutableArray() }
                : null
        },
        _ => throw new InvalidOperationException("Only immutable execution run and chat snapshots can be retained.")
    });
}

internal sealed record CachedWorkspaceSnapshot(object Value, string ContentDigest, long EstimatedBytes, long LastAccess);
