using System.Text.Json;

namespace CanDoItAll.Memory.Abstractions;

public static class MemoryOperationRequestContextExtensions
{
    private const string RequestContextKey = "host.candoitall.memory.operation.requestContext";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static MemoryExtensionData WithMemoryRequestContext(
        this MemoryExtensionData extensions,
        MemoryOperationRecord operation,
        MemoryRequestContext context)
    {
        ArgumentNullException.ThrowIfNull(extensions);
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(context);
        EnsureValidContext(context, persisted: false);

        var values = extensions.Values.ToDictionary(
            pair => pair.Key,
            pair => pair.Value,
            StringComparer.Ordinal);
        values[RequestContextKey] = JsonSerializer.SerializeToElement(
            new PersistedMemoryRequestContext(
                operation.OperationId,
                operation.ProviderInstanceId,
                operation.CorrelationId,
                operation.CausationId,
                context),
            JsonOptions);
        return new MemoryExtensionData(values);
    }

    public static MemoryRequestContext GetRequiredMemoryRequestContext(
        this MemoryOperationRecord operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        if (!operation.Extensions.Values.TryGetValue(RequestContextKey, out var value))
        {
            throw new InvalidOperationException(
                "The memory operation does not contain persisted request context.");
        }

        PersistedMemoryRequestContext persisted;
        try
        {
            persisted = value.Deserialize<PersistedMemoryRequestContext>(JsonOptions)
                ?? throw new InvalidOperationException(
                    "The persisted memory operation request context is empty.");
        }
        catch (Exception exception) when (
            exception is JsonException or NotSupportedException or ArgumentException)
        {
            throw new InvalidOperationException(
                "The persisted memory operation request context is malformed.",
                exception);
        }

        if (persisted.OperationId != operation.OperationId ||
            persisted.ProviderInstanceId != operation.ProviderInstanceId ||
            persisted.CorrelationId != operation.CorrelationId ||
            persisted.CausationId != operation.CausationId)
        {
            throw new InvalidOperationException(
                "The persisted memory operation request context does not match its operation envelope.");
        }

        EnsureValidContext(persisted.Context, persisted: true);
        return persisted.Context;
    }

    private static void EnsureValidContext(
        MemoryRequestContext? context,
        bool persisted)
    {
        var valid = context is not null &&
            context.Workspace is not null &&
            context.Workspace.Tags is not null &&
            context.Execution is not null &&
            context.Execution.ArtifactIds is not null &&
            context.Policy is not null &&
            context.Policy.AllowedSourceScopes is not null &&
            context.Budget is not null &&
            context.Extensions is not null;
        if (valid)
        {
            return;
        }

        if (persisted)
        {
            throw new InvalidOperationException(
                "The persisted memory operation request context is incomplete.");
        }

        throw new ArgumentException("Memory request context is incomplete.", nameof(context));
    }

    private sealed record PersistedMemoryRequestContext(
        MemoryOperationId OperationId,
        MemoryProviderInstanceId ProviderInstanceId,
        MemoryCorrelationId CorrelationId,
        MemoryCausationId CausationId,
        MemoryRequestContext Context);
}
