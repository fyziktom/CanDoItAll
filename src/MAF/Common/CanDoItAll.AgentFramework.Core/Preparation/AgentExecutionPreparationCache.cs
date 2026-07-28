using System.Buffers;
using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.AgentFramework.Core;

public sealed record AgentExecutionPreparationRequest
{
    public AgentExecutionPreparationRequest(
        AgentExecutionPreparationKey key,
        AgentExecutionPreparationVersion version)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(version);
        if (!version.CatalogRevision.IsAssigned)
        {
            throw new ArgumentException(
                "A persisted catalog revision is required.",
                nameof(version));
        }
        if (string.IsNullOrWhiteSpace(version.ProviderFingerprint.Value))
        {
            throw new ArgumentException(
                "A provider configuration fingerprint is required.",
                nameof(version));
        }

        Key = key;
        Version = version;
    }

    public AgentExecutionPreparationKey Key { get; }

    public AgentExecutionPreparationVersion Version { get; }
}

public sealed record AgentExecutionPreparationBlueprint
{
    private AgentExecutionPreparationBlueprint(
        AgentExecutionPreparationRequest request,
        AgentDefinition agent,
        ProviderProfile provider,
        ImmutableArray<CapabilityCatalogItem> capabilities,
        ImmutableArray<AgentMemoryRecord> memory,
        DateTimeOffset preparedAtUtc)
    {
        Request = request;
        Agent = agent;
        Provider = provider;
        Capabilities = capabilities;
        Memory = memory;
        PreparedAtUtc = preparedAtUtc;
    }

    public AgentExecutionPreparationRequest Request { get; }

    public AgentDefinition Agent { get; }

    public ProviderProfile Provider { get; }

    public ImmutableArray<CapabilityCatalogItem> Capabilities { get; }

    public ImmutableArray<AgentMemoryRecord> Memory { get; }

    public DateTimeOffset PreparedAtUtc { get; }

    public static AgentExecutionPreparationBlueprint Create(
        AgentExecutionPreparationRequest request,
        AgentDefinition agent,
        ProviderProfile provider,
        IEnumerable<CapabilityCatalogItem> capabilities,
        IEnumerable<AgentMemoryRecord> memory,
        DateTimeOffset? preparedAtUtc = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(agent);
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(capabilities);
        ArgumentNullException.ThrowIfNull(memory);

        if (agent.Id != request.Key.AgentId)
        {
            throw new ArgumentException(
                "The prepared agent does not match the preparation key.",
                nameof(agent));
        }

        if (agent.ProviderProfileId is Guid providerProfileId &&
            providerProfileId != provider.Id)
        {
            throw new ArgumentException(
                "The prepared provider does not match the agent provider profile.",
                nameof(provider));
        }

        var memorySnapshot = memory
            .Select(item => item with { })
            .ToImmutableArray();
        if (memorySnapshot.Any(item => item.AgentId != agent.Id))
        {
            throw new ArgumentException(
                "Prepared memory must belong to the prepared agent.",
                nameof(memory));
        }

        return new AgentExecutionPreparationBlueprint(
            request,
            CloneAgent(agent),
            CloneProvider(provider),
            capabilities.Select(CloneCapability).ToImmutableArray(),
            memorySnapshot,
            preparedAtUtc ?? DateTimeOffset.UtcNow);
    }

    private static AgentDefinition CloneAgent(AgentDefinition agent)
    {
        var permissions = agent.Permissions with
        {
            AllowedSecrets = agent.Permissions.NormalizedAllowedSecrets
                .Select(item => item with { })
                .ToImmutableArray()
        };

        return agent with
        {
            Permissions = permissions,
            Capabilities = agent.Capabilities
                .Select(item => item with { })
                .ToImmutableArray(),
            Tags = agent.Tags.ToImmutableArray()
        };
    }

    private static ProviderProfile CloneProvider(ProviderProfile provider)
    {
        return provider with
        {
            HealthStatus = string.Empty,
            LastCheckedAtUtc = null,
            SuggestedModels = provider.SuggestedModels.ToImmutableArray(),
            ModelPrices = provider.ModelPrices
                .Select(item => item with { })
                .ToImmutableArray(),
            Tags = provider.Tags.ToImmutableArray()
        };
    }

