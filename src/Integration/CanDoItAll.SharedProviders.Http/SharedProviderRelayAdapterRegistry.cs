using System.Collections.Frozen;
using System.Text.Json;
using CanDoItAll.SharedProviders.Abstractions;

namespace CanDoItAll.SharedProviders.Http;

internal interface ISharedProviderRelayAdapter
{
    IReadOnlyList<SharedProviderRelayAdapterDescriptor> Descriptors { get; }

    ValueTask<SharedProviderRelayDispatchResult> DispatchAsync(
        SharedProviderRelayDispatchRequest request,
        CancellationToken cancellationToken);
}

internal readonly record struct SharedProviderRelayAdapterKey(
    string ConnectorPluginKey,
    SharedProviderPurpose Purpose,
    SharedProviderRelayOperation Operation);

internal sealed class SharedProviderRelayAdapterKeyComparer : IEqualityComparer<SharedProviderRelayAdapterKey>
{
    public static SharedProviderRelayAdapterKeyComparer Instance { get; } = new();

    public bool Equals(SharedProviderRelayAdapterKey left, SharedProviderRelayAdapterKey right)
        => left.Purpose == right.Purpose &&
            left.Operation == right.Operation &&
            string.Equals(left.ConnectorPluginKey, right.ConnectorPluginKey, StringComparison.OrdinalIgnoreCase);

    public int GetHashCode(SharedProviderRelayAdapterKey key)
        => HashCode.Combine(
            StringComparer.OrdinalIgnoreCase.GetHashCode(key.ConnectorPluginKey),
            key.Purpose,
            key.Operation);
}

internal sealed class SharedProviderRelayAdapterRegistry
{
    private readonly FrozenDictionary<SharedProviderRelayAdapterKey, ISharedProviderRelayAdapter> adapters;

    public SharedProviderRelayAdapterRegistry(IEnumerable<ISharedProviderRelayAdapter> adapters)
    {
        ArgumentNullException.ThrowIfNull(adapters);
        var rows = adapters
            .SelectMany(adapter => adapter.Descriptors.SelectMany(descriptor =>
                descriptor.Support.Operations.Select(operation => new
                {
                    Key = new SharedProviderRelayAdapterKey(
                        descriptor.ConnectorPluginKey,
                        descriptor.Purpose,
                        operation),
                    Adapter = adapter
                })))
            .ToArray();
        if (rows.Length == 0)
        {
            throw new InvalidOperationException("No shared-provider relay adapters are registered.");
        }

        var duplicate = rows
            .GroupBy(row => row.Key, SharedProviderRelayAdapterKeyComparer.Instance)
            .FirstOrDefault(group => group.Skip(1).Any());
        if (duplicate is not null)
        {
            throw new InvalidOperationException(
                $"Duplicate shared-provider relay adapter registration for connector '{duplicate.Key.ConnectorPluginKey}', purpose '{duplicate.Key.Purpose}', and operation '{duplicate.Key.Operation}'.");
        }

        this.adapters = rows.ToFrozenDictionary(
            row => row.Key,
            row => row.Adapter,
            SharedProviderRelayAdapterKeyComparer.Instance);

        Descriptors = Array.AsReadOnly(rows
            .SelectMany(row => row.Adapter.Descriptors)
            .Distinct()
            .OrderBy(descriptor => descriptor.ConnectorPluginKey, StringComparer.Ordinal)
            .ThenBy(descriptor => descriptor.Purpose)
            .ToArray());
    }

    public IReadOnlyList<SharedProviderRelayAdapterDescriptor> Descriptors { get; }

    public bool TryResolve(
        SharedProviderRelayTarget target,
        SharedProviderRelayOperation operation,
        out ISharedProviderRelayAdapter adapter)
    {
        ArgumentNullException.ThrowIfNull(target);
        return adapters.TryGetValue(
            new SharedProviderRelayAdapterKey(target.ConnectorPluginKey, target.Purpose, operation),
            out adapter!);
    }
}

internal sealed class SharedProviderRelayDispatcher(
    SharedProviderRelayAdapterRegistry registry) : ISharedProviderRelayDispatcher
{
    public ValueTask<SharedProviderRelayDispatchResult> DispatchAsync(
        SharedProviderRelayDispatchRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!registry.TryResolve(request.Target, request.Request.Operation, out var adapter))
        {
            return ValueTask.FromResult<SharedProviderRelayDispatchResult>(
                new SharedProviderRelayDispatchResult.Failed(new SharedProviderFailure(
                    SharedProviderFailureCategory.Conflict,
                    new SharedProviderFailureCode("shared_provider_adapter_not_available"),
                    "The published model does not have an available relay adapter.")));
        }

        return adapter.DispatchAsync(request, cancellationToken);
    }
}

internal sealed class SharedProviderOpenAiRelayAdapter(
    SharedProviderHttpRelayClient client) : ISharedProviderRelayAdapter
{
    public IReadOnlyList<SharedProviderRelayAdapterDescriptor> Descriptors { get; } =
        Array.AsReadOnly(SharedProviderRelaySupportCatalog.ProductionDescriptors
            .Where(descriptor => string.Equals(
                descriptor.ConnectorPluginKey,
                SharedProviderConnectorPluginKeys.OpenAi,
                StringComparison.Ordinal))
            .ToArray());

    public ValueTask<SharedProviderRelayDispatchResult> DispatchAsync(
        SharedProviderRelayDispatchRequest request,
        CancellationToken cancellationToken)
        => client.DispatchAsync(request, cancellationToken);
}

