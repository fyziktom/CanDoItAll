using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using CanDoItAll.AgentFramework.Mcp.Abstractions;

namespace CanDoItAll.AgentFramework.Mcp;

internal static class McpJsonRpcFraming
{
    private static readonly Encoding HeaderEncoding = Encoding.ASCII;
    private static readonly byte[] NewlineTerminator = "\n"u8.ToArray();

    public static async Task WriteMessageAsync(
        Stream stream,
        JsonObject payload,
        McpStdioMessageFraming messageFraming,
        CancellationToken cancellationToken)
    {
        var body = JsonSerializer.SerializeToUtf8Bytes(payload);
        if (body.Length > McpPayloadSizeLimit.Default.MaximumBytes)
        {
            throw new McpMessageTooLargeException(
                McpPayloadSizeLimit.Default.MaximumBytes);
        }

        switch (messageFraming)
        {
            case McpStdioMessageFraming.ContentLength:
                await WriteContentLengthMessageAsync(stream, body, cancellationToken);
                return;
            case McpStdioMessageFraming.NewlineDelimitedJson:
                await WriteNewlineDelimitedMessageAsync(stream, body, cancellationToken);
                return;
            default:
                throw new InvalidDataException(
                    $"Unsupported MCP stdio message framing '{messageFraming}'.");
        }
    }

    public static Task WriteMessageAsync(
        Stream stream,
        JsonObject payload,
        CancellationToken cancellationToken)
    {
        return WriteMessageAsync(
            stream,
            payload,
            McpStdioMessageFraming.ContentLength,
            cancellationToken);
    }

    public static Task<string> ReadMessageAsync(
        Stream stream,
        McpStdioMessageFraming messageFraming,
        CancellationToken cancellationToken)
    {
        return McpJsonRpcMessageReader.ReadAsync(
            stream,
            messageFraming,
            cancellationToken);
    }

    public static Task<string> ReadMessageAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        return ReadMessageAsync(
            stream,
            McpStdioMessageFraming.ContentLength,
            cancellationToken);
    }

    private static async Task WriteContentLengthMessageAsync(
        Stream stream,
        byte[] body,
        CancellationToken cancellationToken)
    {
        var header = HeaderEncoding.GetBytes(
            $"Content-Length: {body.Length}\r\n\r\n");
        await stream.WriteAsync(header, cancellationToken);
        await stream.WriteAsync(body, cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }

    private static async Task WriteNewlineDelimitedMessageAsync(
        Stream stream,
        byte[] body,
        CancellationToken cancellationToken)
    {
        await stream.WriteAsync(body, cancellationToken);
        await stream.WriteAsync(NewlineTerminator, cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }
}
