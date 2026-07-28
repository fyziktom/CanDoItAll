using System.Buffers;
using System.Collections.Immutable;

namespace CanDoItAll.AgentFramework.Core;

internal sealed class AgentRuntimeInputAttachmentSizeException(
    string sourcePath,
    long maximumBytes,
    long observedBytes)
    : InvalidOperationException(
        $"Image attachment '{sourcePath}' exceeds the {maximumBytes:N0}-byte per-image limit.")
{
    public string SourcePath { get; } = sourcePath;

    public long MaximumBytes { get; } = maximumBytes;

    public long ObservedBytes { get; } = observedBytes;
}

internal static class AgentRuntimeInputAttachmentPolicy
{
    public const int MaximumImageCount = 8;
    public const long MaximumImageBytes = 10 * 1024 * 1024;

    public static async Task<ImmutableArray<byte>> ReadFileAsync(
        string fullPath,
        string sourcePath,
        CancellationToken cancellationToken)
    {
        await using var input = new FileStream(
            fullPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 81920,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        var capacity = (int)Math.Min(
            MaximumImageBytes,
            Math.Max(0L, input.Length));
        using var output = capacity > 0
            ? new MemoryStream(capacity)
            : new MemoryStream();
        await CopyBoundedAsync(
            input,
            output,
            sourcePath,
            MaximumImageBytes,
            cancellationToken);
        return ImmutableArray.Create(
            output.GetBuffer(),
            0,
            checked((int)output.Length));
    }

    public static async Task<long> CopyBoundedAsync(
        Stream input,
        Stream output,
        string sourcePath,
        long maximumBytes,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumBytes);

        var buffer = ArrayPool<byte>.Shared.Rent(81920);
        try
        {
            long copied = 0;
            while (true)
            {
                var remainingProbeBytes = maximumBytes - copied + 1;
                var readLength = checked((int)Math.Min(
                    buffer.Length,
                    remainingProbeBytes));
                var read = await input
                    .ReadAsync(
                        buffer.AsMemory(0, readLength),
                        cancellationToken)
                    .ConfigureAwait(false);
                if (read == 0)
                {
                    return copied;
                }

                copied += read;
                if (copied > maximumBytes)
                {
                    throw new AgentRuntimeInputAttachmentSizeException(
                        sourcePath,
                        maximumBytes,
                        copied);
                }

                await output
                    .WriteAsync(
                        buffer.AsMemory(0, read),
                        cancellationToken)
                    .ConfigureAwait(false);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }
}
