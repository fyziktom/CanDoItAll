using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CanDoItAll.Mcp.DotNetWatch.Backend;

internal sealed class BackendRequestReplayStore
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();
    private static readonly TimeSpan Retention = TimeSpan.FromMinutes(10);
    private readonly ConcurrentDictionary<string, ReplayEntry> _entries = new(StringComparer.OrdinalIgnoreCase);

    public async Task<string> ExecuteJsonAsync<TRequest, TResponse>(
        string route,
        string? requestId,
        TRequest request,
        Func<CancellationToken, Task<TResponse>> callback,
        CancellationToken cancellationToken)
    {
        PruneExpired();
        if (string.IsNullOrWhiteSpace(requestId))
        {
            return await ExecuteAndSerializeAsync(callback, cancellationToken);
        }

        var replayKey = CreateKey(route, requestId, request);
        ReplayEntry? createdEntry = null;
        var entry = _entries.GetOrAdd(
            replayKey,
            _ =>
            {
                createdEntry = new ReplayEntry(DateTimeOffset.UtcNow, ExecuteAndSerializeAsync(callback, cancellationToken));
                return createdEntry;
            });

        try
        {
            return await entry.PayloadTask;
        }
        catch
        {
            if (ReferenceEquals(entry, createdEntry))
            {
                _entries.TryRemove(replayKey, out _);
            }

            throw;
        }
    }

    private void PruneExpired()
    {
        var cutoff = DateTimeOffset.UtcNow.Subtract(Retention);
        foreach (var entry in _entries)
        {
            if (entry.Value.CreatedUtc < cutoff && entry.Value.PayloadTask.IsCompleted)
            {
                _entries.TryRemove(entry.Key, out _);
            }
        }
    }

    private static async Task<string> ExecuteAndSerializeAsync<TResponse>(Func<CancellationToken, Task<TResponse>> callback, CancellationToken cancellationToken)
    {
        var response = await callback(cancellationToken);
        return JsonSerializer.Serialize(response, JsonOptions);
    }

    private static string CreateKey<TRequest>(string route, string requestId, TRequest request)
    {
        var requestJson = JsonSerializer.Serialize(request, JsonOptions);
        var requestHash = SHA256.HashData(Encoding.UTF8.GetBytes(requestJson));
        return $"{route}:{requestId.Trim()}:{Convert.ToHexString(requestHash)}";
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private sealed record ReplayEntry(DateTimeOffset CreatedUtc, Task<string> PayloadTask);
}
