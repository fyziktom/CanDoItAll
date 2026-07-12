using System.Text;
using CanDoItAll.AgentFramework.Mcp.Abstractions;

namespace CanDoItAll.AgentFramework.Mcp;

internal static class McpJsonRpcMessageReader
{
    private static readonly Encoding HeaderEncoding = Encoding.ASCII;
    private static readonly Encoding BodyEncoding = new UTF8Encoding(
        encoderShouldEmitUTF8Identifier: false);
    private static readonly byte[] HeaderTerminator = "\r\n\r\n"u8.ToArray();

    public static Task<string> ReadAsync(
        Stream stream,
        McpStdioMessageFraming messageFraming,
        CancellationToken cancellationToken,
        McpPayloadSizeLimit? payloadSizeLimit = null)
    {
        var sizeLimit = payloadSizeLimit ?? McpPayloadSizeLimit.Default;
        sizeLimit.EnsureValid();
        return messageFraming switch
        {
            McpStdioMessageFraming.ContentLength =>
                ReadContentLengthMessageAsync(stream, sizeLimit, cancellationToken),
            McpStdioMessageFraming.NewlineDelimitedJson =>
                ReadNewlineDelimitedMessageAsync(stream, sizeLimit, cancellationToken),
            _ => throw new InvalidDataException(
                $"Unsupported MCP stdio message framing '{messageFraming}'.")
        };
    }

    private static async Task<string> ReadContentLengthMessageAsync(
        Stream stream,
        McpPayloadSizeLimit sizeLimit,
        CancellationToken cancellationToken)
    {
        var header = await ReadHeaderAsync(stream, cancellationToken);
        var contentLength = ParseContentLength(header);
        if (contentLength > sizeLimit.MaximumBytes)
        {
            throw MessageTooLarge(sizeLimit);
        }

        var body = new byte[contentLength];
        var totalRead = 0;
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

        return BodyEncoding.GetString(body);
    }

    private static async Task<string> ReadNewlineDelimitedMessageAsync(
        Stream stream,
        McpPayloadSizeLimit sizeLimit,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            var body = await ReadNewlineDelimitedLineAsync(
                stream,
                sizeLimit,
                cancellationToken);
            if (body.Length > 0)
            {
                return BodyEncoding.GetString(body);
            }
        }
    }

    private static async Task<byte[]> ReadNewlineDelimitedLineAsync(
        Stream stream,
        McpPayloadSizeLimit sizeLimit,
        CancellationToken cancellationToken)
    {
        var body = new List<byte>(512);
        var singleByte = new byte[1];
        while (true)
        {
            var read = await stream.ReadAsync(singleByte, cancellationToken);
            if (read == 0)
            {
                if (body.Count == 0)
                {
                    throw new EndOfStreamException(
                        "MCP stdio stream ended while reading a newline-delimited message.");
                }

                return body.ToArray();
            }

            var value = singleByte[0];
            if (value == (byte)'\n')
            {
                if (body.Count > 0 && body[^1] == (byte)'\r')
                {
                    body.RemoveAt(body.Count - 1);
                }

                return body.ToArray();
            }

            body.Add(value);
            if (body.Count > sizeLimit.MaximumBytes)
            {
                throw MessageTooLarge(sizeLimit);
            }
        }
    }

    private static async Task<string> ReadHeaderAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        var headerBytes = new List<byte>(128);
        var singleByte = new byte[1];
        while (!EndsWith(headerBytes, HeaderTerminator))
        {
            var read = await stream.ReadAsync(singleByte, cancellationToken);
            if (read == 0)
            {
                throw new EndOfStreamException(
                    "MCP stdio stream ended while reading message headers.");
            }

            headerBytes.Add(singleByte[0]);
            if (headerBytes.Count > 8192)
            {
                throw new InvalidDataException(
                    "MCP stdio message header exceeded 8192 bytes.");
            }
        }

        return HeaderEncoding.GetString(headerBytes.ToArray());
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

    private static InvalidDataException MessageTooLarge(
        McpPayloadSizeLimit sizeLimit)
    {
        return new InvalidDataException(
            $"MCP message exceeded the host payload limit of {sizeLimit.MaximumBytes} bytes.");
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