    private static CapabilityCatalogItem CloneCapability(
        CapabilityCatalogItem capability)
    {
        return capability with
        {
            Tags = capability.Tags.ToImmutableArray()
        };
    }
}

public static class ProviderConfigurationFingerprintFactory
{
    public static ProviderConfigurationFingerprint Create(ProviderProfile provider)
    {
        ArgumentNullException.ThrowIfNull(provider);

        var material = new ProviderRuntimeConfiguration(
            provider.Id,
            provider.Kind,
            provider.BaseUrl.Trim(),
            provider.ApiKeyEnvironmentVariable.Trim(),
            provider.DefaultModel.Trim(),
            provider.Transport,
            provider.IsEnabled,
            provider.SupportsStreaming,
            provider.SupportsTools,
            provider.PreferFrameworkManagedChatHistory,
            provider.SupportsBackgroundResponses,
            CanonicalizeConfigurationJson(provider),
            provider.Purpose,
            provider.IsPrivateProvider);
        var serialized = JsonSerializer.Serialize(material);

        return new ProviderConfigurationFingerprint(
            StableContentHash.ComputeSha256Hex(serialized));
    }

    private sealed record ProviderRuntimeConfiguration(
        Guid Id,
        ProviderKind Kind,
        string BaseUrl,
        string ApiKeyEnvironmentVariable,
        string DefaultModel,
        ProviderTransportKind Transport,
        bool IsEnabled,
        bool SupportsStreaming,
        bool SupportsTools,
        bool PreferFrameworkManagedChatHistory,
        bool SupportsBackgroundResponses,
        string ConfigurationJson,
        ProviderProfilePurpose Purpose,
        bool IsPrivateProvider);

    private static string CanonicalizeConfigurationJson(
        ProviderProfile provider)
    {
        try
        {
            using var document = JsonDocument.Parse(
                string.IsNullOrWhiteSpace(provider.ConfigurationJson)
                    ? "{}"
                    : provider.ConfigurationJson);
            var buffer = new ArrayBufferWriter<byte>();
            using (var writer = new Utf8JsonWriter(buffer))
            {
                WriteCanonicalJson(writer, document.RootElement);
            }

            return Encoding.UTF8.GetString(buffer.WrittenSpan);
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException(
                $"Provider '{provider.Id:N}' has invalid runtime configuration JSON.",
                exception);
        }
    }

    private static void WriteCanonicalJson(
        Utf8JsonWriter writer,
        JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in element
                             .EnumerateObject()
                             .OrderBy(
                                 item => item.Name,
                                 StringComparer.Ordinal))
                {
                    writer.WritePropertyName(property.Name);
                    WriteCanonicalJson(writer, property.Value);
                }

                writer.WriteEndObject();
                return;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray())
                {
                    WriteCanonicalJson(writer, item);
                }

                writer.WriteEndArray();
                return;
            default:
                element.WriteTo(writer);
                return;
        }
    }
}

public sealed record AgentExecutionPreparationCachePolicy
{
    public static AgentExecutionPreparationCachePolicy Default { get; } =
        new(64);

    public AgentExecutionPreparationCachePolicy(int maximumEntries)
    {
        if (maximumEntries <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumEntries),
                maximumEntries,
                "Preparation cache capacity must be positive.");
        }

        MaximumEntries = maximumEntries;
    }

    public int MaximumEntries { get; }
}

public enum AgentExecutionPreparationCacheDisposition
{
    Reused,
    Refreshed
}

public enum AgentExecutionPreparationRejectionReason
{
    CapacityExhausted
}

public abstract record AgentExecutionPreparationAcquireResult(
    AgentExecutionPreparationKey Key);

public sealed record AgentExecutionPreparationAcquired(
    AgentExecutionPreparationKey Key,
    AgentExecutionPreparationBlueprint Blueprint,
    AgentExecutionPreparationCacheDisposition Disposition)
    : AgentExecutionPreparationAcquireResult(Key);

