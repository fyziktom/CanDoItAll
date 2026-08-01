using System.Buffers;
using System.Text.Json;
using CanDoItAll.Memory.Abstractions;

namespace CanDoItAll.Memory.Http;

internal static class HttpMemoryProviderResponseReader
{
    private const int BufferSize = 16 * 1024;

    public static async Task<T?> ReadJsonAsync<T>(
        HttpContent content,
        MemoryProviderResponseSizeLimit sizeLimit,
        JsonSerializerOptions serializerOptions,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(serializerOptions);
        sizeLimit.EnsureValid();

        if (content.Headers.ContentLength is { } declaredLength &&
            declaredLength > sizeLimit.MaximumBytes)
        {
            throw new HttpMemoryProviderResponseTooLargeException(sizeLimit);
        }

        await using var source = await content.ReadAsStreamAsync(cancellationToken);
        using var payload = new MemoryStream(
            capacity: (int)Math.Min(sizeLimit.MaximumBytes, BufferSize));
        var buffer = ArrayPool<byte>.Shared.Rent(BufferSize);
        try
        {
            long totalBytes = 0;
            while (true)
            {
                var remainingBytes = sizeLimit.MaximumBytes - totalBytes;
                var requestedBytes = (int)Math.Min(buffer.Length, remainingBytes + 1);
                var read = await source.ReadAsync(
                    buffer.AsMemory(0, requestedBytes),
                    cancellationToken);
                if (read == 0)
                {
                    break;
                }

                if (read > remainingBytes)
                {
                    throw new HttpMemoryProviderResponseTooLargeException(sizeLimit);
                }

                await payload.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                totalBytes += read;
            }

            payload.Position = 0;
            return await JsonSerializer.DeserializeAsync<T>(
                payload,
                serializerOptions,
                cancellationToken);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }
}

internal sealed class HttpMemoryProviderResponseTooLargeException(
    MemoryProviderResponseSizeLimit sizeLimit) : Exception
{
    public MemoryProviderResponseSizeLimit SizeLimit { get; } = sizeLimit;
}
