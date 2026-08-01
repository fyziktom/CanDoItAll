using System.Text.Json;
using CanDoItAll.Memory.Abstractions;

namespace CanDoItAll.Memory.Application;

public static class MemoryRuntimeOperationExtensionKeys
{
    public const string AcceptedOperation = "host.candoitall.memory.operation.accepted";
    public const string Caller = "host.candoitall.memory.operation.caller";
    public const string ContextDelivery = "host.candoitall.memory.operation.contextDelivery";
}

public sealed record MemoryOperationCallerMetadata(
    MemoryOperationCallerKind Kind,
    string Route);

public sealed record MemoryContextDeliveryMetadata(
    MemoryContextPackId ContextPackId,
    MemoryFeedbackHandle FeedbackHandle);

public static class MemoryRuntimeOperationExtensions
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static MemoryExtensionData WithMemoryOperationCaller(
        this MemoryExtensionData extensions,
        MemoryOperationCaller caller)
    {
        ArgumentNullException.ThrowIfNull(extensions);
        ArgumentNullException.ThrowIfNull(caller);

        var values = extensions.Values.ToDictionary(
            pair => pair.Key,
            pair => pair.Value,
            StringComparer.Ordinal);
        values[MemoryRuntimeOperationExtensionKeys.Caller] =
            JsonSerializer.SerializeToElement(
                new MemoryOperationCallerMetadata(caller.Kind, caller.Route),
                JsonOptions);
        return new MemoryExtensionData(values);
    }

    public static MemoryOperationCallerMetadata? GetMemoryOperationCaller(this MemoryExtensionData extensions)
    {
        ArgumentNullException.ThrowIfNull(extensions);

        return extensions.Values.TryGetValue(MemoryRuntimeOperationExtensionKeys.Caller, out var value)
            ? value.Deserialize<MemoryOperationCallerMetadata>(JsonOptions)
            : null;
    }

    public static MemoryExtensionData WithAcceptedOperation(
        this MemoryExtensionData extensions,
        MemoryOperationAccepted acceptedOperation)
    {
        ArgumentNullException.ThrowIfNull(extensions);
        ArgumentNullException.ThrowIfNull(acceptedOperation);

        var values = extensions.Values.ToDictionary(
            pair => pair.Key,
            pair => pair.Value,
            StringComparer.Ordinal);
        values[MemoryRuntimeOperationExtensionKeys.AcceptedOperation] =
            JsonSerializer.SerializeToElement(acceptedOperation, JsonOptions);
        return new MemoryExtensionData(values);
    }

    public static MemoryOperationAccepted? GetAcceptedOperation(this MemoryExtensionData extensions)
    {
        ArgumentNullException.ThrowIfNull(extensions);

        return extensions.Values.TryGetValue(MemoryRuntimeOperationExtensionKeys.AcceptedOperation, out var value)
            ? value.Deserialize<MemoryOperationAccepted>(JsonOptions)
            : null;
    }

    public static MemoryExtensionData WithContextDelivery(
        this MemoryExtensionData extensions,
        MemoryContextPackId contextPackId,
        MemoryFeedbackHandle feedbackHandle)
    {
        ArgumentNullException.ThrowIfNull(extensions);

        var values = extensions.Values.ToDictionary(
            pair => pair.Key,
            pair => pair.Value,
            StringComparer.Ordinal);
        values[MemoryRuntimeOperationExtensionKeys.ContextDelivery] =
            JsonSerializer.SerializeToElement(
                new MemoryContextDeliveryMetadata(contextPackId, feedbackHandle),
                JsonOptions);
        return new MemoryExtensionData(values);
    }

    public static MemoryContextDeliveryMetadata? GetContextDelivery(this MemoryExtensionData extensions)
    {
        ArgumentNullException.ThrowIfNull(extensions);

        return extensions.Values.TryGetValue(MemoryRuntimeOperationExtensionKeys.ContextDelivery, out var value)
            ? value.Deserialize<MemoryContextDeliveryMetadata>(JsonOptions)
            : null;
    }
}