public sealed record AgentExecutionPreparationRejected(
    AgentExecutionPreparationKey Key,
    AgentExecutionPreparationRejectionReason Reason,
    int Capacity)
    : AgentExecutionPreparationAcquireResult(Key);

public sealed record AgentExecutionPreparationCacheSnapshot(
    int Capacity,
    int EntryCount,
    int LoadingCount,
    long ReusedCount,
    long RefreshedCount,
    long RejectedCount);

public sealed class AgentExecutionPreparationInvalidatedException(
    AgentExecutionPreparationKey key)
    : OperationCanceledException(
        $"Agent execution preparation for '{key.AgentId}' was invalidated.")
{
    public AgentExecutionPreparationKey Key { get; } = key;
}

public interface IAgentExecutionPreparationCache : IDisposable
{
    Task<AgentExecutionPreparationAcquireResult> AcquireAsync(
        AgentExecutionPreparationRequest request,
        Func<CancellationToken, Task<AgentExecutionPreparationBlueprint>> factory,
        CancellationToken cancellationToken = default);

    void Invalidate(AgentExecutionPreparationKey key);

    void InvalidateAll();

    AgentExecutionPreparationCacheSnapshot Snapshot();
}

public sealed class AgentExecutionPreparationCache :
    IAgentExecutionPreparationCache,
    IDisposable
{
    private readonly object syncRoot = new();
    private readonly Dictionary<AgentExecutionPreparationKey, Entry> entries = [];
    private readonly CancellationTokenSource lifetimeCancellation = new();
    private readonly AgentExecutionPreparationCachePolicy policy;
    private bool disposed;
    private long generation;
    private long accessSequence;
    private long reusedCount;
    private long refreshedCount;
    private long rejectedCount;

    public AgentExecutionPreparationCache(
        AgentExecutionPreparationCachePolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        this.policy = policy;
    }

    public async Task<AgentExecutionPreparationAcquireResult> AcquireAsync(
        AgentExecutionPreparationRequest request,
        Func<CancellationToken, Task<AgentExecutionPreparationBlueprint>> factory,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(factory);

        Entry? entry;
        Entry? supersededEntry = null;
        Entry? evictedEntry = null;
        var created = false;
        AgentExecutionPreparationRejected? rejection = null;

        lock (syncRoot)
        {
            ThrowIfDisposed();

            if (entries.TryGetValue(request.Key, out var current) &&
                current.Request.Version == request.Version)
            {
                current.LastAccessSequence = ++accessSequence;
                entry = current;
                reusedCount++;
            }
            else
            {
                if (current is not null)
                {
                    entries.Remove(request.Key);
                    supersededEntry = current;
                }

                if (entries.Count >= policy.MaximumEntries)
                {
                    evictedEntry = entries.Values
                        .Where(candidate => candidate.Completion.Task.IsCompleted)
                        .MinBy(candidate => candidate.LastAccessSequence);
                    if (evictedEntry is not null)
                    {
                        entries.Remove(evictedEntry.Request.Key);
                    }
                }

                if (entries.Count >= policy.MaximumEntries)
                {
                    rejectedCount++;
                    entry = null;
                    rejection = new AgentExecutionPreparationRejected(
                        request.Key,
                        AgentExecutionPreparationRejectionReason.CapacityExhausted,
                        policy.MaximumEntries);
                }
                else
                {
                    entry = new Entry(
                        request,
                        ++generation,
                        ++accessSequence,
                        CancellationTokenSource.CreateLinkedTokenSource(
                            lifetimeCancellation.Token));
                    entries.Add(request.Key, entry);
                    refreshedCount++;
                    created = true;
                }
            }
        }

        CancelEntry(supersededEntry);
        CancelEntry(evictedEntry);

        if (rejection is not null)
        {
            return rejection;
        }

        if (created)
        {
            _ = CompleteEntryAsync(entry!, factory);
        }

        var blueprint = await entry!.Completion.Task
            .WaitAsync(cancellationToken)
            .ConfigureAwait(false);
        return new AgentExecutionPreparationAcquired(
            request.Key,
            blueprint,
            created
                ? AgentExecutionPreparationCacheDisposition.Refreshed
                : AgentExecutionPreparationCacheDisposition.Reused);
    }

    public void Invalidate(AgentExecutionPreparationKey key)
    {
        ArgumentNullException.ThrowIfNull(key);

        Entry? removed = null;
        lock (syncRoot)
        {
            if (disposed)
            {
                return;
            }

            generation++;
            if (entries.Remove(key, out var entry))
            {
                removed = entry;
            }
        }

        CancelEntry(removed);
    }

    public void InvalidateAll()
    {
        Entry[] removed;
        lock (syncRoot)
        {
            if (disposed)
            {
                return;
            }

            generation++;
            removed = entries.Values.ToArray();
            entries.Clear();
        }

        foreach (var entry in removed)
        {
            CancelEntry(entry);
        }
    }

    public AgentExecutionPreparationCacheSnapshot Snapshot()
    {
        lock (syncRoot)
        {
            ThrowIfDisposed();
            return new AgentExecutionPreparationCacheSnapshot(
                policy.MaximumEntries,
                entries.Count,
                entries.Values.Count(entry => !entry.Completion.Task.IsCompleted),
                reusedCount,
                refreshedCount,
                rejectedCount);
        }
    }

    public void Dispose()
    {
        Entry[] removed;
        lock (syncRoot)
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            generation++;
            removed = entries.Values.ToArray();
            entries.Clear();
        }

        lifetimeCancellation.Cancel();
        foreach (var entry in removed)
        {
            CancelEntry(entry);
        }

        lifetimeCancellation.Dispose();
    }

    private async Task CompleteEntryAsync(
        Entry entry,
        Func<CancellationToken, Task<AgentExecutionPreparationBlueprint>> factory)
    {
        AgentExecutionPreparationBlueprint? blueprint = null;
        Exception? failure = null;

        try
        {
            blueprint = await factory(entry.Cancellation.Token).ConfigureAwait(false);
            if (blueprint.Request != entry.Request)
            {
                throw new InvalidOperationException(
                    "The preparation factory returned a blueprint for a different request.");
            }
        }
        catch (Exception exception)
        {
            failure = exception;
        }

        var wasCurrent = false;
        var isCurrent = false;
        var isDisposed = false;
        var committed = false;
        lock (syncRoot)
        {
            isDisposed = disposed;
            isCurrent = entries.TryGetValue(entry.Request.Key, out var current) &&
                        ReferenceEquals(current, entry);
            wasCurrent = isCurrent;

            if (blueprint is not null && isCurrent)
            {
                committed = entry.Completion.TrySetResult(blueprint);
            }
            else if (failure is not null && isCurrent)
            {
                entries.Remove(entry.Request.Key);
                isCurrent = false;
            }
        }

        if (committed)
        {
            return;
        }

        if (!wasCurrent && !isDisposed)
        {
            entry.Completion.TrySetException(
                new AgentExecutionPreparationInvalidatedException(
                    entry.Request.Key));
        }
        else if (failure is OperationCanceledException)
        {
            entry.Completion.TrySetCanceled(entry.Cancellation.Token);
        }
        else if (failure is not null)
        {
            entry.Completion.TrySetException(failure);
        }
        else
        {
            entry.Completion.TrySetCanceled(entry.Cancellation.Token);
        }

        entry.Cancellation.Dispose();
    }

    private static void CancelEntry(Entry? entry)
    {
        if (entry is null)
        {
            return;
        }

        entry.Cancellation.Cancel();
        if (entry.Completion.Task.IsCompleted)
        {
            entry.Cancellation.Dispose();
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
    }

    private sealed class Entry(
        AgentExecutionPreparationRequest request,
        long generation,
        long lastAccessSequence,
        CancellationTokenSource cancellation)
    {
        public AgentExecutionPreparationRequest Request { get; } = request;

        public long Generation { get; } = generation;

        public long LastAccessSequence { get; set; } = lastAccessSequence;

        public CancellationTokenSource Cancellation { get; } = cancellation;

        public TaskCompletionSource<AgentExecutionPreparationBlueprint> Completion { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
