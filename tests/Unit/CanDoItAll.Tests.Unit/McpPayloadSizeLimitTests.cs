using System.Text;
using System.Text.Json;
using CanDoItAll.AgentFramework.Capabilities.Abstractions;
using CanDoItAll.AgentFramework.Mcp;
using CanDoItAll.AgentFramework.Mcp.Abstractions;
using ModelContextProtocol.Protocol;

namespace CanDoItAll.Tests.Unit;

public sealed class McpPayloadSizeLimitTests
{
    [Fact]
    public void Tool_result_reader_rejects_oversized_text_before_json_parsing()
    {
        var result = new CallToolResult
        {
            Content = [new TextContentBlock { Text = $"{{\"value\":\"{new string('x', 64)}\"}}" }]
        };

        var exception = Assert.Throws<McpSetupException>(() => McpToolResultReader.Read(
            result,
            McpServerKey.Create("memory-mcp"),
            McpToolName.Create("memory_query"),
            new McpPayloadSizeLimit(32)));

        Assert.Equal(CapabilityDiagnosticCategory.SchemaValidation, exception.Category);
        Assert.Contains("32 bytes", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(new string('x', 64), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Tool_result_reader_rejects_oversized_structured_content()
    {
        var result = new CallToolResult
        {
            Content = [],
            StructuredContent = JsonSerializer.SerializeToElement(
                new { value = new string('x', 64) })
        };

        var exception = Assert.Throws<McpSetupException>(() => McpToolResultReader.Read(
            result,
            McpServerKey.Create("memory-mcp"),
            McpToolName.Create("memory_query"),
            new McpPayloadSizeLimit(32)));

        Assert.Equal(CapabilityDiagnosticCategory.SchemaValidation, exception.Category);
        Assert.Contains("32 bytes", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Stdio_reader_rejects_declared_oversized_body_before_allocation()
    {
        await using var stream = new MemoryStream(
            Encoding.ASCII.GetBytes("Content-Length: 33\r\n\r\n"));

        var exception = await Assert.ThrowsAsync<McpMessageTooLargeException>(() =>
            McpJsonRpcMessageReader.ReadAsync(
                stream,
                McpStdioMessageFraming.ContentLength,
                CancellationToken.None,
                new McpPayloadSizeLimit(32)));

        Assert.Contains("32 bytes", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Stdio_reader_stops_newline_payload_at_limit_plus_one()
    {
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(new string('x', 33)));

        var exception = await Assert.ThrowsAsync<McpMessageTooLargeException>(() =>
            McpJsonRpcMessageReader.ReadAsync(
                stream,
                McpStdioMessageFraming.NewlineDelimitedJson,
                CancellationToken.None,
                new McpPayloadSizeLimit(32)));

        Assert.Contains("32 bytes", exception.Message, StringComparison.Ordinal);
        Assert.Equal(33, stream.Position);
    }

    [Fact]
    public async Task Stdio_stream_reader_preserves_multiple_buffered_newline_messages()
    {
        await using var stream = new MemoryStream(
            Encoding.UTF8.GetBytes("{\"id\":1}\n{\"id\":2}\n"));
        var reader = new McpJsonRpcStreamReader(
            stream,
            McpStdioMessageFraming.NewlineDelimitedJson,
            new McpPayloadSizeLimit(32));

        var first = await reader.ReadAsync(CancellationToken.None);
        var second = await reader.ReadAsync(CancellationToken.None);

        Assert.Equal("{\"id\":1}", first);
        Assert.Equal("{\"id\":2}", second);
    }

    [Fact]
    public async Task Stdio_reader_rejects_invalid_utf8()
    {
        await using var stream = new MemoryStream([0xff, (byte)'\n']);

        var exception = await Assert.ThrowsAsync<InvalidDataException>(() =>
            McpJsonRpcMessageReader.ReadAsync(
                stream,
                McpStdioMessageFraming.NewlineDelimitedJson,
                CancellationToken.None));

        Assert.Contains("valid UTF-8", exception.Message, StringComparison.Ordinal);
    }
}
