using System.Text.Json;

namespace CanDoItAll.Memory.Abstractions;

public static class MemoryOperationResultExtensions
{
    private const string FinalResultKey = "host.candoitall.memory.operation.finalResult";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static MemoryExtensionData WithFinalOperationResult(
        this MemoryExtensionData extensions,
        MemoryOperationId operationId,
        MemoryProviderInstanceId providerInstanceId,
        MemoryOperationResult result)
    {
        ArgumentNullException.ThrowIfNull(extensions);
        ArgumentNullException.ThrowIfNull(result);
        EnsureOperationMatch(operationId, result.OperationId);

        var values = extensions.Values.ToDictionary(
            pair => pair.Key,
            pair => pair.Value,
            StringComparer.Ordinal);
        values[FinalResultKey] = JsonSerializer.SerializeToElement(
            new PersistedMemoryOperationResult(providerInstanceId, result),
            JsonOptions);
        return new MemoryExtensionData(values);
    }

    public static MemoryOperationResult? GetFinalOperationResult(this MemoryOperationRecord operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        if (!operation.Extensions.Values.TryGetValue(FinalResultKey, out var value))
        {
            return null;
        }

        var persisted = value.Deserialize<PersistedMemoryOperationResult>(JsonOptions)
            ?? throw new InvalidOperationException("Persisted final memory operation result is empty.");
        EnsureOperationMatch(operation.OperationId, persisted.FinalResult.OperationId);
        if (persisted.ProviderInstanceId != operation.ProviderInstanceId)
        {
            throw new InvalidOperationException(
                "Persisted final memory operation result does not match the operation provider.");
        }

        return persisted.FinalResult;
    }

    private static void EnsureOperationMatch(
        MemoryOperationId expected,
        MemoryOperationId actual)
    {
        if (expected != actual)
        {
            throw new InvalidOperationException(
                "Final memory operation result does not match the host operation id.");
        }
    }

    private sealed record PersistedMemoryOperationResult(
        MemoryProviderInstanceId ProviderInstanceId,
        MemoryOperationResult FinalResult);
}
