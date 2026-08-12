using System.Buffers;
using System.Text;
using CanDoItAll.AgentFramework.Mcp.Abstractions;

namespace CanDoItAll.AgentFramework.Mcp;

internal static class McpJsonRpcMessageReader
{
    public static Task<string> ReadAsync(
        Stream stream,
        McpStdioMessageFraming messageFraming,
        CancellationToken cancellationToken,
        McpPayloadSizeLimit? payloadSizeLimit = null)
    {
        return new McpJsonRpcStreamReader(
                stream,
                messageFraming,
                payloadSizeLimit ?? McpPayloadSizeLimit.Default)
            .ReadAsync(cancellationToken);
    }
}

internal sealed class McpJsonRpcStreamReader
{
    private const int MaximumHeaderBytes = 8192;
    private static readonly Encoding HeaderEncoding = Encoding.ASCII;
    private static readonly Encoding BodyEncoding = new UTF8Encoding(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);
    private static readonly byte[] HeaderTerminator = "\r\n\r\n"u8.ToArray();

    private readonly Stream stream;
    private readonly McpStdioMessageFraming messageFraming;
    private readonly McpPayloadSizeLimit payloadSizeLimit;
    private readonly byte[] readBuffer = new byte[4096];
    private int readOffset;
    private int readCount;

    public McpJsonRpcStreamReader(
        Stream stream,
        McpStdioMessageFraming messageFraming,
        McpPayloadSizeLimit payloadSizeLimit)
    {
        this.stream = stream ?? throw new ArgumentNullException(nameof(stream));
        this.messageFraming = messageFraming;
        payloadSizeLimit.EnsureValid();
        this.payloadSizeLimit = payloadSizeLimit;
    }

    public Task<string> ReadAsync(CancellationToken cancellationToken)
    {
        return messageFraming switch
        {
            McpStdioMessageFraming.ContentLength =>
                ReadContentLengthMessageAsync(cancellationToken),
            McpStdioMessageFraming.NewlineDelimitedJson =>
                ReadNewlineDelimitedMessageAsync(cancellationToken),
            _ => throw new InvalidDataException(
                $"Unsupported MCP stdio message framing '{messageFraming}'.")
        };
    }

    private async Task<string> ReadContentLengthMessageAsync(
        CancellationToken cancellationToken)
    {
        var header = await ReadHeaderAsync(cancellationToken);
        var contentLength = ParseContentLength(header);
        if (contentLength > payloadSizeLimit.MaximumBytes)
        {
            throw MessageTooLarge();
        }

        var body = new byte[contentLength];
        var totalRead = CopyBufferedBytes(body);
        while (totalRead < body.Length)
        {
            var read = await stream.ReadAsync(
                body.AsMemory(totalRead, body.Length - totalRead),
                cancellationToken);
            if (read == 0)
            {
                throw new EndOfStreamException(
                    "MCP stdio stream ended while reading a message body.");
            }

            totalRead += read;
        }

        return DecodeBody(body);
    }

    private async Task<string> ReadNewlineDelimitedMessageAsync(
        CancellationToken cancellationToken)
    {
        while (true)
        {
            var body = await ReadNewlineDelimitedLineAsync(cancellationToken);
            if (body.Length > 0)
            {
                return body;
            }
        }
    }

    private async Task<string> ReadNewlineDelimitedLineAsync(
        CancellationToken cancellationToken)
    {
        var body = new ArrayBufferWriter<byte>(512);
        while (true)
        {
            await EnsureBufferedAsync(
                "MCP stdio stream ended while reading a newline-delimited message.",
                cancellationToken);
            var available = readBuffer.AsSpan(readOffset, readCount);
            var newlineIndex = available.IndexOf((byte)'\n');
            var bytesToCopy = newlineIndex >= 0
                ? newlineIndex
                : available.Length;
            if (body.WrittenCount + bytesToCopy > payloadSizeLimit.MaximumBytes)
            {
                throw MessageTooLarge();
            }

            Append(body, available[..bytesToCopy]);
            AdvanceReadBuffer(bytesToCopy + (newlineIndex >= 0 ? 1 : 0));
            if (newlineIndex < 0)
            {
                continue;
            }

            var length = body.WrittenCount;
            if (length > 0 && body.WrittenSpan[length - 1] == (byte)'\r')
            {
                length--;
            }

            return DecodeBody(body.WrittenSpan[..length]);
        }
    }

    private async Task<string> ReadHeaderAsync(CancellationToken cancellationToken)
    {
        var headerBytes = new List<byte>(128);
        while (!EndsWith(headerBytes, HeaderTerminator))
        {
            await EnsureBufferedAsync(
                "MCP stdio stream ended while reading message headers.",
                cancellationToken);
            headerBytes.Add(readBuffer[readOffset]);
            AdvanceReadBuffer(1);
            if (headerBytes.Count > MaximumHeaderBytes)
            {
                throw new InvalidDataException(
                    $"MCP stdio message header exceeded {MaximumHeaderBytes} bytes.");
            }
        }

        return HeaderEncoding.GetString(headerBytes.ToArray());
    }

    private async Task EnsureBufferedAsync(
        string endOfStreamMessage,
        CancellationToken cancellationToken)
    {
        if (readCount > 0)
        {
            return;
        }

        readOffset = 0;
        readCount = await stream.ReadAsync(readBuffer, cancellationToken);
        if (readCount == 0)
        {
            throw new EndOfStreamException(endOfStreamMessage);
        }
    }

    private int CopyBufferedBytes(byte[] destination)
    {
        var bytesToCopy = Math.Min(readCount, destination.Length);
        readBuffer.AsSpan(readOffset, bytesToCopy).CopyTo(destination);
        AdvanceReadBuffer(bytesToCopy);
        return bytesToCopy;
    }

    private void AdvanceReadBuffer(int count)
    {
        readOffset += count;
        readCount -= count;
        if (readCount == 0)
        {
            readOffset = 0;
        }
    }

    private static int ParseContentLength(string header)
    {
        foreach (var line in header.Split(
                     ["\r\n"],
                     StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = line.Split(':', count: 2);
            if (parts.Length == 2 &&
                string.Equals(
                    parts[0].Trim(),
                    "Content-Length",
                    StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(parts[1].Trim(), out var contentLength) &&
                contentLength >= 0)
            {
                return contentLength;
            }
        }

        throw new InvalidDataException(
            "MCP stdio message header is missing a valid Content-Length value.");
    }

    private McpMessageTooLargeException MessageTooLarge()
        => new(payloadSizeLimit.MaximumBytes);

    private static string DecodeBody(ReadOnlySpan<byte> body)
    {
        try
        {
            return BodyEncoding.GetString(body);
        }
        catch (DecoderFallbackException exception)
        {
            throw new InvalidDataException(
                "MCP stdio message body is not valid UTF-8.",
                exception);
        }
    }

    private static void Append(
        ArrayBufferWriter<byte> destination,
        ReadOnlySpan<byte> source)
    {
        source.CopyTo(destination.GetSpan(source.Length));
        destination.Advance(source.Length);
    }

    private static bool EndsWith(List<byte> source, byte[] suffix)
    {
        if (source.Count < suffix.Length)
        {
            return false;
        }

        for (var index = 0; index < suffix.Length; index++)
        {
            if (source[source.Count - suffix.Length + index] != suffix[index])
            {
                return false;
            }
        }

        return true;
    }
}

internal sealed class McpMessageTooLargeException(int maximumBytes)
    : IOException(
        $"MCP message exceeded the host payload limit of {maximumBytes} bytes.")
{
    public int MaximumBytes { get; } = maximumBytes;
}