internal sealed class SharedProviderOllamaRelayAdapter(
    SharedProviderHttpRelayClient client) : ISharedProviderRelayAdapter
{
    public IReadOnlyList<SharedProviderRelayAdapterDescriptor> Descriptors { get; } =
        Array.AsReadOnly(SharedProviderRelaySupportCatalog.ProductionDescriptors
            .Where(descriptor => descriptor.ConnectorPluginKey is
                SharedProviderConnectorPluginKeys.OllamaLocal or
                SharedProviderConnectorPluginKeys.OllamaRemote)
            .ToArray());

    public ValueTask<SharedProviderRelayDispatchResult> DispatchAsync(
        SharedProviderRelayDispatchRequest request,
        CancellationToken cancellationToken)
        => client.DispatchAsync(request, cancellationToken);
}

internal sealed class SharedProviderComfyUiRelayAdapter(
    ISharedProviderImageCapabilityRelay imageCapability) : ISharedProviderRelayAdapter
{
    private const int MaximumImageBytes = 8 * 1024 * 1024;
    private const int MaximumTotalImageBytes = 32 * 1024 * 1024;

    public IReadOnlyList<SharedProviderRelayAdapterDescriptor> Descriptors { get; } =
        Array.AsReadOnly(SharedProviderRelaySupportCatalog.ProductionDescriptors
            .Where(descriptor => string.Equals(
                descriptor.ConnectorPluginKey,
                SharedProviderConnectorPluginKeys.ComfyUiLocal,
                StringComparison.Ordinal))
            .ToArray());

    public async ValueTask<SharedProviderRelayDispatchResult> DispatchAsync(
        SharedProviderRelayDispatchRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Request.Operation != SharedProviderRelayOperation.ImageGenerations)
        {
            return Failed(
                SharedProviderFailureCategory.Conflict,
                "shared_provider_operation_not_supported",
                "The published model does not support this relay operation.");
        }

        try
        {
            using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutSource.CancelAfter(request.Target.Timeout);
            using var document = JsonDocument.Parse(request.Request.CanonicalPayloadUtf8);
            var root = document.RootElement;
            var prompt = root.GetProperty("prompt").GetString()!;
            var size = ReadString(root, "size", "1024x1024");
            var quality = ReadString(root, "quality", "standard");
            var outputFormat = ReadString(root, "output_format", "png");
            var images = await imageCapability.GenerateAsync(
                new SharedProviderImageCapabilityRequest(
                    request.Target.PublicationId,
                    request.Target.ProviderProfileId,
                    request.Target.UpstreamModelId,
                    prompt,
                    size,
                    quality,
                    outputFormat,
                    request.Request.RequestedImageCount) { Context = request.Context },
                timeoutSource.Token).ConfigureAwait(false);

            if (images.Count == 0 || images.Count > request.Request.RequestedImageCount)
            {
                return Failed(
                    SharedProviderFailureCategory.UpstreamFailure,
                    "shared_provider_image_result_invalid",
                    "The image provider returned an invalid image result.");
            }

            long totalBytes = 0;
            foreach (var image in images)
            {
                totalBytes += image.Bytes.Length;
                if (image.Bytes.IsEmpty ||
                    image.Bytes.Length > MaximumImageBytes ||
                    totalBytes > MaximumTotalImageBytes ||
                    !IsAllowedContentType(image.ContentType, outputFormat))
                {
                    return Failed(
                        SharedProviderFailureCategory.UpstreamFailure,
                        "shared_provider_image_result_invalid",
                        "The image provider returned an invalid image result.");
                }
            }

            using var output = new MemoryStream();
            using (var writer = new Utf8JsonWriter(output))
            {
                writer.WriteStartObject();
                writer.WriteNumber("created", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
                writer.WriteStartArray("data");
                foreach (var image in images)
                {
                    writer.WriteStartObject();
                    writer.WriteBase64String("b64_json", image.Bytes.Span);
                    if (!string.IsNullOrWhiteSpace(image.RevisedPrompt))
                    {
                        writer.WriteString("revised_prompt", image.RevisedPrompt);
                    }

                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
                writer.WriteEndObject();
            }

            return new SharedProviderRelayDispatchResult.Buffered(
                output.ToArray(),
                "application/json",
                SharedProviderRelayResponseHeaders.Empty,
                new SharedProviderRelayUsage(
                    inputTokens: null,
                    outputTokens: null,
                    imageCount: images.Count,
                    SharedProviderRelayUsageCompleteness.Complete));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return Failed(
                SharedProviderFailureCategory.Cancelled,
                "shared_provider_request_cancelled",
                "The shared-provider request was cancelled.");
        }
        catch (OperationCanceledException)
        {
            return Failed(
                SharedProviderFailureCategory.Timeout,
                "shared_provider_upstream_timeout",
                "The upstream provider did not respond before the timeout.");
        }
        catch (TimeoutException)
        {
            return Failed(
                SharedProviderFailureCategory.Timeout,
                "shared_provider_upstream_timeout",
                "The upstream provider did not respond before the timeout.");
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            return Failed(
                SharedProviderFailureCategory.UpstreamFailure,
                "shared_provider_image_upstream_failure",
                "The image provider could not complete the request.");
        }
    }

    private static string ReadString(JsonElement root, string propertyName, string fallback)
        => root.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()!
            : fallback;

    private static bool IsAllowedContentType(string contentType, string outputFormat)
        => outputFormat switch
        {
            "png" => string.Equals(contentType, "image/png", StringComparison.OrdinalIgnoreCase),
            "jpeg" => string.Equals(contentType, "image/jpeg", StringComparison.OrdinalIgnoreCase),
            "webp" => string.Equals(contentType, "image/webp", StringComparison.OrdinalIgnoreCase),
            _ => false
        };

    private static SharedProviderRelayDispatchResult.Failed Failed(
        SharedProviderFailureCategory category,
        string code,
        string message)
        => new(new SharedProviderFailure(
            category,
            new SharedProviderFailureCode(code),
            message));
}
