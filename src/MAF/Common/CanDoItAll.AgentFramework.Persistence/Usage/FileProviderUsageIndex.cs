using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Usage;

namespace CanDoItAll.AgentFramework.Persistence;

internal sealed class FileProviderUsageIndex(FileSandboxWorkspaceStorageLayout layout, FileSandboxWorkspaceJsonStore storage) {
    internal const int Version = 1;
    internal string Root => Path.Combine(layout.DataRoot, "usage-locators");
    internal string HeaderPath => Path.Combine(Root, "index.json");
    internal string CursorPath => Path.Combine(Root, "backfill.json");

    internal async Task<FileUsageIndexHeader?> ReadHeaderAsync(CancellationToken token)
        => await storage.ReadJsonAsync<FileUsageIndexHeader>(HeaderPath, token);

    internal Task WriteHeaderAsync(FileUsageIndexHeader header, CancellationToken token)
        => storage.WriteJsonAtomicallyAsync(HeaderPath, header, token);

    internal string BucketPath(FileUsageIndexHeader header, DateOnly day)
        => Path.Combine(Root, header.Generation.ToString("N"), day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + ".json");

    internal static string Hash(FileUsageIndexBucket bucket)
        => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(bucket)));

    internal async Task IncludeAsync(string path, ProviderUsageObservation observation, CancellationToken token) {
        var locator = Locator(path);
        var header = await ReadHeaderAsync(token) ?? FileUsageIndexHeader.New();
        if (header.Version != Version) {
            throw new InvalidDataException("The usage locator index requires an upgrade. Run usage-index maintenance.");
        }
        var day = DateOnly.FromDateTime(observation.CreatedAtUtc.UtcDateTime);
        var bucket = await storage.ReadJsonAsync<FileUsageIndexBucket>(BucketPath(header, day), token);
        if (header.Buckets.TryGetValue(day, out var expected) && (bucket is null || Hash(bucket) != expected)) {
            await WriteHeaderAsync(header with { Complete = false }, token);
            throw new InvalidDataException("The usage locator bucket is inconsistent. Run usage-index maintenance.");
        }
        var paths = bucket?.Paths.ToHashSet(StringComparer.Ordinal) ?? [];
        if (!paths.Add(locator) && expected is not null) {
            return;
        }
        bucket = new(paths.Order(StringComparer.Ordinal).ToArray());
        await storage.WriteJsonAtomicallyAsync(BucketPath(header, day), bucket, token);
        header.Buckets[day] = Hash(bucket);
        await WriteHeaderAsync(header, token);
    }

    internal string Locator(string path) {
        var relative = Path.GetRelativePath(layout.ExecutionStorageRoot, path).Replace('\\', '/');
        _ = Absolute(relative);
        return relative;
    }

    internal string Absolute(string locator) {
        var parts = locator.Split('/');
        var valid = parts is ["runs", var run, "usage", var name] && Guid.TryParseExact(run, "N", out _) && RecordName(name)
            || parts is ["orphans", "usage", var orphan] && RecordName(orphan);
        if (!valid) {
            throw new InvalidDataException("Invalid canonical usage locator.");
        }
        return Path.Combine(layout.ExecutionStorageRoot, Path.Combine(parts));
    }

    private static bool RecordName(string name)
        => name.EndsWith(".json", StringComparison.Ordinal) && Guid.TryParseExact(name[..^5], "N", out _);

    internal async Task<AgentProviderUsageWindowEvidence> ReadAsync(ProviderUsageWindow window, CancellationToken token) {
        try {
            var header = await ReadHeaderAsync(token);
            if (header is not { Complete: true, Version: Version }) {
                return Indexing();
            }
            var candidates = new HashSet<string>(StringComparer.Ordinal);
            for (var day = DateOnly.FromDateTime(window.FromUtc.UtcDateTime); day <= DateOnly.FromDateTime(window.ToUtc.AddTicks(-1).UtcDateTime); day = day.AddDays(1)) {
                token.ThrowIfCancellationRequested();
                if (!header.Buckets.TryGetValue(day, out var hash)) {
                    continue;
                }
                var bucket = await storage.ReadJsonAsync<FileUsageIndexBucket>(BucketPath(header, day), token);
                if (bucket is null || Hash(bucket) != hash) {
                    await WriteHeaderAsync(header with { Complete = false }, token);
                    return Indexing();
                }
                candidates.UnionWith(bucket.Paths);
            }
            var observations = new List<ProviderUsageObservation>();
            var identities = new List<string>();
            var locators = candidates.Order(StringComparer.Ordinal).ToArray();
            var records = await storage.ReadJsonBatchAsync<ProviderUsageObservation>(locators.Select(Absolute).ToArray(), token);
            var runIds = new HashSet<Guid>();
            for (var index = 0; index < records.Length; index++) {
                token.ThrowIfCancellationRequested();
                var observation = records[index];
                if (observation is null || !window.Contains(observation.CreatedAtUtc)) {
                    continue;
                }
                if (observation.ExecutionRunId is { } runId) {
                    runIds.Add(runId);
                }
                observations.Add(observation);
                identities.Add(observation.Id == Guid.Empty
                    ? "legacy:" + Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(locators[index])))
                    : observation.Id.ToString("D"));
            }
            var runs = await storage.ReadJsonBatchAsync<ExecutionRunRecord>(runIds.Select(layout.RunPath).ToArray(), token);
            return new(new("1.0", runs.OfType<ExecutionRunRecord>().ToArray(), observations),
                ProviderUsageSourceState.Complete, header.CoverageVerifiedAtUtc, identities);
        } catch (InvalidDataException) {
            return Indexing();
        }
    }

    private static AgentProviderUsageWindowEvidence Indexing()
        => new(AgentProviderUsageEvidence.Empty, ProviderUsageSourceState.Indexing, null, [],
            new("agent_usage_index_required", "Agent usage indexing is incomplete or needs repair. Run the usage-index maintenance command."));
}

internal sealed record FileUsageIndexHeader(int Version, Guid Generation, bool Complete,
    Dictionary<DateOnly, string> Buckets, DateTimeOffset? CoverageVerifiedAtUtc) {
    internal static FileUsageIndexHeader New() => new(FileProviderUsageIndex.Version, Guid.NewGuid(), false, [], null);
}

internal sealed record FileUsageIndexBucket(string[] Paths);
